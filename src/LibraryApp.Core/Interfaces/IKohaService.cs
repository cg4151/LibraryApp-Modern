using System.Collections.Generic;
using System.Threading.Tasks;
using LibraryApp.Core.Models;

namespace LibraryApp.Core.Interfaces
{
    /// <summary>
    /// Interface for Koha API integration
    /// </summary>
    public interface IKohaService
    {
        Task<Book> GetBookByIsbnAsync(string isbn);
        Task<Book> GetBookByRfidAsync(string rfidTag);
        Task<IEnumerable<Book>> SearchBooksAsync(string query);
        Task<bool> CheckoutBookAsync(int bookId, int memberId);
        Task<bool> CheckinBookAsync(int bookId);
        Task<User> GetUserAsync(int memberId);
        Task<bool> IsConnectedAsync();
    }
}
