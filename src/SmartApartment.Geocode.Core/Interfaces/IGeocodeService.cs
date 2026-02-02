namespace SmartApartment.Geocode.Core.Interfaces;

public interface IGeocodeService
{
    Task<string> GetGeocodeAsync(string address);
}