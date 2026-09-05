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

function Ensure-RemoteSupportHelper {
    $artifactDir = Join-Path $PSScriptRoot "artifacts\remote-support"
    $exe = Join-Path $artifactDir "QecRemoteSupportHelper.exe"
    $helperProject = Join-Path $PSScriptRoot "tools\Qec.Itmg.RemoteSupport.Helper\Program.cs"
    $publishScript = Join-Path $PSScriptRoot "scripts\publish-remote-support-helper.ps1"

    $needsPublish = $false
    if (-not (Test-Path $exe)) {
        $needsPublish = $true
        Write-Host "Remote Support Helper artifact missing — publishing..." -ForegroundColor Yellow
    }
    elseif ((Test-Path $helperProject) -and ((Get-Item $helperProject).LastWriteTimeUtc -gt (Get-Item $exe).LastWriteTimeUtc)) {
        $needsPublish = $true
        Write-Host "Remote Support Helper source is newer — republishing..." -ForegroundColor Yellow
    }

    if (-not $needsPublish) {
        Write-Host "Remote Support Helper artifact ready." -ForegroundColor DarkGray
        return
    }

    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $publishScript `
            -ApiBaseUrl "http://localhost:5080" `
            -AppBaseUrl "http://localhost:5173"
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
            throw "Publish finished without producing QecRemoteSupportHelper.exe"
        }
        Write-Host "Remote Support Helper published." -ForegroundColor Green
    }
    catch {
        Write-Host "WARNING: Could not publish Remote Support Helper. Download button may be unavailable." -ForegroundColor Yellow
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Stop-PortListener -Port 5080
Stop-PortListener -Port 5173
Start-Sleep -Milliseconds 500

Ensure-RemoteSupportHelper

if (-not (Test-Path "$PSScriptRoot\frontend\web\node_modules\vite")) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    npm install --prefix frontend/web
}

$artifactPath = (Join-Path $PSScriptRoot "artifacts\remote-support").Replace('\', '\\')

$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
`$env:RemoteSupport__HelperArtifactPath = '$artifactPath'
`$env:RemoteSupport__PublicAppBaseUrl = 'http://localhost:5173'
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
Write-Host "Google Sign-In: copy appsettings.Development.local.example.json -> appsettings.Development.local.json,"
Write-Host "  paste Client ID/Secret, set Enabled=true. Docs: docs\01-foundation\GOOGLE-OAUTH-LOCAL-DEVELOPMENT.md"
Write-Host "  (personal Gmail OK; Google Workspace not required)."
Write-Host "If Vite still fails, paste the full error from the UI PowerShell window."
