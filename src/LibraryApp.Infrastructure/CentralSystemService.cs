using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LibraryApp.Core.Interfaces;
using LibraryApp.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LibraryApp.Infrastructure
{
    /// <summary>
    /// Central library system integration service
    /// </summary>
    public class CentralSystemService : ICentralSystemService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CentralSystemService> _logger;
        private readonly string _baseUrl;
        private string _token;

        public CentralSystemService(HttpClient httpClient, IConfiguration configuration, ILogger<CentralSystemService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = configuration["CentralSystem:BaseUrl"];
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                var url = $"{_baseUrl}/auth/login";
                var data = new { username, password };
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseContent);
                    _token = jsonDoc.RootElement.GetProperty("token").GetString();
                    _logger.LogInformation("Successfully authenticated with central system");
                    return true;
                }
                else
                {
                    _logger.LogError($"Authentication failed: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SyncTransactionAsync(Transaction transaction)
        {
            try
            {
                var url = $"{_baseUrl}/api/transactions/sync";
                var json = JsonSerializer.Serialize(transaction);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Add("Authorization", $"Bearer {_token}");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    transaction.SyncedToCentral = true;
                    _logger.LogInformation($"Transaction synced: {transaction.Id}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Sync failed, will retry: Transaction {transaction.Id}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Sync error: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<Transaction>> GetPendingSyncAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving pending transactions");
                return new List<Transaction>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting pending transactions: {ex.Message}");
                return new List<Transaction>();
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var url = $"{_baseUrl}/api/health";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(_token))
                {
                    request.Headers.Add("Authorization", $"Bearer {_token}");
                }

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task SyncAllPendingAsync()
        {
            try
            {
                _logger.LogInformation("Syncing all pending transactions");
                var pending = await GetPendingSyncAsync();
                
                foreach (var transaction in pending)
                {
                    await SyncTransactionAsync(transaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error syncing pending: {ex.Message}");
            }
        }
    }
}
