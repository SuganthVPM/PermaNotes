$ErrorActionPreference = "Stop"

Write-Host "Publishing PermaNotes for MSIX..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/msix_staging

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Copying AppxManifest.xml..." -ForegroundColor Cyan
Copy-Item ".\AppxManifest.xml" -Destination ".\dist\msix_staging\AppxManifest.xml" -Force

Write-Host "Generating required PNG assets for MSIX..." -ForegroundColor Cyan
$stagingAssets = ".\dist\msix_staging\Assets"
if (!(Test-Path $stagingAssets)) { New-Item -ItemType Directory -Force -Path $stagingAssets | Out-Null }

# Use pre-generated PNG assets committed to the repo
$assetSrc = ".\Assets"
Copy-Item "$assetSrc\Square150x150Logo.png" -Destination "$stagingAssets\Square150x150Logo.png" -Force
Copy-Item "$assetSrc\Square44x44Logo.png"  -Destination "$stagingAssets\Square44x44Logo.png"  -Force
Copy-Item "$assetSrc\StoreLogo.png"        -Destination "$stagingAssets\StoreLogo.png"        -Force

Write-Host "Creating MSIX package using makeappx.exe..." -ForegroundColor Cyan
$makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"

if (!(Test-Path $makeAppxPath)) {
    # Fallback to arm64 if x64 doesn't exist
    $makeAppxPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\arm64\makeappx.exe"
}

if (!(Test-Path $makeAppxPath)) {
    # Broad search just in case
    $makeAppxPath = (Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
}

if (!(Test-Path $makeAppxPath)) {
    Write-Host "makeappx.exe not found! Please install the Windows SDK." -ForegroundColor Red
    exit 1
}

$msixOut = ".\dist\PermaNotes_v1.5.0.msix"
& $makeAppxPath pack -d ".\dist\msix_staging" -p $msixOut -o

if ($LASTEXITCODE -ne 0) {
    Write-Host "makeappx packaging failed!" -ForegroundColor Red
    exit 1
}

Write-Host "MSIX package created successfully at $msixOut" -ForegroundColor Green
