# 1. Build and Publish the standalone executable
Write-Host "Publishing PermaNotes standalone executable..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# 2. Package the installer using Inno Setup
Write-Host "Packaging installer with Inno Setup..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss

if ($LASTEXITCODE -ne 0) {
    Write-Host "Inno Setup packaging failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build and packaging complete! Your setup file is in the dist/ folder." -ForegroundColor Green
