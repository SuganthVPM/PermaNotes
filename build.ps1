$ErrorActionPreference = "Stop"

# Ensure local dotnet is found if not in default PATH
if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet") {
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
}

# Stop any running instances to unlock binaries
Get-Process | Where-Object { $_.ProcessName -like "*PermaNotes*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 1. Build and Publish the standalone executable
Write-Host "Publishing PermaNotes standalone portable executable..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Copy the published executable with versioned name for portable release
Copy-Item "./dist/PermaNotes.exe" -Destination "./dist/PermaNotes_v1.4.2.exe" -Force

# 2. Package the installer using Inno Setup
Write-Host "Packaging installer with Inno Setup..." -ForegroundColor Cyan
$isccCmd = "ISCC.exe"
if (!(Get-Command ISCC.exe -ErrorAction SilentlyContinue)) {
    if (Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") { $isccCmd = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" }
    elseif (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") { $isccCmd = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
}
& $isccCmd installer.iss

if ($LASTEXITCODE -ne 0) {
    Write-Host "Inno Setup packaging failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build and packaging complete! Your setup file is in the dist/ folder." -ForegroundColor Green
