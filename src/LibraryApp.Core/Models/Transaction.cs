using System;

namespace LibraryApp.Core.Models
{
    /// <summary>
    /// Transaction entity (checkout, checkin)
    /// </summary>
    public class Transaction
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int MemberId { get; set; }
        public TransactionType Type { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public bool SyncedToCentral { get; set; } = false;
        public string Notes { get; set; }
    }

    public enum TransactionType
    {
        Checkout = 0,
        Checkin = 1,
        Renewal = 2,
        Fine = 3
    }
}
