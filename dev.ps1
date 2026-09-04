$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Stop-PortListener([int]$Port) {
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        $processId = $connection.OwningProcess
        if ($processId -and $processId -ne 0) {
            Write-Host "Stopping process $processId on port $Port..." -ForegroundColor Yellow
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

Stop-PortListener -Port 5080
Stop-PortListener -Port 5173
Start-Sleep -Milliseconds 500

if (-not (Test-Path "$PSScriptRoot\frontend\web\node_modules\vite")) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    npm install --prefix frontend/web
}

$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'API  http://localhost:5080' -ForegroundColor Cyan
dotnet run --project src\Qec.Itmg.Host\Qec.Itmg.Host.csproj
"@

$uiCommand = @"
Write-Host 'UI   http://localhost:5173/login' -ForegroundColor Cyan
npm run dev --prefix frontend/web
"@

Start-Process powershell -WorkingDirectory $PSScriptRoot -ArgumentList "-NoExit", "-Command", $apiCommand
Start-Process powershell -WorkingDirectory $PSScriptRoot -ArgumentList "-NoExit", "-Command", $uiCommand

Write-Host "Started API and UI. Open http://localhost:5173/login"
Write-Host "If Vite still fails, paste the full error from the UI PowerShell window."
