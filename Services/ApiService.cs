using System;
using System.Net.Http;
using System.Net.Http.Json;
using ViandriveApp.Helpers;

namespace ViandriveApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint.TrimStart('/'));
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Error HTTP: {(int)response.StatusCode} {response.StatusCode}");
                    return default;
                }
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción de red: {ex.Message}");
                throw;
            }
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T model)
        {
            try
            {
                return await _httpClient.PostAsJsonAsync(endpoint.TrimStart('/'), model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excepción de red (POST): {ex.Message}");
                throw;
            }
        }
    }
}
