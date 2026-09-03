using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Core.Services
{
    /// <summary>
    /// Manages RFID reader selection and failover between TCP and Serial COM connections
    /// Implements factory pattern for reader abstraction
    /// </summary>
    public interface IRfidReaderFactory
    {
        Task<IRfidReader> CreateReader(RfidConnectionMode mode);
        Task<IRfidReader> CreateReaderWithFailover();
    }

    public interface IRfidReader
    {
        bool IsConnected { get; }
        RfidConnectionMode ConnectionMode { get; }
        Task ConnectAsync();
        Task DisconnectAsync();
        Task<string> ReadTagAsync(int timeoutMs = 5000);
        Task<bool> WriteTagAsync(string accessionNo, string rfidContent, int timeoutMs = 8000);
        Task<bool> VerifyTagAsync(string expectedContent);
    }

    public enum RfidConnectionMode
    {
        TCP,
        SerialCOM,
        Unknown
    }

    public class RfidReaderFactory : IRfidReaderFactory
    {
        private readonly ILogger<RfidReaderFactory> _logger;
        private readonly RfidConfiguration _config;

        public RfidReaderFactory(RfidConfiguration config, ILogger<RfidReaderFactory> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create RFID reader for specific connection mode
        /// </summary>
        public async Task<IRfidReader> CreateReader(RfidConnectionMode mode)
        {
            IRfidReader reader = mode switch
            {
                RfidConnectionMode.TCP => new TcpRfidReader(_config.ConnectionModes.TCP, _logger),
                RfidConnectionMode.SerialCOM => new SerialComRfidReader(_config.ConnectionModes.SerialCOM, _logger),
                _ => throw new ArgumentException($"Unsupported connection mode: {mode}")
            };

            try
            {
                await reader.ConnectAsync();
                _logger.LogInformation($"RFID reader connected: {mode}");
                return reader;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect RFID reader ({mode}): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create RFID reader with automatic failover (TCP → SerialCOM)
        /// </summary>
        public async Task<IRfidReader> CreateReaderWithFailover()
        {
            if (!_config.FailoverEnabled)
            {
                return await CreateReader(_config.DefaultMode);
            }

            var primaryMode = _config.DefaultMode;
            var secondaryMode = primaryMode == RfidConnectionMode.TCP 
                ? RfidConnectionMode.SerialCOM 
                : RfidConnectionMode.TCP;

            try
            {
                _logger.LogInformation($"Attempting primary connection: {primaryMode}");
                return await CreateReader(primaryMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Primary connection failed ({primaryMode}): {ex.Message}. Attempting fallback...");

                try
                {
                    _logger.LogInformation($"Attempting secondary connection: {secondaryMode}");
                    return await CreateReader(secondaryMode);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError($"Failover failed ({secondaryMode}): {fallbackEx.Message}");
                    throw new InvalidOperationException($"Could not connect to RFID reader via {primaryMode} or {secondaryMode}", fallbackEx);
                }
            }
        }
    }

    /// <summary>
    /// TCP-based RFID reader implementation
    /// Connects to RFID reader via network socket
    /// </summary>
    public class TcpRfidReader : IRfidReader
    {
        private readonly TcpRfidConfiguration _config;
        private readonly ILogger<TcpRfidReader> _logger;
        private System.Net.Sockets.TcpClient _tcpClient;
        private System.IO.StreamReader _reader;
        private System.IO.StreamWriter _writer;
        private int _reconnectAttempts = 0;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        public RfidConnectionMode ConnectionMode => RfidConnectionMode.TCP;

        public TcpRfidReader(TcpRfidConfiguration config, ILogger<TcpRfidReader> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ConnectAsync()
        {
            try
            {
                _tcpClient = new System.Net.Sockets.TcpClient();
                _tcpClient.ReceiveTimeout = _config.ReadTimeout;
                _tcpClient.SendTimeout = _config.WriteTimeout;

                await _tcpClient.ConnectAsync(_config.ReaderIp, _config.ReaderPort);
                
                var networkStream = _tcpClient.GetStream();
                _reader = new System.IO.StreamReader(networkStream);
                _writer = new System.IO.StreamWriter(networkStream) { AutoFlush = true };

                _logger.LogInformation($"Connected to RFID reader via TCP: {_config.ReaderIp}:{_config.ReaderPort}");
                _reconnectAttempts = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"TCP connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _tcpClient?.Close();
                _logger.LogInformation("Disconnected from RFID reader");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Disconnection error: {ex.Message}");
            }
        }

        public async Task<string> ReadTagAsync(int timeoutMs = 5000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RFID reader not connected");
            }

            try
            {
                var cts = new System.Threading.CancellationTokenSource(timeoutMs);
                var task = _reader.ReadLineAsync();
                
                var result = await task.ConfigureAwait(false);
                return result?.Trim();
            }
            catch (System.Threading.OperationCanceledException)
            {
                _logger.LogWarning("Read tag timeout");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read tag: {ex.Message}");
                await AttemptReconnect();
                throw;
            }
        }

        public async Task<bool> WriteTagAsync(string accessionNo, string rfidContent, int timeoutMs = 8000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RFID reader not connected");
            }

            try
            {
                var command = FormatWriteCommand(rfidContent);
                var cts = new System.Threading.CancellationTokenSource(timeoutMs);
                
                await _writer.WriteLineAsync(command);
                
                var response = await ReadTagAsync(timeoutMs);
                return response?.Contains("OK") ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write tag for {accessionNo}: {ex.Message}");
                await AttemptReconnect();
                throw;
            }
        }

        public async Task<bool> VerifyTagAsync(string expectedContent)
        {
            try
            {
                var readContent = await ReadTagAsync(3000);
                return readContent == expectedContent;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to verify tag: {ex.Message}");
                return false;
            }
        }

        private string FormatWriteCommand(string content)
        {
            // Format depends on your RFID reader protocol
            return $"WRITE:{content}";
        }

        private async Task AttemptReconnect()
        {
            if (!_config.Reconnect.Enabled || _reconnectAttempts >= _config.Reconnect.MaxAttempts)
            {
                return;
            }

            _reconnectAttempts++;
            _logger.LogInformation($"Attempting reconnect ({_reconnectAttempts}/{_config.Reconnect.MaxAttempts})...");
            
            await Task.Delay(_config.Reconnect.DelayMs);
            
            try
            {
                await DisconnectAsync();
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reconnect failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Serial COM port RFID reader implementation
    /// Fallback option for networks without TCP capability
    /// </summary>
    public class SerialComRfidReader : IRfidReader
    {
        private readonly SerialComRfidConfiguration _config;
        private readonly ILogger<SerialComRfidReader> _logger;
        private System.IO.Ports.SerialPort _serialPort;

        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public RfidConnectionMode ConnectionMode => RfidConnectionMode.SerialCOM;

        public SerialComRfidReader(SerialComRfidConfiguration config, ILogger<SerialComRfidReader> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ConnectAsync()
        {
            try
            {
                _serialPort = new System.IO.Ports.SerialPort(
                    _config.ComPort,
                    _config.BaudRate,
                    (System.IO.Ports.Parity)Enum.Parse(typeof(System.IO.Ports.Parity), _config.Parity),
                    _config.DataBits,
                    (System.IO.Ports.StopBits)Enum.Parse(typeof(System.IO.Ports.StopBits), _config.StopBits.ToString())
                )
                {
                    ReadTimeout = _config.ReadTimeout,
                    WriteTimeout = _config.WriteTimeout
                };

                _serialPort.Open();
                _logger.LogInformation($"Connected to RFID reader via COM: {_config.ComPort}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Serial COM connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _serialPort?.Close();
                _serialPort?.Dispose();
                _logger.LogInformation("Disconnected from RFID reader");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Disconnection error: {ex.Message}");
            }
        }

        public async Task<string> ReadTagAsync(int timeoutMs = 5000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RFID reader not connected");
            }

            try
            {
                _serialPort.ReadTimeout = timeoutMs;
                return _serialPort.ReadLine()?.Trim();
            }
            catch (System.IO.IOException)
            {
                _logger.LogWarning("Read tag timeout");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to read tag: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> WriteTagAsync(string accessionNo, string rfidContent, int timeoutMs = 8000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RFID reader not connected");
            }

            try
            {
                var command = FormatWriteCommand(rfidContent);
                _serialPort.WriteTimeout = timeoutMs;
                _serialPort.WriteLine(command);
                
                var response = await ReadTagAsync(timeoutMs);
                return response?.Contains("OK") ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write tag for {accessionNo}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> VerifyTagAsync(string expectedContent)
        {
            try
            {
                var readContent = await ReadTagAsync(3000);
                return readContent == expectedContent;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to verify tag: {ex.Message}");
                return false;
            }
        }

        private string FormatWriteCommand(string content)
        {
            return $"WRITE:{content}";
        }
    }

    // ==================== Configuration Classes ====================

    public class RfidConfiguration
    {
        public bool Enabled { get; set; }
        public RfidConnectionMode DefaultMode { get; set; }
        public RfidConnectionModes ConnectionModes { get; set; }
        public bool FailoverEnabled { get; set; }
        public string FailoverStrategy { get; set; }
        public string TagEncoding { get; set; }
        public RfidValidationRules ValidationRules { get; set; }
        public RfidGateOperations GateOperations { get; set; }
    }

    public class RfidConnectionModes
    {
        public TcpRfidConfiguration TCP { get; set; }
        public SerialComRfidConfiguration SerialCOM { get; set; }
    }

    public class TcpRfidConfiguration
    {
        public bool Enabled { get; set; }
        public string ReaderIp { get; set; }
        public int ReaderPort { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
        public RfidReconnectPolicy Reconnect { get; set; }
    }

    public class SerialComRfidConfiguration
    {
        public bool Enabled { get; set; }
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public int StopBits { get; set; }
        public string Parity { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
    }

    public class RfidReconnectPolicy
    {
        public bool Enabled { get; set; }
        public int MaxAttempts { get; set; }
        public int DelayMs { get; set; }
    }

    public class RfidValidationRules
    {
        public int MinTagLength { get; set; }
        public int MaxTagLength { get; set; }
        public string AllowedCharacters { get; set; }
    }

    public class RfidGateOperations
    {
        public bool WriteVerificationEnabled { get; set; }
        public int WriteRetries { get; set; }
        public int VerificationDelay { get; set; }
    }
}
