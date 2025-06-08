using MinimalApiPolly.Extensions;
using MinimalApiPolly.Models;
using MinimalApiPolly.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register services
builder.Services.AddScoped<ILlmService, LlmService>();
builder.Services.AddScoped<FakeLlmService>();

// Configure HttpClient without any resilience policies (for comparison)
builder.Services.AddHttpClient("LlmClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000"); // Points to our fake LLM endpoint
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Configure HttpClient with Polly Circuit Breaker and other resilience patterns
builder.Services.AddHttpClient("LlmClientWithCircuitBreaker", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000"); // Points to our fake LLM endpoint
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy())
.AddPolicyHandler(GetTimeoutPolicy());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Fake LLM endpoint for testing (simulates external LLM service)
app.MapPost("/v1/completions", async (LlmRequest request, FakeLlmService fakeLlmService) =>
{
    return await fakeLlmService.HandleLlmRequest(request);
});

// Reset the fake service call count for testing
app.MapPost("/reset-fake-service", (FakeLlmService fakeLlmService) =>
{
    FakeLlmService.ResetCallCount();
    return Results.Ok(new { Message = "Fake service call count reset" });
});

// Main endpoint demonstrating MapPost with LLM call WITHOUT circuit breaker
app.MapPost("/generate-text", async (LlmSimpleRequest request, ILlmService llmService) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { Error = "Prompt is required" });
    }

    var result = await llmService.GenerateTextAsync(request.Prompt);
    
    if (result.Success)
    {
        return Results.Ok(result);
    }
    else
    {
        return Results.StatusCode(503); // Service Unavailable
    }
})
.WithName("GenerateText")
.WithOpenApi();

// Main endpoint demonstrating MapPost with LLM call WITH circuit breaker
app.MapPost("/generate-text-resilient", async (LlmSimpleRequest request, ILlmService llmService) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { Error = "Prompt is required" });
    }

    try
    {
        var result = await llmService.GenerateTextWithCircuitBreakerAsync(request.Prompt);
        
        if (result.Success)
        {
            return Results.Ok(result);
        }
        else
        {
            return Results.StatusCode(503); // Service Unavailable
        }
    }
    catch (Exception ex) when (ex.Message.Contains("circuit breaker"))
    {
        // Circuit breaker is open - return a fallback response
        return Results.Ok(new LlmSimpleResponse(
            "I'm temporarily unavailable due to high error rates. Please try again later.", 
            true, 
            "Circuit breaker fallback"));
    }
})
.WithName("GenerateTextResilient")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();

// Polly Policy Definitions
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // HttpRequestException and 5XX, 408 status codes
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.GetLogger();
                logger?.LogWarning("Retry {RetryCount} after {Delay}s due to: {Exception}", 
                    retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3, // Number of consecutive failures before opening circuit
            durationOfBreak: TimeSpan.FromSeconds(30), // How long circuit stays open
            onBreak: (exception, duration) =>
            {
                Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds}s due to: {exception.Exception?.Message ?? exception.Result?.StatusCode.ToString()}");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit breaker closed - service recovered");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("Circuit breaker half-open - testing service");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<HttpResponseMessage>(
        timeout: TimeSpan.FromSeconds(5), // Per-request timeout
        onTimeoutAsync: async (context, timespan, task) =>
        {
            var logger = context.GetLogger();
            logger?.LogWarning("Request timed out after {Timeout}s", timespan.TotalSeconds);
            await Task.CompletedTask;
        });
}
