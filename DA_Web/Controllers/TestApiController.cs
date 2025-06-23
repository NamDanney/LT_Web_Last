using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace DA_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestGeminiController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestGeminiController> _logger;
        private readonly HttpClient _httpClient;

        public TestGeminiController(
            IConfiguration configuration,
            ILogger<TestGeminiController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }
        [HttpGet("test-direct")]
        public async Task<IActionResult> TestDirect()
        {
            try
            {
                var apiKey = _configuration["Gemini:ApiKey"];

                // Test với gemini-1.5-flash
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Say hello in Vietnamese" }
                        }
                    }
                }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    statusCode = response.StatusCode,
                    isSuccess = response.IsSuccessStatusCode,
                    response = responseContent,
                    apiKeyLength = apiKey?.Length,
                    url = url.Replace(apiKey, "***HIDDEN***")
                });
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message });
            }
        }
        [HttpGet("test-basic")]
        public async Task<IActionResult> TestBasic()
        {
            try
            {
                var apiKey = _configuration["Gemini:ApiKey"];

                // Test 1: Kiểm tra API key
                if (string.IsNullOrEmpty(apiKey))
                {
                    return BadRequest(new { error = "API key not configured" });
                }

                // Test 2: Gọi API đơn giản nhất
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = "Say hello in Vietnamese" }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Calling Gemini API: {url.Substring(0, 50)}...");
                _logger.LogInformation($"Request: {json}");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Response Status: {response.StatusCode}");
                _logger.LogInformation($"Response Content: {responseContent}");

                return Ok(new
                {
                    apiKeyLength = apiKey.Length,
                    apiKeyPrefix = apiKey.Substring(0, Math.Min(10, apiKey.Length)),
                    statusCode = response.StatusCode.ToString(),
                    isSuccess = response.IsSuccessStatusCode,
                    response = responseContent,
                    headers = response.Headers.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Test error: {ex}");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("test-models")]
        public async Task<IActionResult> TestModels()
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var results = new List<object>();

            // Danh sách các model và endpoint để test
            var endpoints = new[]
            {
                ("gemini-pro", "v1beta"),
                ("gemini-pro", "v1"),
                ("gemini-1.5-flash", "v1beta"),
                ("gemini-1.5-flash-latest", "v1beta"),
                ("gemini-1.0-pro", "v1beta")
            };

            foreach (var (model, version) in endpoints)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/{version}/models/{model}:generateContent?key={apiKey}";

                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new[]
                                {
                                    new { text = "Hi" }
                                }
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    results.Add(new
                    {
                        model,
                        version,
                        success = response.IsSuccessStatusCode,
                        statusCode = response.StatusCode.ToString(),
                        response = responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        model,
                        version,
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return Ok(results);
        }

        [HttpGet("test-list-models")]
        public async Task<IActionResult> ListModels()
        {
            try
            {
                var apiKey = _configuration["Gemini:ApiKey"];
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";

                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    statusCode = response.StatusCode.ToString(),
                    isSuccess = response.IsSuccessStatusCode,
                    models = responseContent
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}