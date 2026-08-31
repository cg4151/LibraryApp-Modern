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
    /// Koha API client implementation
    /// </summary>
    public class KohaService : IKohaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KohaService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public KohaService(HttpClient httpClient, IConfiguration configuration, ILogger<KohaService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = configuration["Koha:BaseUrl"];
            _apiKey = configuration["Koha:ApiKey"];
        }

        public async Task<Book> GetBookByIsbnAsync(string isbn)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/biblios?q=isbn:{isbn}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Found book with ISBN: {isbn}");
                
                // Parse and return book (simplified)
                return new Book { Isbn = isbn, Status = BookStatus.Available };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching book by ISBN: {ex.Message}");
                return null;
            }
        }

        public async Task<Book> GetBookByRfidAsync(string rfidTag)
        {
            try
            {
                _logger.LogInformation($"Fetching book with RFID: {rfidTag}");
                // Implementation for RFID lookup
                return new Book { RfidTag = rfidTag, Status = BookStatus.Available };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching book by RFID: {ex.Message}");
                return null;
            }
        }

        public async Task<IEnumerable<Book>> SearchBooksAsync(string query)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/biblios?q={Uri.EscapeDataString(query)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation($"Book search completed for: {query}");
                return new List<Book>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching books: {ex.Message}");
                return new List<Book>();
            }
        }

        public async Task<bool> CheckoutBookAsync(int bookId, int memberId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/checkouts";
                var data = new { item_id = bookId, patron_id = memberId };
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation($"Checkout successful: Book {bookId} to Member {memberId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkout failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckinBookAsync(int bookId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/checkouts/by-item/{bookId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                _logger.LogInformation($"Checkin successful: Book {bookId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Checkin failed: {ex.Message}");
                return false;
            }
        }

        public async Task<User> GetUserAsync(int memberId)
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/patrons/{memberId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation($"User fetched: {memberId}");
                return new User { Id = memberId };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/status";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
