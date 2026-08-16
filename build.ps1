# 1. Build and Publish the standalone executable
Write-Host "Publishing PermaNotes standalone executable..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Rename the published executable to include version
if (Test-Path "./dist/PermaNotes_v1.4.2.exe") { Remove-Item "./dist/PermaNotes_v1.4.2.exe" -Force }
Rename-Item -Path "./dist/PermaNotes.exe" -NewName "PermaNotes_v1.4.2.exe"

# 2. Package the installer using Inno Setup
Write-Host "Packaging installer with Inno Setup..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss

if ($LASTEXITCODE -ne 0) {
    Write-Host "Inno Setup packaging failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build and packaging complete! Your setup file is in the dist/ folder." -ForegroundColor Green
