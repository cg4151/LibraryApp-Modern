using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Core.Services
{
    /// <summary>
    /// Specialized service for anti-theft gate RFID write operations
    /// Coordinates with Koha and Central System for complete lifecycle
    /// </summary>
    public interface IGateRfidWriteService
    {
        Task<GateRfidWriteResult> WriteBookAtGateAsync(
            string accessionNo,
            string bookTitle,
            string isbn,
            string gateId,
            string operatorId);
    }

    public class GateRfidWriteService : IGateRfidWriteService
    {
        private readonly IRfidReaderFactory _rfidFactory;
        private readonly IKohaService _kohaService;
        private readonly ICentralSystemService _centralSystemService;
        private readonly IRfidAuditService _auditService;
        private readonly ILogger<GateRfidWriteService> _logger;
        private readonly RfidConfiguration _rfidConfig;

        public GateRfidWriteService(
            IRfidReaderFactory rfidFactory,
            IKohaService kohaService,
            ICentralSystemService centralSystemService,
            IRfidAuditService auditService,
            RfidConfiguration rfidConfig,
            ILogger<GateRfidWriteService> logger)
        {
            _rfidFactory = rfidFactory ?? throw new ArgumentNullException(nameof(rfidFactory));
            _kohaService = kohaService ?? throw new ArgumentNullException(nameof(kohaService));
            _centralSystemService = centralSystemService ?? throw new ArgumentNullException(nameof(centralSystemService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _rfidConfig = rfidConfig ?? throw new ArgumentNullException(nameof(rfidConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Complete workflow for writing RFID tag at gate:
        /// 1. Get book info from Koha
        /// 2. Connect to RFID reader
        /// 3. Write RFID tag with book accession number
        /// 4. Verify write success
        /// 5. Update Koha with RFID UID
        /// 6. Sync with Central System
        /// 7. Log to audit trail
        /// </summary>
        public async Task<GateRfidWriteResult> WriteBookAtGateAsync(
            string accessionNo,
            string bookTitle,
            string isbn,
            string gateId,
            string operatorId)
        {
            var result = new GateRfidWriteResult
            {
                Timestamp = DateTime.UtcNow,
                AccessionNo = accessionNo,
                BookTitle = bookTitle,
                GateId = gateId,
                OperatorId = operatorId
            };

            try
            {
                _logger.LogInformation($"Starting gate RFID write: AccNo={accessionNo}, Gate={gateId}");

                // Step 1: Validate book exists in Koha
                var kohaBook = await _kohaService.GetItemByAccessionAsync(accessionNo);
                if (kohaBook == null)
                {
                    result.Status = GateWriteStatus.BOOK_NOT_FOUND;
                    result.ErrorMessage = $"Book not found in Koha: {accessionNo}";
                    _logger.LogWarning($"Book not found in Koha: {accessionNo}");
                    return result;
                }

                result.ItemId = kohaBook.ItemId;

                // Step 2: Connect to RFID reader
                IRfidReader reader = null;
                try
                {
                    reader = await _rfidFactory.CreateReaderWithFailover();
                }
                catch (Exception ex)
                {
                    result.Status = GateWriteStatus.READER_CONNECTION_FAILED;
                    result.ErrorMessage = $"Failed to connect to RFID reader: {ex.Message}";
                    _logger.LogError($"RFID reader connection failed: {ex.Message}");
                    return result;
                }

                // Step 3: Write RFID tag with retries
                string rfidContent = FormatRfidContent(accessionNo);
                string rfidUid = null;
                int retryCount = 0;
                int maxRetries = _rfidConfig.GateOperations.WriteRetries;

                while (retryCount <= maxRetries && string.IsNullOrEmpty(rfidUid))
                {
                    try
                    {
                        result.WriteAttempts = retryCount + 1;

                        // Write to RFID tag
                        bool writeSuccess = await reader.WriteTagAsync(accessionNo, rfidContent);
                        if (!writeSuccess)
                        {
                            _logger.LogWarning($"Write returned false (attempt {retryCount + 1}/{maxRetries})");
                            retryCount++;
                            
                            if (retryCount <= maxRetries)
                            {
                                await Task.Delay(_rfidConfig.GateOperations.VerificationDelay);
                                continue;
                            }
                            else
                            {
                                result.Status = GateWriteStatus.WRITE_FAILED;
                                result.ErrorMessage = $"Failed to write tag after {maxRetries} attempts";
                                break;
                            }
                        }

                        // Step 4: Verify write success
                        if (_rfidConfig.GateOperations.WriteVerificationEnabled)
                        {
                            await Task.Delay(_rfidConfig.GateOperations.VerificationDelay);
                            bool verified = await reader.VerifyTagAsync(rfidContent);
                            
                            if (!verified)
                            {
                                _logger.LogWarning($"Write verification failed (attempt {retryCount + 1}/{maxRetries})");
                                retryCount++;
                                
                                if (retryCount <= maxRetries)
                                {
                                    continue;
                                }
                                else
                                {
                                    result.Status = GateWriteStatus.VERIFICATION_FAILED;
                                    result.ErrorMessage = "Write verification failed after retries";
                                    break;
                                }
                            }
                        }

                        // Write successful
                        rfidUid = FormatRfidUid(accessionNo);
                        result.RfidUid = rfidUid;
                        result.Status = GateWriteStatus.WRITE_SUCCESSFUL;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"RFID write exception (attempt {retryCount + 1}): {ex.Message}");
                        retryCount++;
                    }
                }

                // Step 5: Update Koha with RFID UID
                if (!string.IsNullOrEmpty(rfidUid))
                {
                    try
                    {
                        await _kohaService.UpdateItemRfidTagAsync(accessionNo, rfidUid);
                        result.KohaUpdateStatus = "SUCCESS";
                        _logger.LogInformation($"Updated Koha with RFID UID: {rfidUid}");
                    }
                    catch (Exception ex)
                    {
                        result.KohaUpdateStatus = "FAILED";
                        result.ErrorMessage += $"; Koha update failed: {ex.Message}";
                        _logger.LogError($"Failed to update Koha: {ex.Message}");
                    }
                }

                // Step 6: Sync with Central System
                try
                {
                    await _centralSystemService.SyncGateWriteOperationAsync(
                        accessionNo,
                        rfidUid,
                        gateId,
                        result.Status.ToString());
                    
                    result.CentralSystemSyncStatus = "SUCCESS";
                    _logger.LogInformation("Synced with Central System");
                }
                catch (Exception ex)
                {
                    result.CentralSystemSyncStatus = "FAILED";
                    _logger.LogWarning($"Failed to sync with Central System: {ex.Message}");
                }

                // Step 7: Log to audit trail
                try
                {
                    var auditRecord = new GateOperationAudit
                    {
                        Timestamp = DateTime.UtcNow,
                        GateId = gateId,
                        AccessionNo = accessionNo,
                        BookTitle = bookTitle,
                        WriteAttempts = result.WriteAttempts,
                        Status = result.Status.ToString(),
                        RfidUid = rfidUid ?? "N/A",
                        Notes = result.ErrorMessage ?? "Operation completed successfully"
                    };

                    await _auditService.LogGateWriteOperation(auditRecord);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to log audit trail: {ex.Message}");
                }

                // Cleanup
                try
                {
                    if (reader != null)
                    {
                        await reader.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error disconnecting reader: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                result.Status = GateWriteStatus.UNKNOWN_ERROR;
                result.ErrorMessage = ex.Message;
                _logger.LogError($"Unexpected error in gate write operation: {ex}");
            }

            return result;
        }

        private string FormatRfidContent(string accessionNo)
        {
            // Format: ACC-XXXXXX | timestamp
            return $"ACC|{accessionNo}|{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private string FormatRfidUid(string accessionNo)
        {
            // Generate readable UID from accession number
            return $"UID-{accessionNo}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
        }
    }

    // ==================== Models ====================

    public class GateRfidWriteResult
    {
        public DateTime Timestamp { get; set; }
        public string AccessionNo { get; set; }
        public string BookTitle { get; set; }
        public string GateId { get; set; }
        public string OperatorId { get; set; }
        public string ItemId { get; set; }
        public string RfidUid { get; set; }
        public GateWriteStatus Status { get; set; }
        public int WriteAttempts { get; set; }
        public string KohaUpdateStatus { get; set; }
        public string CentralSystemSyncStatus { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum GateWriteStatus
    {
        PENDING,
        BOOK_NOT_FOUND,
        READER_CONNECTION_FAILED,
        WRITE_IN_PROGRESS,
        WRITE_SUCCESSFUL,
        WRITE_FAILED,
        VERIFICATION_FAILED,
        UNKNOWN_ERROR
    }

    // Placeholder interfaces for Koha and Central System
    // These should be implemented in Infrastructure layer
    public interface IKohaService
    {
        Task<BookInfo> GetItemByAccessionAsync(string accessionNo);
        Task UpdateItemRfidTagAsync(string accessionNo, string rfidUid);
    }

    public interface ICentralSystemService
    {
        Task SyncGateWriteOperationAsync(string accessionNo, string rfidUid, string gateId, string status);
    }

    public class BookInfo
    {
        public string ItemId { get; set; }
        public string AccessionNo { get; set; }
        public string Title { get; set; }
        public string ISBN { get; set; }
    }
}
