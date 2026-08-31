using System;

namespace LibraryApp.Core.Models
{
    /// <summary>
    /// User entity
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginDate { get; set; }
    }

    public enum UserRole
    {
        Member = 0,
        Librarian = 1,
        Admin = 2
    }
}
