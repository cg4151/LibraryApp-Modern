using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LibraryApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Infrastructure
{
    /// <summary>
    /// RFID reader service implementation
    /// </summary>
    public class RfidService : IRfidService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RfidService> _logger;
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _readerIp;
        private readonly int _readerPort;

        public RfidService(IConfiguration configuration, ILogger<RfidService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _readerIp = configuration["RFID:ReaderIp"];
            _readerPort = int.Parse(configuration["RFID:ReaderPort"] ?? "10001");
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_readerIp, _readerPort);
                _stream = _client.GetStream();
                _logger.LogInformation($"Connected to RFID reader at {_readerIp}:{_readerPort}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect to RFID reader: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                }
                if (_client != null)
                {
                    _client.Dispose();
                }
                _logger.LogInformation("Disconnected from RFID reader");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disconnecting: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        public async Task<string> ReadTagAsync()
        {
            try
            {
                if (!_client.Connected)
                {
                    await ConnectAsync();
                }

                byte[] buffer = new byte[256];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                string tagId = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                _logger.LogInformation($"RFID tag read: {tagId}");
                return tagId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading RFID tag: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> WriteTagAsync(string tagId, string data)
        {
            try
            {
                if (!_client.Connected)
                {
                    await ConnectAsync();
                }

                string command = $"WRITE|{tagId}|{data}";
                byte[] buffer = Encoding.UTF8.GetBytes(command);
                await _stream.WriteAsync(buffer, 0, buffer.Length);

                _logger.LogInformation($"RFID tag written: {tagId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error writing RFID tag: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                return _client != null && _client.Connected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetReaderStatusAsync()
        {
            try
            {
                if (!_client.Connected)
                {
                    await ConnectAsync();
                }

                byte[] statusCmd = Encoding.UTF8.GetBytes("STATUS");
                await _stream.WriteAsync(statusCmd, 0, statusCmd.Length);

                byte[] buffer = new byte[256];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                string status = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                _logger.LogInformation($"Reader status: {status}");
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting reader status: {ex.Message}");
                return "ERROR";
            }
        }
    }
}
