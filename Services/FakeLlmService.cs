using MinimalApiPolly.Models;
using System.Text.Json;

namespace MinimalApiPolly.Services;

/// <summary>
/// Simulates an unreliable LLM service for testing circuit breaker patterns
/// </summary>
public class FakeLlmService
{
    private static int _callCount = 0;
    private readonly ILogger<FakeLlmService> _logger;

    public FakeLlmService(ILogger<FakeLlmService> logger)
    {
        _logger = logger;
    }

    public async Task<IResult> HandleLlmRequest(LlmRequest request)
    {
        _callCount++;
        _logger.LogInformation("Fake LLM service called {CallCount} times", _callCount);

        // Simulate some processing time
        await Task.Delay(Random.Shared.Next(100, 500));

        // Simulate failures for demonstration
        // Fail every 3rd call to trigger circuit breaker
        if (_callCount % 3 == 0)
        {
            _logger.LogWarning("Simulating LLM service failure on call {CallCount}", _callCount);
            return Results.StatusCode(503); // Service Unavailable
        }

        // Simulate timeout occasionally
        if (_callCount % 7 == 0)
        {
            _logger.LogWarning("Simulating LLM service timeout on call {CallCount}", _callCount);
            await Task.Delay(30000); // Very long delay to simulate timeout
        }

        // Successful response
        var response = new LlmResponse(
            Id: $"completion-{Guid.NewGuid():N}",
            Object: "text_completion",
            Created: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model: "fake-llm-model",
            Choices: new List<Choice>
            {
                new Choice(
                    Text: $" This is a generated response for the prompt: '{request.Prompt}'. Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.",
                    Index: 0,
                    LogProbs: null,
                    FinishReason: "stop"
                )
            },
            Usage: new Usage(
                PromptTokens: request.Prompt.Split(' ').Length,
                CompletionTokens: 20,
                TotalTokens: request.Prompt.Split(' ').Length + 20
            )
        );

        return Results.Ok(response);
    }

    public static void ResetCallCount() => _callCount = 0;
}
