using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Infrastructure.CentralSystem
{
    /// <summary>
    /// Client for communicating with the legacy Central System (NHRM)
    /// Supports three service endpoints: Payment, Base, and Utils
    /// Falls back to Koha API if Central System is unavailable
    /// </summary>
    public interface ICentralSystemService
    {
        Task<bool> AuthenticateAsync(string username, string password);
        Task<CirculationRecord> GetCirculationStatusAsync(string accessionNo);
        Task<bool> SyncCheckoutAsync(string accessionNo, string patronId);
        Task<bool> SyncCheckinAsync(string accessionNo);
        Task<bool> SyncGateWriteOperationAsync(string accessionNo, string rfidUid, string gateId, string status);
        Task<bool> IsConnectedAsync();
    }

    public class CentralSystemClient : ICentralSystemService
    {
        private readonly HttpClient _httpClient;
        private readonly CentralSystemConfiguration _config;
        private readonly ILogger<CentralSystemClient> _logger;
        private string _authToken;
        private DateTime _authTokenExpiry;

        // Service endpoints
        private string PaymentServiceUrl => $"{_config.BaseUrl}/{_config.Services.Payment}";
        private string BaseServiceUrl => $"{_config.BaseUrl}/{_config.Services.Base}";
        private string UtilsServiceUrl => $"{_config.BaseUrl}/{_config.Services.Utils}";

        public CentralSystemClient(
            HttpClient httpClient,
            CentralSystemConfiguration config,
            ILogger<CentralSystemClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure HTTP client
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.Connection.Timeout);
        }

        /// <summary>
        /// Authenticate with Central System using provided credentials
        /// </summary>
        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                var authRequest = new
                {
                    username = username,
                    password = password,
                    timestamp = DateTime.UtcNow
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(authRequest),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{BaseServiceUrl}/authenticate",
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
                    
                    if (jsonDoc.RootElement.TryGetProperty("token", out var tokenProp))
                    {
                        _authToken = tokenProp.GetString();
                        _authTokenExpiry = DateTime.UtcNow.AddHours(1);
                        _logger.LogInformation("Successfully authenticated with Central System");
                        return true;
                    }
                }

                _logger.LogWarning($"Authentication failed: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get circulation status from Central System
        /// </summary>
        public async Task<CirculationRecord> GetCirculationStatusAsync(string accessionNo)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    _logger.LogWarning("Not authenticated with Central System");
                    return null;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseServiceUrl}/circulation/{accessionNo}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                var response = await RetryPolicy(async () => await _httpClient.SendAsync(request));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var record = System.Text.Json.JsonSerializer.Deserialize<CirculationRecord>(content);
                    _logger.LogInformation($"Retrieved circulation status for {accessionNo}");
                    return record;
                }

                _logger.LogWarning($"Failed to get circulation status: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Circulation status error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sync checkout operation with Central System
        /// </summary>
        public async Task<bool> SyncCheckoutAsync(string accessionNo, string patronId)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    _logger.LogWarning("Not authenticated with Central System");
                    return false;
                }

                var checkoutRequest = new
                {
                    accessionNo = accessionNo,
                    patronId = patronId,
                    timestamp = DateTime.UtcNow,
                    operationType = "CHECKOUT"
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(checkoutRequest),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseServiceUrl}/circulation/checkout")
                {
                    Content = content
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                var response = await RetryPolicy(async () => await _httpClient.SendAsync(request));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Checkout synced: Patron={patronId}, AccNo={accessionNo}");
                    return true;
                }

                _logger.LogWarning($"Checkout sync failed: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkout sync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sync checkin operation with Central System
        /// </summary>
        public async Task<bool> SyncCheckinAsync(string accessionNo)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    _logger.LogWarning("Not authenticated with Central System");
                    return false;
                }

                var checkinRequest = new
                {
                    accessionNo = accessionNo,
                    timestamp = DateTime.UtcNow,
                    operationType = "CHECKIN"
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(checkinRequest),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseServiceUrl}/circulation/checkin")
                {
                    Content = content
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                var response = await RetryPolicy(async () => await _httpClient.SendAsync(request));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Checkin synced: AccNo={accessionNo}");
                    return true;
                }

                _logger.LogWarning($"Checkin sync failed: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkin sync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sync anti-theft gate RFID write operation with Central System
        /// </summary>
        public async Task<bool> SyncGateWriteOperationAsync(string accessionNo, string rfidUid, string gateId, string status)
        {
            try
            {
                if (!await EnsureAuthenticatedAsync())
                {
                    _logger.LogWarning("Not authenticated with Central System");
                    return false;
                }

                var gateRequest = new
                {
                    accessionNo = accessionNo,
                    rfidUid = rfidUid,
                    gateId = gateId,
                    status = status,
                    timestamp = DateTime.UtcNow,
                    operationType = "GATE_WRITE"
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(gateRequest),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var request = new HttpRequestMessage(HttpMethod.Post, $"{UtilsServiceUrl}/gate/write-operation")
                {
                    Content = content
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

                var response = await RetryPolicy(async () => await _httpClient.SendAsync(request));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Gate write operation synced: Gate={gateId}, AccNo={accessionNo}, Status={status}");
                    return true;
                }

                _logger.LogWarning($"Gate write sync failed: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Gate write sync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check connection status to Central System
        /// </summary>
        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseServiceUrl}/health");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _authTokenExpiry)
            {
                return true;
            }

            return await AuthenticateAsync(_config.Authentication.Username, _config.Authentication.Password);
        }

        private async Task<HttpResponseMessage> RetryPolicy(Func<Task<HttpResponseMessage>> operation)
        {
            int attempts = 0;
            int maxRetries = _config.Connection.MaxRetries;
            double backoffMultiplier = _config.Connection.BackoffMultiplier;
            int baseDelayMs = 1000;

            while (attempts <= maxRetries)
            {
                try
                {
                    var response = await operation();
                    return response;
                }
                catch (HttpRequestException) when (attempts < maxRetries)
                {
                    attempts++;
                    int delayMs = (int)(baseDelayMs * Math.Pow(backoffMultiplier, attempts - 1));
                    _logger.LogWarning($"Request failed, retrying in {delayMs}ms (attempt {attempts}/{maxRetries})");
                    await Task.Delay(delayMs);
                }
            }

            throw new InvalidOperationException($"Failed after {maxRetries} retries");
        }
    }

    // ==================== Configuration Classes ====================

    public class CentralSystemConfiguration
    {
        public string BaseUrl { get; set; }
        public CentralSystemServices Services { get; set; }
        public CentralSystemAuthentication Authentication { get; set; }
        public CentralSystemConnection Connection { get; set; }
        public CentralSystemSecurity Security { get; set; }
    }

    public class CentralSystemServices
    {
        public string Payment { get; set; }
        public string Base { get; set; }
        public string Utils { get; set; }
    }

    public class CentralSystemAuthentication
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseEnvironmentVariables { get; set; }
    }

    public class CentralSystemConnection
    {
        public int Timeout { get; set; }
        public string RetryPolicy { get; set; }
        public int MaxRetries { get; set; }
        public double BackoffMultiplier { get; set; }
    }

    public class CentralSystemSecurity
    {
        public bool ValidateCertificate { get; set; }
        public bool TrustSelfSigned { get; set; }
    }

    public class CirculationRecord
    {
        public string AccessionNo { get; set; }
        public string PatronId { get; set; }
        public string Status { get; set; }
        public DateTime CheckoutDate { get; set; }
        public DateTime DueDate { get; set; }
        public int RenewCount { get; set; }
        public bool IsOverdue { get; set; }
    }
}
