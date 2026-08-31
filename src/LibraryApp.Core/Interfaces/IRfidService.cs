using System.Threading.Tasks;

namespace LibraryApp.Core.Interfaces
{
    /// <summary>
    /// Interface for RFID reader operations
    /// </summary>
    public interface IRfidService
    {
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<string> ReadTagAsync();
        Task<bool> WriteTagAsync(string tagId, string data);
        Task<bool> IsConnectedAsync();
        Task<string> GetReaderStatusAsync();
    }
}
