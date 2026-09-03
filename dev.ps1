$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

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
