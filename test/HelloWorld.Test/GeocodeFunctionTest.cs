using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using SmartApartment.Geocode.Api;
using SmartApartment.Geocode.Core.Interfaces;

namespace SmartApartment.Geocode.Tests
{
    /// <summary>
    /// Unit tests for the Geocode Lambda Function
    /// Tests cache-first pattern, error handling, and API integration
    /// </summary>
    public class GeocodeFunctionTest
    {
        private const string TestAddress = "1600 Pennsylvania Avenue NW Washington DC 20500";
        private const string TestAddressEncoded = "1600%20Pennsylvania%20Avenue%20NW%20Washington%20DC%2020500";

        private static readonly string SampleGoogleResponse = @"{
            ""results"": [{
                ""address_components"": [
                    {""long_name"": ""1600"", ""short_name"": ""1600"", ""types"": [""street_number""]},
                    {""long_name"": ""Pennsylvania Avenue Northwest"", ""short_name"": ""Pennsylvania Avenue Northwest"", ""types"": [""route""]}
                ],
                ""formatted_address"": ""1600 Pennsylvania Avenue Northwest, Washington, DC 20500, USA"",
                ""geometry"": {
                    ""location"": {""lat"": 38.8987895, ""lng"": -77.0366305},
                    ""location_type"": ""ROOFTOP""
                },
                ""place_id"": ""ChIJmU5kSwi3t4kR8sNkjcMXN2I"",
                ""types"": [""premise""]
            }],
            ""status"": ""OK""
        }";

        [Fact]
        public async Task TestMissingAddressParameter_ReturnsBadRequest()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = null
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(400, response.StatusCode);
            Assert.Contains("Missing 'address' query parameter", response.Body);
        }

        [Fact]
        public async Task TestEmptyAddressParameter_ReturnsBadRequest()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> { { "address", "" } }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(400, response.StatusCode);
        }

        [Fact]
        public async Task TestValidRequest_ReturnsSuccess()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", TestAddress } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(200, response.StatusCode);
            Assert.NotNull(response.Body);
            Assert.Contains("Content-Type", response.Headers);
            Assert.True(
                response.Headers["Content-Type"] == "application/json",
                "Response should be JSON"
            );
        }

        [Fact]
        public async Task TestResponseIncludesCacheHeader()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", TestAddress } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.True(
                response.Headers.ContainsKey("X-Cache"),
                "Response should include X-Cache header indicating HIT or MISS"
            );
            Assert.True(
                response.Headers["X-Cache"] == "HIT" || response.Headers["X-Cache"] == "MISS",
                "X-Cache header should be either HIT or MISS"
            );
        }

        [Fact]
        public async Task TestMultipleAddresses_IndependentCache()
        {
            // Arrange
            var function = new Function();
            var context = new TestLambdaContext();

            var address1 = "1600 Pennsylvania Avenue NW Washington DC 20500";
            var address2 = "Times Square New York NY 10036";

            // Act
            var request1 = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> { { "address", address1 } }
            };
            var response1 = await function.FunctionHandler(request1, context);

            var request2 = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> { { "address", address2 } }
            };
            var response2 = await function.FunctionHandler(request2, context);

            // Assert
            Assert.Equal(200, response1.StatusCode);
            Assert.Equal(200, response2.StatusCode);
            Assert.NotEqual(response1.Body, response2.Body);
        }

        [Fact]
        public async Task TestResponseValidJson()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", TestAddress } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(200, response.StatusCode);
            
            // Verify response body is valid JSON
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(response.Body);
                Assert.NotNull(json);
                Assert.True(json.RootElement.TryGetProperty("results", out _) || 
                           json.RootElement.TryGetProperty("status", out _),
                           "Google API response should contain 'results' or 'status' field");
            }
            catch (System.Text.Json.JsonException ex)
            {
                Assert.True(false, $"Response body is not valid JSON: {ex.Message}");
            }
        }

        [Fact]
        public async Task TestAddressWithSpecialCharacters()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", "Rue de l'École Polytechnique, Paris, France" } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task TestAddressWithNumbers()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", "123 Main Street" } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public void TestFunctionInitialization()
        {
            // Arrange & Act
            var function = new Function();

            // Assert
            Assert.NotNull(function);
        }

        [Fact]
        public async Task TestInvalidAddress_StillReturnsSuccess()
        {
            // Arrange
            var function = new Function();
            var request = new APIGatewayProxyRequest
            {
                QueryStringParameters = new Dictionary<string, string> 
                { 
                    { "address", "xyzabc123nonexistent12345" } 
                }
            };
            var context = new TestLambdaContext();

            // Act
            var response = await function.FunctionHandler(request, context);

            // Assert
            // Google API returns 200 with empty results for invalid addresses
            Assert.NotNull(response);
            Assert.Equal(200, response.StatusCode);
            Assert.Contains("status", response.Body.ToLower());
        }
    }
}
