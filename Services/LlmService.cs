using MinimalApiPolly.Models;
using Polly;
using Polly.Extensions.Http;
using System.Text;
using System.Text.Json;

namespace MinimalApiPolly.Services;

public interface ILlmService
{
    Task<LlmSimpleResponse> GenerateTextAsync(string prompt);
    Task<LlmSimpleResponse> GenerateTextWithCircuitBreakerAsync(string prompt);
}

public class LlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _httpClientWithCircuitBreaker;
    private readonly ILogger<LlmService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public LlmService(
        IHttpClientFactory httpClientFactory, 
        ILogger<LlmService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("LlmClient");
        _httpClientWithCircuitBreaker = httpClientFactory.CreateClient("LlmClientWithCircuitBreaker");
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
    }

    public async Task<LlmSimpleResponse> GenerateTextAsync(string prompt)
    {
        try
        {
            _logger.LogInformation("Calling LLM API without circuit breaker for prompt: {Prompt}", prompt);
            
            // Simulate calling an LLM API (like OpenAI, Anthropic, etc.)
            var request = new LlmRequest(prompt);
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/completions", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                // For demo purposes, we'll simulate a response since we don't have a real LLM endpoint
                return new LlmSimpleResponse($"Generated response for: '{prompt}' - Success!", true);
            }
            else
            {
                _logger.LogWarning("LLM API returned error: {StatusCode}", response.StatusCode);
                return new LlmSimpleResponse(string.Empty, false, $"API Error: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling LLM API");
            return new LlmSimpleResponse(string.Empty, false, $"HTTP Error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling LLM API");
            return new LlmSimpleResponse(string.Empty, false, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling LLM API");
            return new LlmSimpleResponse(string.Empty, false, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<LlmSimpleResponse> GenerateTextWithCircuitBreakerAsync(string prompt)
    {
        try
        {
            _logger.LogInformation("Calling LLM API with circuit breaker for prompt: {Prompt}", prompt);
            
            var request = new LlmRequest(prompt);
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // This HttpClient has Polly circuit breaker configured
            var response = await _httpClientWithCircuitBreaker.PostAsync("/v1/completions", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return new LlmSimpleResponse($"Generated response for: '{prompt}' - Success with Circuit Breaker!", true);
            }
            else
            {
                _logger.LogWarning("LLM API returned error: {StatusCode}", response.StatusCode);
                return new LlmSimpleResponse(string.Empty, false, $"API Error: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling LLM API with circuit breaker");
            return new LlmSimpleResponse(string.Empty, false, $"HTTP Error: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling LLM API with circuit breaker");
            return new LlmSimpleResponse(string.Empty, false, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling LLM API with circuit breaker");
            return new LlmSimpleResponse(string.Empty, false, $"Unexpected error: {ex.Message}");
        }
    }
}
