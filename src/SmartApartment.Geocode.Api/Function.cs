using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartApartment.Geocode.Core.Interfaces;
using SmartApartment.Geocode.Infrastructure.Services;
using Amazon.DynamoDBv2;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SmartApartment.Geocode.Api;

public class Function
{
    private readonly IServiceProvider _serviceProvider;

    public Function()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // AWS services
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonDynamoDB>();

        // Application services
        services.AddHttpClient<IGeocodeService, GoogleGeocodeService>();
        services.AddSingleton<ICacheService, DynamoDbCacheService>();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<Function>>();
        
        try 
        {
            string? address = null;
            if (request.QueryStringParameters != null)
            {
                request.QueryStringParameters.TryGetValue("address", out address);
            }

            if (string.IsNullOrEmpty(address))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "Missing 'address' query parameter."
                };
            }

            logger.LogInformation("Received request for address: {Address}", address);

            var cacheService = _serviceProvider.GetRequiredService<ICacheService>();
            var geocodeService = _serviceProvider.GetRequiredService<IGeocodeService>();

            // Check Cache
            var cachedResponse = await cacheService.GetCachedResponseAsync(address);
            if (!string.IsNullOrEmpty(cachedResponse))
            {
                logger.LogInformation("Returning cached response for address: {Address}", address);
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = cachedResponse,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "X-Cache", "HIT" } }
                };
            }

            // Call google API
            var googleResponse = await geocodeService.GetGeocodeAsync(address);

            // Cache response for 30 days
            await cacheService.CacheResponseAsync(address, googleResponse, TimeSpan.FromDays(30));

            logger.LogInformation("Returning fresh response for address: {Address}", address);
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = googleResponse,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "X-Cache", "MISS" } }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = "Internal Server Error: " + ex.Message
            };
        }
    }
}