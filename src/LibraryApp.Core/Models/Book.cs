using System;

namespace LibraryApp.Core.Models
{
    /// <summary>
    /// Book entity
    /// </summary>
    public class Book
    {
        public int Id { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public DateTime PublishedDate { get; set; }
        public string RfidTag { get; set; }
        public BookStatus Status { get; set; } = BookStatus.Available;
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        public int KohaItemId { get; set; }
    }

    public enum BookStatus
    {
        Available = 0,
        CheckedOut = 1,
        Reserved = 2,
        Damaged = 3,
        Lost = 4,
        Processing = 5
    }
}
