# Circuit Breaker Demo Script for MinimalApiPolly
# Demonstrates the difference between retry-only and circuit breaker patterns

param(
    [string]$BaseUrl = "http://localhost:5117"
)

Write-Host "=== Circuit Breaker vs Retry-Only Demo ===" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray
Write-Host ""

# Function to measure and display request timing
function Invoke-TimedRequest {
    param(
        [string]$Uri,
        [string]$Method = "Get",
        [string]$Body = $null,
        [string]$ContentType = "application/json",
        [string]$Description = ""
    )
    
    $start = Get-Date
    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -Body $Body -ContentType $ContentType -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -ErrorAction Stop
        }
        
        $end = Get-Date
        $duration = ($end - $start).TotalMilliseconds
        
        Write-Host "  ✅ $Description Success in $([math]::Round($duration))ms" -ForegroundColor Green
        if ($response.response) {
            Write-Host "     Response: $($response.response)" -ForegroundColor Gray
        }
        return $response
    }
    catch {
        $end = Get-Date
        $duration = ($end - $start).TotalMilliseconds
        
        $statusCode = $_.Exception.Response.StatusCode
        if ($statusCode -eq 503) {
            Write-Host "  ❌ $Description Service Unavailable in $([math]::Round($duration))ms" -ForegroundColor Red
        } elseif ($duration -lt 100) {
            Write-Host "  ⚡ $Description Fast Fail in $([math]::Round($duration))ms (Circuit Breaker!)" -ForegroundColor Magenta
        } else {
            Write-Host "  ❌ $Description Failed in $([math]::Round($duration))ms" -ForegroundColor Red
        }
        return $null
    }
}

# Step 1: Health Check
Write-Host "1. Health Check" -ForegroundColor Cyan
Invoke-TimedRequest -Uri "$BaseUrl/health" -Description "Health check"
Write-Host ""

# Step 2: Reset Counter
Write-Host "2. Resetting Fake Service Counter" -ForegroundColor Cyan
Invoke-TimedRequest -Uri "$BaseUrl/reset-fake-service" -Method Post -Description "Reset counter"
Write-Host ""

# Step 3: Test Normal Operation
Write-Host "3. Testing Normal Operation" -ForegroundColor Cyan
$testBody = @{ prompt = "What is artificial intelligence?" } | ConvertTo-Json

Invoke-TimedRequest -Uri "$BaseUrl/generate-text" -Method Post -Body $testBody -Description "Without Circuit Breaker"
Invoke-TimedRequest -Uri "$BaseUrl/generate-text-resilient" -Method Post -Body $testBody -Description "With Circuit Breaker"
Write-Host ""

# Step 4: Trigger Circuit Breaker
Write-Host "4. Triggering Circuit Breaker (Making Multiple Requests)" -ForegroundColor Cyan
$triggerBody = @{ prompt = "Trigger circuit breaker test" } | ConvertTo-Json

Write-Host "Making 8 requests to trigger circuit breaker..." -ForegroundColor Gray
for ($i = 1; $i -le 8; $i++) {
    Write-Host "Request ${i}:" -NoNewline -ForegroundColor Gray
    Invoke-TimedRequest -Uri "$BaseUrl/generate-text-resilient" -Method Post -Body $triggerBody -Description ""
    Start-Sleep -Milliseconds 200
}
Write-Host ""

# Step 5: Compare Response Times
Write-Host "5. Comparing Response Times When Service is Failing" -ForegroundColor Cyan

Write-Host "Testing WITHOUT Circuit Breaker (will retry on failures):"
Invoke-TimedRequest -Uri "$BaseUrl/generate-text" -Method Post -Body $triggerBody -Description "Retry-only approach"

Write-Host "Testing WITH Circuit Breaker (should fail fast):"
Invoke-TimedRequest -Uri "$BaseUrl/generate-text-resilient" -Method Post -Body $triggerBody -Description "Circuit breaker approach"
Write-Host ""

# Step 6: Show Circuit Recovery
Write-Host "6. Circuit Breaker Recovery Test" -ForegroundColor Cyan
Write-Host "The circuit breaker opens for 30 seconds after failures." -ForegroundColor Gray
Write-Host "You can wait and run this script again to see recovery, or test manually." -ForegroundColor Gray
Write-Host ""

Write-Host "=== Demo Complete ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "Key Observations:" -ForegroundColor Yellow
Write-Host "• Circuit breaker fails fast (< 100ms) when open" -ForegroundColor White
Write-Host "• Retry-only approach takes much longer to fail" -ForegroundColor White
Write-Host "• Circuit breaker prevents resource exhaustion" -ForegroundColor White
Write-Host "• Both approaches succeed when service is healthy" -ForegroundColor White
Write-Host ""
Write-Host "Try running the script again in 35 seconds to see circuit recovery!" -ForegroundColor Green
