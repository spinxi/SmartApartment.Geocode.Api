using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartApartment.Geocode.Core.Interfaces;

namespace SmartApartment.Geocode.Infrastructure.Services;

public class GoogleGeocodeService : IGeocodeService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleGeocodeService> _logger;

    public GoogleGeocodeService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleGeocodeService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetGeocodeAsync(string address)
    {
        var apiKey = _configuration["GoogleApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Google API Key is missing.");
            throw new InvalidOperationException("Google API Key is not configured.");
        }

        var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={apiKey}";
        
        _logger.LogInformation("Calling Google Geocode API for address: {Address}", address);
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google API call failed with status code: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync();
        return content;
    }
}