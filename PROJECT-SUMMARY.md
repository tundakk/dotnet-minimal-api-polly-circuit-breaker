# .NET 8 Minimal API with Polly Circuit Breaker Demo

## Project Overview

This solution demonstrates how to integrate **Polly Circuit Breaker** into a .NET 8 Minimal API for resilient LLM endpoint calls. It shows the practical difference between retry-only approaches and circuit breaker patterns.

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Client        │    │   Minimal API    │    │   LLM Service   │
│                 │───▶│                  │───▶│   (Simulated)   │
│  POST /generate │    │  Circuit Breaker │    │   Failures      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 🚀 Key Features

### Circuit Breaker Configuration
- **Failure Threshold**: 3 consecutive failures
- **Circuit Open Duration**: 30 seconds  
- **Timeout Policy**: 5 seconds per request
- **Retry Policy**: 3 attempts with exponential backoff

### Endpoints
- `POST /generate-text` - **Without** circuit breaker (retry only)
- `POST /generate-text-resilient` - **With** circuit breaker + retry
- `POST /v1/completions` - Simulated LLM endpoint (fails every 3rd call)
- `GET /health` - Health check
- `POST /reset-fake-service` - Reset failure simulation

## 🎯 Why Circuit Breaker > Retry Alone for LLM APIs?

| Aspect | Retry Only | Circuit Breaker + Retry |
|--------|------------|------------------------|
| **Failure Response Time** | 4+ seconds | 10ms (when open) |
| **Resource Usage** | High (threads blocked) | Low (fast fail) |
| **Cascading Failures** | Yes (hammers failing service) | No (prevents thundering herd) |
| **User Experience** | Poor (long waits) | Good (immediate feedback) |
| **Cost Impact** | High (continued LLM charges) | Low (no wasted calls) |

## 📋 Demo Results

```
=== Circuit Breaker vs Retry-Only Demo ===

3. Testing Normal Operation
  ✅ Without Circuit Breaker Success in 253ms
  ✅ With Circuit Breaker Success in 248ms

4. Triggering Circuit Breaker (Multiple Failed Requests)
Request 1:  ❌ Service Unavailable in 10018ms  ← Initial failures
Request 2:  ❌ Service Unavailable in 6112ms   ← Retry attempts
Request 3:  ❌ Service Unavailable in 8ms      ← Circuit opens!
Request 4:  ❌ Service Unavailable in 5ms      ← Fast fail
Request 5:  ❌ Service Unavailable in 0ms      ← Fast fail
...

5. Comparing Response Times When Service is Failing
Without Circuit Breaker: ❌ Failed in 4056ms  ← Multiple retries
With Circuit Breaker:    ❌ Failed in 10ms    ← Fast fail!
```

## 🛠️ Running the Demo

### 1. Start the Application
```powershell
cd "c:\Repositories\minimal-api\MinimalApiPolly"
dotnet run
```

### 2. Run the Demo Script
```powershell
.\demo.ps1
```

### 3. Manual Testing
Use the `api-tests.http` file or curl:

```bash
# Reset counter
curl -X POST http://localhost:5117/reset-fake-service

# Test resilient endpoint
curl -X POST http://localhost:5117/generate-text-resilient \
  -H "Content-Type: application/json" \
  -d '{"prompt": "What is AI?"}'
```

## 📁 Project Structure

```
MinimalApiPolly/
├── Models/
│   ├── LlmRequest.cs           # LLM API request models
│   ├── LlmResponse.cs          # LLM API response models  
│   └── LlmSimpleModels.cs      # Simplified models
├── Services/
│   ├── LlmService.cs           # Main service with/without circuit breaker
│   └── FakeLlmService.cs       # Simulated unreliable LLM service
├── Extensions/
│   └── PollyContextExtensions.cs  # Polly context helpers
├── Program.cs                  # Main application setup
├── demo.ps1                    # PowerShell demo script
├── api-tests.http             # HTTP test file
└── README.md                  # Why circuit breaker > retry alone
```

## 🔧 Key Code Snippets

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

## 🎓 Learning Outcomes

1. **Circuit breakers prevent cascading failures** in distributed systems
2. **Fast failure is better than slow failure** for user experience  
3. **Polly provides powerful resilience patterns** beyond simple retry
4. **LLM APIs need special consideration** due to cost and latency
5. **Minimal APIs can easily integrate** sophisticated resilience patterns

## 🔄 Next Steps

- Add fallback responses from cached data
- Implement bulkhead pattern for resource isolation
- Add metrics and monitoring with Application Insights
- Configure different policies per LLM provider
- Add rate limiting to prevent API quota exhaustion

---

**🚀 This demo shows why Polly Circuit Breaker is essential for production LLM integrations in .NET applications!**
