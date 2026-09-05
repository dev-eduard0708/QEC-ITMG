#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publishes a Windows x64 self-contained QEC Remote Support Helper (no binary committed to git).

.EXAMPLE
  ./scripts/publish-remote-support-helper.ps1
#>
param(
  [string]$OutputDir = "artifacts/remote-support",
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$project = "tools/Qec.Itmg.RemoteSupport.Helper/Qec.Itmg.RemoteSupport.Helper.csproj"
$publishDir = Join-Path $root $OutputDir

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $project `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  -o $publishDir

$published = Join-Path $publishDir "Qec.Itmg.RemoteSupport.Helper.exe"
$alias = Join-Path $publishDir "QecRemoteSupportHelper.exe"
if (Test-Path $published) {
  Copy-Item -Force $published $alias
}

Write-Host "Published helper to: $publishDir"
Write-Host "Configure RemoteSupport:HelperArtifactPath to this folder (or the EXE path)."
Write-Host "Do NOT commit artifacts/remote-support binaries."
