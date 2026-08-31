using System;
using System.Threading.Tasks;
using LibraryApp.Core.Interfaces;
using LibraryApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Core.Services
{
    /// <summary>
    /// Main library service orchestrating operations
    /// </summary>
    public class LibraryService
    {
        private readonly IKohaService _kohaService;
        private readonly IRfidService _rfidService;
        private readonly ICentralSystemService _centralSystemService;
        private readonly ILogger<LibraryService> _logger;

        public LibraryService(
            IKohaService kohaService,
            IRfidService rfidService,
            ICentralSystemService centralSystemService,
            ILogger<LibraryService> logger)
        {
            _kohaService = kohaService;
            _rfidService = rfidService;
            _centralSystemService = centralSystemService;
            _logger = logger;
        }

        /// <summary>
        /// Checkout book workflow
        /// </summary>
        public async Task<bool> CheckoutBookAsync(int bookId, int memberId)
        {
            try
            {
                _logger.LogInformation($"Starting checkout: Book {bookId}, Member {memberId}");

                // 1. Verify book in Koha
                var book = await _kohaService.GetBookByRfidAsync(bookId.ToString());
                if (book == null || book.Status != BookStatus.Available)
                {
                    _logger.LogWarning($"Book not available: {bookId}");
                    return false;
                }

                // 2. Perform checkout in Koha
                var kohaSuccess = await _kohaService.CheckoutBookAsync(bookId, memberId);
                if (!kohaSuccess)
                {
                    _logger.LogError($"Koha checkout failed: {bookId}");
                    return false;
                }

                // 3. Sync with central system
                var transaction = new Transaction
                {
                    BookId = bookId,
                    MemberId = memberId,
                    Type = TransactionType.Checkout,
                    TransactionDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14)
                };

                var syncSuccess = await _centralSystemService.SyncTransactionAsync(transaction);
                
                _logger.LogInformation($"Checkout completed: Book {bookId}");
                return syncSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkout error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checkin book workflow
        /// </summary>
        public async Task<bool> CheckinBookAsync(int bookId)
        {
            try
            {
                _logger.LogInformation($"Starting checkin: Book {bookId}");

                // 1. Perform checkin in Koha
                var kohaSuccess = await _kohaService.CheckinBookAsync(bookId);
                if (!kohaSuccess)
                {
                    _logger.LogError($"Koha checkin failed: {bookId}");
                    return false;
                }

                // 2. Sync with central system
                var transaction = new Transaction
                {
                    BookId = bookId,
                    Type = TransactionType.Checkin,
                    TransactionDate = DateTime.UtcNow,
                    ReturnDate = DateTime.UtcNow
                };

                var syncSuccess = await _centralSystemService.SyncTransactionAsync(transaction);

                _logger.LogInformation($"Checkin completed: Book {bookId}");
                return syncSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkin error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Read RFID tag and get book info
        /// </summary>
        public async Task<Book> ReadRfidBookAsync()
        {
            try
            {
                _logger.LogInformation("Reading RFID tag...");
                var rfidTag = await _rfidService.ReadTagAsync();
                
                if (string.IsNullOrEmpty(rfidTag))
                {
                    _logger.LogWarning("No RFID tag read");
                    return null;
                }

                var book = await _kohaService.GetBookByRfidAsync(rfidTag);
                return book;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RFID read error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Write RFID tag on new book
        /// </summary>
        public async Task<bool> WriteRfidTagAsync(string bookId, string rfidData)
        {
            try
            {
                _logger.LogInformation($"Writing RFID tag for book: {bookId}");
                return await _rfidService.WriteTagAsync(bookId, rfidData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"RFID write error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check system health
        /// </summary>
        public async Task<SystemHealth> GetSystemHealthAsync()
        {
            var health = new SystemHealth
            {
                Timestamp = DateTime.UtcNow
            };

            health.KohaConnected = await _kohaService.IsConnectedAsync();
            health.CentralSystemConnected = await _centralSystemService.IsConnectedAsync();
            health.RfidConnected = await _rfidService.IsConnectedAsync();

            return health;
        }
    }

    public class SystemHealth
    {
        public DateTime Timestamp { get; set; }
        public bool KohaConnected { get; set; }
        public bool CentralSystemConnected { get; set; }
        public bool RfidConnected { get; set; }
    }
}
