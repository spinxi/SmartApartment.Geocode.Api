using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartApartment.Geocode.Core.Interfaces;

namespace SmartApartment.Geocode.Infrastructure.Services;

public class DynamoDbCacheService : ICacheService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamoDbCacheService> _logger;
    private readonly string _tableName;

    public DynamoDbCacheService(IAmazonDynamoDB dynamoDb, IConfiguration configuration, ILogger<DynamoDbCacheService> logger)
    {
        _dynamoDb = dynamoDb;
        _configuration = configuration;
        _logger = logger;
        _tableName = _configuration["DynamoDbTableName"] ?? "GeocodeCache";
    }

    public async Task<string?> GetCachedResponseAsync(string key)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "Address", new AttributeValue { S = key } }
            }
        };

        try
        {
            var response = await _dynamoDb.GetItemAsync(request);
            if (response.Item != null && response.Item.ContainsKey("Response"))
            {
                // Check TTL just in case, though DynamoDB handles deletion eventually, it might not be instant.
                if (response.Item.ContainsKey("TTL"))
                {
                    if (long.TryParse(response.Item["TTL"].N, out var ttl))
                    {
                        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ttl)
                        {
                            _logger.LogInformation("Cache expired for key: {Key}", key);
                            return null;
                        }
                    }
                }
                
                _logger.LogInformation("Cache hit for key: {Key}", key);
                return response.Item["Response"].S;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from DynamoDB for key: {Key}", key);
        }

        _logger.LogInformation("Cache miss for key: {Key}", key);
        return null;
    }

    public async Task CacheResponseAsync(string key, string response, TimeSpan ttl)
    {
        var ttlValue = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();

        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "Address", new AttributeValue { S = key } },
                { "Response", new AttributeValue { S = response } },
                { "TTL", new AttributeValue { N = ttlValue.ToString() } }
            }
        };

        try
        {
            await _dynamoDb.PutItemAsync(request);
            _logger.LogInformation("Cached response for key: {Key} with TTL: {TTL}", key, ttlValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to DynamoDB for key: {Key}", key);
        }
    }
}