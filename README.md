# .NET 8 Minimal API with Polly Circuit Breaker Demo

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **🎯 A complete demonstration of integrating Polly Circuit Breaker into .NET 8 Minimal API for resilient LLM endpoint calls**

This project shows **why circuit breakers are essential** for production LLM integrations and **how they outperform retry-only approaches** in preventing cascading failures.

## 🚀 Quick Start

```bash
# Clone the repository
git clone https://github.com/tundakk/dotnet-minimal-api-polly-circuit-breaker.git
cd dotnet-minimal-api-polly-circuit-breaker

# Run the application
dotnet run

# Run the demo script (PowerShell)
.\demo.ps1
```

## 🎬 Live Demo Results

```
=== Circuit Breaker vs Retry-Only Demo ===

5. Comparing Response Times When Service is Failing
Without Circuit Breaker: ❌ Failed in 4056ms  ← Multiple retries
With Circuit Breaker:    ❌ Failed in 10ms    ← Fast fail!

Key Observations:
• Circuit breaker fails fast (< 100ms) when open
• Retry-only approach takes much longer to fail  
• Circuit breaker prevents resource exhaustion
• Both approaches succeed when service is healthy
```

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Client        │    │   Minimal API    │    │   LLM Service   │
│                 │───▶│                  │───▶│   (Simulated)   │
│  POST /generate │    │  Circuit Breaker │    │   Failures      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 📊 Why Circuit Breaker > Retry Alone for LLM APIs?

| Aspect | Retry Only | Circuit Breaker + Retry |
|--------|------------|------------------------|
| **Failure Response Time** | 4+ seconds | 10ms (when open) |
| **Resource Usage** | High (threads blocked) | Low (fast fail) |
| **Cascading Failures** | Yes (hammers failing service) | No (prevents thundering herd) |
| **User Experience** | Poor (long waits) | Good (immediate feedback) |
| **Cost Impact** | High (continued LLM charges) | Low (no wasted calls) |

## The Problem with Retry-Only Approaches

### 1. **Cascading Failures & Resource Exhaustion**
```
❌ With Only Retry:
Request → Fail → Retry → Fail → Retry → Fail → Retry → Fail
   ↓        ↓       ↓       ↓       ↓       ↓       ↓
 Thread   Thread  Thread  Thread  Thread  Thread  Thread
 Blocked  Blocked Blocked Blocked Blocked Blocked Blocked
```

When an external service is down, retry-only approaches keep hammering the failing service, consuming:
- **Thread pool threads** (blocked waiting for timeouts)
- **Memory** (holding request/response objects)
- **Network resources** (continued failed requests)
- **Time** (users waiting for multiple retry attempts)

### 2. **Thundering Herd Effect**
Multiple instances of your API all retrying simultaneously can overwhelm a recovering service, preventing it from coming back online.

## How Circuit Breaker Solves These Issues

### 1. **Fast Failure & Resource Conservation**
```
✅ With Circuit Breaker:
Request → Fail → Fail → Fail → [CIRCUIT OPENS] → Fast Fail → Fast Fail
   ↓        ↓       ↓              ↓                 ↓          ↓
 Thread   Thread  Thread      Immediate          Immediate  Immediate
 Blocked  Blocked Blocked     Response           Response   Response
```

After a threshold of failures, the circuit breaker **opens** and immediately returns errors without making network calls.

### 2. **Automatic Recovery Detection**
The circuit breaker periodically allows a single request through (half-open state) to test if the service has recovered.

### 3. **Graceful Degradation**
You can provide fallback responses when the circuit is open, maintaining user experience.

## LLM-Specific Considerations

### **High Cost of Failed LLM Calls**
- LLM APIs often charge per token, even for failed requests
- Long timeouts (30-60 seconds) are common
- Rate limiting is strict and varies by provider

### **Unpredictable Failure Patterns**
- LLM services can have:
  - Sudden capacity issues
  - Model loading delays
  - Rate limit exhaustion
  - Regional outages

### **User Experience Impact**
- Users expect fast responses
- Multiple 30-second timeouts create terrible UX
- Circuit breaker enables instant fallback responses

## 🔧 Implementation Details

### MapPost with Circuit Breaker
```csharp
app.MapPost("/generate-text-resilient", async (LlmSimpleRequest request, ILlmService llmService) =>
{
    try
    {
        var result = await llmService.GenerateTextWithCircuitBreakerAsync(request.Prompt);
        return result.Success ? Results.Ok(result) : Results.StatusCode(503);
    }
    catch (Exception ex) when (ex.Message.Contains("circuit breaker"))
    {
        // Circuit breaker fallback
        return Results.Ok(new LlmSimpleResponse(
            "I'm temporarily unavailable. Please try again later.", 
            true, 
            "Circuit breaker fallback"));
    }
});
```

### Polly Configuration
```csharp
builder.Services.AddHttpClient("LlmClientWithCircuitBreaker", client =>
{
    client.BaseAddress = new Uri("http://localhost:5117");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(GetRetryPolicy())         // 3 retries with backoff
.AddPolicyHandler(GetCircuitBreakerPolicy()) // Circuit breaker
.AddPolicyHandler(GetTimeoutPolicy());      // 5s timeout
```

### Circuit Breaker Policy
```csharp
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,    // 3 failures opens circuit
            durationOfBreak: TimeSpan.FromSeconds(30), // 30s recovery window
            onBreak: (exception, duration) => {
                Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds}s");
            },
            onReset: () => Console.WriteLine("Circuit breaker closed - service recovered"),
            onHalfOpen: () => Console.WriteLine("Circuit breaker half-open - testing service")
        );
}
```

## 🎮 Testing the Demo

### API Endpoints

| Endpoint | Purpose | Resilience Pattern |
|----------|---------|-------------------|
| `POST /generate-text` | Basic LLM call | **Retry only** |
| `POST /generate-text-resilient` | Resilient LLM call | **Circuit breaker + retry** |
| `POST /v1/completions` | Simulated LLM service | Fails every 3rd call |
| `GET /health` | Health check | None |
| `POST /reset-fake-service` | Reset failure counter | None |

### PowerShell Demo Script
```powershell
# Run the comprehensive demo
.\demo.ps1

# Or test manually
Invoke-RestMethod -Uri "http://localhost:5117/generate-text-resilient" `
  -Method Post -Body '{"prompt":"What is AI?"}' -ContentType "application/json"
```

### HTTP Test File
Use the included `api-tests.http` file with VS Code REST Client extension.

## 📁 Project Structure

```
MinimalApiPolly/
├── Models/                     # Request/Response models
│   ├── LlmRequest.cs          
│   ├── LlmResponse.cs         
│   └── LlmSimpleModels.cs     
├── Services/                   # Business logic
│   ├── LlmService.cs          # Main service with/without circuit breaker
│   └── FakeLlmService.cs      # Simulated unreliable LLM service
├── Extensions/                 # Helper extensions
│   └── PollyContextExtensions.cs
├── Program.cs                  # Application configuration
├── demo.ps1                    # PowerShell demo script
├── api-tests.http             # HTTP test requests
└── README.md                  # This file
```

## Real-World Example

```csharp
// ❌ BAD: Retry only - can take 90+ seconds to fail
app.MapPost("/generate", async (string prompt, HttpClient client) =>
{
    // If LLM service is down:
    // Attempt 1: 30s timeout
    // Attempt 2: 30s timeout  
    // Attempt 3: 30s timeout
    // Total: 90+ seconds of user waiting
    
    var response = await client.PostAsync("/llm", content);
    // ... handle response
});

// ✅ GOOD: Circuit breaker - fails fast after initial detection
app.MapPost("/generate", async (string prompt, ILlmService llmService) =>
{
    try 
    {
        return await llmService.GenerateWithCircuitBreaker(prompt);
    }
    catch (CircuitBreakerOpenException)
    {
        // Instant response - no waiting!
        return "I'm temporarily unavailable. Please try again in a moment.";
    }
});
```

## When to Use Each Pattern

| Pattern | Use When | Don't Use When |
|---------|----------|----------------|
| **Retry Only** | Transient network issues, occasional 5xx errors | Service is fundamentally down, high-cost operations |
| **Circuit Breaker** | External service failures, expensive operations, user-facing APIs | Internal service calls, database connections |
| **Both (Recommended)** | Production LLM integrations, critical external APIs | Simple internal HTTP calls |

## 🎓 Learning Outcomes

1. **Circuit breakers prevent cascading failures** in distributed systems
2. **Fast failure is better than slow failure** for user experience  
3. **Polly provides powerful resilience patterns** beyond simple retry
4. **LLM APIs need special consideration** due to cost and latency
5. **Minimal APIs can easily integrate** sophisticated resilience patterns

## 🔄 Next Steps

- [ ] Add fallback responses from cached data
- [ ] Implement bulkhead pattern for resource isolation
- [ ] Add metrics and monitoring with Application Insights
- [ ] Configure different policies per LLM provider
- [ ] Add rate limiting to prevent API quota exhaustion

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

**🚀 This demo shows why Polly Circuit Breaker is essential for production LLM integrations in .NET applications!**
