# VK Street Food - Background Server Launcher
# Run servers in background without visible terminals

$apiPath = "$PSScriptRoot\src\Server\VK.API"
$webPath = "$PSScriptRoot\src\Server\VK.Web"
$logPath = "$PSScriptRoot\logs"

# Create logs directory
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

Write-Host "🚀 Starting VK Street Food Servers in Background..." -ForegroundColor Cyan

# Check if servers already running
$apiRunning = Get-NetTCPConnection -LocalPort 5089 -ErrorAction SilentlyContinue
$webRunning = Get-NetTCPConnection -LocalPort 5117 -ErrorAction SilentlyContinue

if ($apiRunning) {
    Write-Host "⚠️  API Server already running on port 5089" -ForegroundColor Yellow
} else {
    # Start API Server
    $apiProcess = Start-Process -FilePath "dotnet" `
                                -ArgumentList "run --project $apiPath\VK.API.csproj" `
                                -WorkingDirectory $apiPath `
                                -WindowStyle Hidden `
                                -RedirectStandardOutput "$logPath\api-output.log" `
                                -RedirectStandardError "$logPath\api-error.log" `
                                -PassThru
    Write-Host "✅ API Server started (PID: $($apiProcess.Id)) - http://localhost:5089" -ForegroundColor Green
}

if ($webRunning) {
    Write-Host "⚠️  Web Portal already running on port 5117" -ForegroundColor Yellow
} else {
    # Wait a bit for API to initialize
    Start-Sleep -Seconds 2
    
    # Start Web Server
    $webProcess = Start-Process -FilePath "dotnet" `
                                -ArgumentList "run --project $webPath\VK.Web.csproj" `
                                -WorkingDirectory $webPath `
                                -WindowStyle Hidden `
                                -RedirectStandardOutput "$logPath\web-output.log" `
                                -RedirectStandardError "$logPath\web-error.log" `
                                -PassThru
    Write-Host "✅ Web Portal started (PID: $($webProcess.Id)) - http://localhost:5117" -ForegroundColor Green
}

Write-Host "`n⏳ Waiting for servers to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Test servers
Write-Host "`n🧪 Testing servers..." -ForegroundColor Cyan
try {
    $null = Invoke-WebRequest -Uri "http://localhost:5089/swagger" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
    Write-Host "   ✅ API Server: WORKING" -ForegroundColor Green
} catch {
    Write-Host "   ❌ API Server: Not responding yet (check logs/api-error.log)" -ForegroundColor Red
}

try {
    $null = Invoke-WebRequest -Uri "http://localhost:5117" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
    Write-Host "   ✅ Web Portal: WORKING" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Web Portal: Not responding yet (check logs/web-error.log)" -ForegroundColor Red
}

Write-Host "`n[SUCCESS] Servers running in background! URLs:" -ForegroundColor Cyan
Write-Host "   API: http://localhost:5089/swagger" -ForegroundColor White
Write-Host "   Web: http://localhost:5117" -ForegroundColor White
Write-Host "`nLogs saved to: $logPath" -ForegroundColor Gray
Write-Host "To stop: run .\stop-servers.ps1" -ForegroundColor Gray
