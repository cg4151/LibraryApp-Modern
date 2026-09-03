using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Infrastructure.Caching
{
    /// <summary>
    /// Local SQLite cache for offline operation and performance optimization
    /// Syncs with Koha and Central System when connection is available
    /// </summary>
    public interface ILocalCacheService
    {
        // Cache operations
        Task CacheBookDataAsync(CachedBook book);
        Task CachePatronDataAsync(CachedPatron patron);
        Task<CachedBook> GetCachedBookAsync(string accessionNo);
        Task<CachedPatron> GetCachedPatronAsync(string patronId);
        
        // Offline operations queue
        Task QueueCheckoutAsync(string accessionNo, string patronId);
        Task QueueCheckinAsync(string accessionNo);
        Task<IEnumerable<QueuedOperation>> GetPendingOperationsAsync();
        Task MarkOperationSyncedAsync(int operationId);
        
        // Sync operations
        Task SyncCachedChangesAsync();
        Task<SyncResult> GetLastSyncStatusAsync();
    }

    public class LocalCacheService : ILocalCacheService
    {
        private readonly LocalCacheDbContext _dbContext;
        private readonly ILogger<LocalCacheService> _logger;

        public LocalCacheService(LocalCacheDbContext dbContext, ILogger<LocalCacheService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Cache book data locally
        /// </summary>
        public async Task CacheBookDataAsync(CachedBook book)
        {
            try
            {
                var existingBook = await _dbContext.CachedBooks
                    .FirstOrDefaultAsync(b => b.AccessionNo == book.AccessionNo);

                if (existingBook != null)
                {
                    existingBook.Title = book.Title;
                    existingBook.ISBN = book.ISBN;
                    existingBook.Author = book.Author;
                    existingBook.Status = book.Status;
                    existingBook.RfidUid = book.RfidUid;
                    existingBook.LastUpdated = DateTime.UtcNow;
                    _dbContext.CachedBooks.Update(existingBook);
                }
                else
                {
                    book.CachedAt = DateTime.UtcNow;
                    book.LastUpdated = DateTime.UtcNow;
                    _dbContext.CachedBooks.Add(book);
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Cached book data: {book.AccessionNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to cache book data: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cache patron data locally
        /// </summary>
        public async Task CachePatronDataAsync(CachedPatron patron)
        {
            try
            {
                var existingPatron = await _dbContext.CachedPatrons
                    .FirstOrDefaultAsync(p => p.PatronId == patron.PatronId);

                if (existingPatron != null)
                {
                    existingPatron.Name = patron.Name;
                    existingPatron.Email = patron.Email;
                    existingPatron.Status = patron.Status;
                    existingPatron.MembershipExpiry = patron.MembershipExpiry;
                    existingPatron.LastUpdated = DateTime.UtcNow;
                    _dbContext.CachedPatrons.Update(existingPatron);
                }
                else
                {
                    patron.CachedAt = DateTime.UtcNow;
                    patron.LastUpdated = DateTime.UtcNow;
                    _dbContext.CachedPatrons.Add(patron);
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Cached patron data: {patron.PatronId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to cache patron data: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieve cached book data
        /// </summary>
        public async Task<CachedBook> GetCachedBookAsync(string accessionNo)
        {
            try
            {
                var book = await _dbContext.CachedBooks
                    .FirstOrDefaultAsync(b => b.AccessionNo == accessionNo);
                
                if (book != null)
                {
                    _logger.LogDebug($"Retrieved cached book: {accessionNo}");
                }
                
                return book;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to retrieve cached book: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieve cached patron data
        /// </summary>
        public async Task<CachedPatron> GetCachedPatronAsync(string patronId)
        {
            try
            {
                var patron = await _dbContext.CachedPatrons
                    .FirstOrDefaultAsync(p => p.PatronId == patronId);
                
                if (patron != null)
                {
                    _logger.LogDebug($"Retrieved cached patron: {patronId}");
                }
                
                return patron;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to retrieve cached patron: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Queue checkout operation for offline operation
        /// </summary>
        public async Task QueueCheckoutAsync(string accessionNo, string patronId)
        {
            try
            {
                var operation = new QueuedOperation
                {
                    OperationType = OperationType.CHECKOUT,
                    AccessionNo = accessionNo,
                    PatronId = patronId,
                    QueuedAt = DateTime.UtcNow,
                    Status = OperationStatus.PENDING,
                    RetryCount = 0
                };

                _dbContext.QueuedOperations.Add(operation);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Queued checkout: Patron={patronId}, AccNo={accessionNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to queue checkout: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Queue checkin operation for offline operation
        /// </summary>
        public async Task QueueCheckinAsync(string accessionNo)
        {
            try
            {
                var operation = new QueuedOperation
                {
                    OperationType = OperationType.CHECKIN,
                    AccessionNo = accessionNo,
                    QueuedAt = DateTime.UtcNow,
                    Status = OperationStatus.PENDING,
                    RetryCount = 0
                };

                _dbContext.QueuedOperations.Add(operation);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Queued checkin: AccNo={accessionNo}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to queue checkin: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all pending operations that need to be synced
        /// </summary>
        public async Task<IEnumerable<QueuedOperation>> GetPendingOperationsAsync()
        {
            try
            {
                var operations = await _dbContext.QueuedOperations
                    .Where(o => o.Status == OperationStatus.PENDING)
                    .OrderBy(o => o.QueuedAt)
                    .ToListAsync();
                
                _logger.LogInformation($"Retrieved {operations.Count} pending operations");
                return operations;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to retrieve pending operations: {ex.Message}");
                return Enumerable.Empty<QueuedOperation>();
            }
        }

        /// <summary>
        /// Mark operation as synced
        /// </summary>
        public async Task MarkOperationSyncedAsync(int operationId)
        {
            try
            {
                var operation = await _dbContext.QueuedOperations.FindAsync(operationId);
                if (operation != null)
                {
                    operation.Status = OperationStatus.SYNCED;
                    operation.SyncedAt = DateTime.UtcNow;
                    _dbContext.QueuedOperations.Update(operation);
                    await _dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation($"Marked operation as synced: {operationId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to mark operation as synced: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sync all cached changes with remote systems
        /// </summary>
        public async Task SyncCachedChangesAsync()
        {
            try
            {
                var pendingOps = await GetPendingOperationsAsync();
                
                if (!pendingOps.Any())
                {
                    _logger.LogInformation("No pending operations to sync");
                    return;
                }

                _logger.LogInformation($"Starting sync of {pendingOps.Count()} pending operations");
                
                var syncResult = new SyncResult
                {
                    StartTime = DateTime.UtcNow,
                    TotalOperations = pendingOps.Count()
                };

                foreach (var op in pendingOps)
                {
                    try
                    {
                        // Sync logic would be implemented here
                        // This is a placeholder for the actual sync implementation
                        await MarkOperationSyncedAsync(op.Id);
                        syncResult.SyncedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to sync operation {op.Id}: {ex.Message}");
                        syncResult.FailedCount++;
                        op.RetryCount++;
                    }
                }

                syncResult.EndTime = DateTime.UtcNow;
                syncResult.LastSyncTime = DateTime.UtcNow;

                _logger.LogInformation($"Sync completed: Synced={syncResult.SyncedCount}, Failed={syncResult.FailedCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Sync operation failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get last sync status
        /// </summary>
        public async Task<SyncResult> GetLastSyncStatusAsync()
        {
            try
            {
                var pendingCount = await _dbContext.QueuedOperations
                    .CountAsync(o => o.Status == OperationStatus.PENDING);
                
                return new SyncResult
                {
                    PendingOperations = pendingCount,
                    LastSyncTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get sync status: {ex.Message}");
                return null;
            }
        }
    }

    // ==================== Database Models ====================

    public class CachedBook
    {
        public int Id { get; set; }
        public string AccessionNo { get; set; }
        public string Title { get; set; }
        public string ISBN { get; set; }
        public string Author { get; set; }
        public string Status { get; set; }
        public string RfidUid { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CachedPatron
    {
        public int Id { get; set; }
        public string PatronId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public DateTime MembershipExpiry { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class QueuedOperation
    {
        public int Id { get; set; }
        public OperationType OperationType { get; set; }
        public string AccessionNo { get; set; }
        public string PatronId { get; set; }
        public OperationStatus Status { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? SyncedAt { get; set; }
        public int RetryCount { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum OperationType
    {
        CHECKOUT,
        CHECKIN,
        GATE_WRITE
    }

    public enum OperationStatus
    {
        PENDING,
        SYNCING,
        SYNCED,
        FAILED
    }

    public class SyncResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime LastSyncTime { get; set; }
        public int TotalOperations { get; set; }
        public int SyncedCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingOperations { get; set; }
    }
}
