# VK Street Food - Stop Background Servers

Write-Host "🛑 Stopping VK Street Food Servers..." -ForegroundColor Cyan

# Stop API Server (port 5089)
$apiProcess = Get-NetTCPConnection -LocalPort 5089 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($apiProcess) {
    Stop-Process -Id $apiProcess -Force
    Write-Host "✅ API Server stopped (PID: $apiProcess)" -ForegroundColor Green
} else {
    Write-Host "⚠️  No API Server running on port 5089" -ForegroundColor Yellow
}

# Stop Web Server (port 5117)
$webProcess = Get-NetTCPConnection -LocalPort 5117 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($webProcess) {
    Stop-Process -Id $webProcess -Force
    Write-Host "✅ Web Portal stopped (PID: $webProcess)" -ForegroundColor Green
} else {
    Write-Host "⚠️  No Web Portal running on port 5117" -ForegroundColor Yellow
}

Write-Host "`n✨ All servers stopped" -ForegroundColor Cyan
