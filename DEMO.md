# Circuit Breaker Demo Script

This PowerShell script demonstrates the difference between retry-only and circuit breaker patterns.

## Prerequisites
- The MinimalApiPolly application should be running on http://localhost:5117
- PowerShell 5.1 or later

## Demo Steps

### 1. Health Check
```powershell
Invoke-RestMethod -Uri "http://localhost:5117/health" -Method Get
```

### 2. Reset Fake Service Counter
```powershell
Invoke-RestMethod -Uri "http://localhost:5117/reset-fake-service" -Method Post
```

### 3. Test Normal Operation (Both Endpoints)
```powershell
# Test without circuit breaker
$body = @{ prompt = "What is machine learning?" } | ConvertTo-Json
$response1 = Invoke-RestMethod -Uri "http://localhost:5117/generate-text" -Method Post -Body $body -ContentType "application/json"
Write-Host "Without Circuit Breaker: $($response1.response)" -ForegroundColor Green

# Test with circuit breaker
$response2 = Invoke-RestMethod -Uri "http://localhost:5117/generate-text-resilient" -Method Post -Body $body -ContentType "application/json"
Write-Host "With Circuit Breaker: $($response2.response)" -ForegroundColor Green
```

### 4. Trigger Circuit Breaker by Making Multiple Failed Requests
```powershell
Write-Host "`nTesting Circuit Breaker Pattern..." -ForegroundColor Yellow
Write-Host "Making multiple requests to trigger failures..." -ForegroundColor Yellow

$body = @{ prompt = "Trigger circuit breaker test" } | ConvertTo-Json

# Make several requests to trigger the circuit breaker
for ($i = 1; $i -le 8; $i++) {
    try {
        Write-Host "Request $i..." -ForegroundColor Cyan
        $start = Get-Date
        
        $response = Invoke-RestMethod -Uri "http://localhost:5117/generate-text-resilient" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
        
        $end = Get-Date
        $duration = ($end - $start).TotalMilliseconds
        
        Write-Host "  ✅ Success in $([math]::Round($duration))ms: $($response.response)" -ForegroundColor Green
    }
    catch {
        $end = Get-Date
        $duration = ($end - $start).TotalMilliseconds
        
        if ($_.Exception.Response.StatusCode -eq 503) {
            Write-Host "  ❌ Service Unavailable in $([math]::Round($duration))ms" -ForegroundColor Red
        } else {
            Write-Host "  ⚡ Fast Fail in $([math]::Round($duration))ms (Circuit Breaker Open!)" -ForegroundColor Magenta
        }
    }
    
    Start-Sleep -Milliseconds 500
}
```

### 5. Compare Response Times
```powershell
Write-Host "`nComparing Response Times..." -ForegroundColor Yellow

# Test without circuit breaker (will retry multiple times on failures)
Write-Host "Testing WITHOUT Circuit Breaker (with retries):"
$start = Get-Date
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5117/generate-text" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    $end = Get-Date
    $duration = ($end - $start).TotalSeconds
    Write-Host "  Success in $([math]::Round($duration, 2)) seconds" -ForegroundColor Green
}
catch {
    $end = Get-Date
    $duration = ($end - $start).TotalSeconds
    Write-Host "  Failed after $([math]::Round($duration, 2)) seconds (multiple retries)" -ForegroundColor Red
}

# Test with circuit breaker (should fail fast if circuit is open)
Write-Host "Testing WITH Circuit Breaker:"
$start = Get-Date
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5117/generate-text-resilient" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    $end = Get-Date
    $duration = ($end - $start).TotalSeconds
    Write-Host "  Success in $([math]::Round($duration, 2)) seconds" -ForegroundColor Green
}
catch {
    $end = Get-Date
    $duration = ($end - $start).TotalSeconds
    Write-Host "  Fast fail in $([math]::Round($duration, 2)) seconds (circuit breaker)" -ForegroundColor Magenta
}
```

### 6. Wait for Circuit Breaker Recovery
```powershell
Write-Host "`nWaiting for circuit breaker to recover (30 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 35

Write-Host "Testing after recovery period:"
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5117/generate-text-resilient" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    Write-Host "  ✅ Circuit breaker recovered: $($response.response)" -ForegroundColor Green
}
catch {
    Write-Host "  ❌ Still failing - may need more time" -ForegroundColor Red
}
```

## Run the Complete Demo
```powershell
# Save this script as demo.ps1 and run:
# .\demo.ps1
```
