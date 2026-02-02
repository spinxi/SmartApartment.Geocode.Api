namespace SmartApartment.Geocode.Core.Interfaces;

public interface ICacheService
{
    Task<string?> GetCachedResponseAsync(string key);
    Task CacheResponseAsync(string key, string response, TimeSpan ttl);
}