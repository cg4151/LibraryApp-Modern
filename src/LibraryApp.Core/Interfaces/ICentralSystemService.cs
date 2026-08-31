using System.Collections.Generic;
using System.Threading.Tasks;
using LibraryApp.Core.Models;

namespace LibraryApp.Core.Interfaces
{
    /// <summary>
    /// Interface for central library system integration
    /// </summary>
    public interface ICentralSystemService
    {
        Task<bool> AuthenticateAsync(string username, string password);
        Task<bool> SyncTransactionAsync(Transaction transaction);
        Task<IEnumerable<Transaction>> GetPendingSyncAsync();
        Task<bool> IsConnectedAsync();
        Task SyncAllPendingAsync();
    }
}
