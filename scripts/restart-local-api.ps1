# Restarts the local Development API on port 5080.
# Intended for the Vite login "Restart API" control (local Development only).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

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
Start-Sleep -Milliseconds 600

$artifactPath = (Join-Path $root "artifacts\remote-support").Replace('\', '\\')
$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
`$env:RemoteSupport__HelperArtifactPath = '$artifactPath'
`$env:RemoteSupport__PublicAppBaseUrl = 'http://localhost:5173'
Write-Host 'API  http://localhost:5080' -ForegroundColor Cyan
dotnet run --project src\Qec.Itmg.Host\Qec.Itmg.Host.csproj
"@

Start-Process powershell -WorkingDirectory $root -ArgumentList "-NoExit", "-Command", $apiCommand
Write-Host "API restart launched." -ForegroundColor Green
