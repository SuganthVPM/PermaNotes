$ErrorActionPreference = "Stop"

Write-Host "Publishing PermaNotes for MSIX..." -ForegroundColor Cyan
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/msix_staging

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Copying AppxManifest.xml..." -ForegroundColor Cyan
Copy-Item ".\AppxManifest.xml" -Destination ".\dist\msix_staging\AppxManifest.xml" -Force

Write-Host "Generating required PNG assets for MSIX..." -ForegroundColor Cyan
$stagingAssets = ".\dist\msix_staging\Assets"
if (!(Test-Path $stagingAssets)) { New-Item -ItemType Directory -Force -Path $stagingAssets | Out-Null }

Add-Type -AssemblyName System.Drawing

function Create-Image {
    param([int]$width, [int]$height, [string]$path)
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    
    try {
        $iconPath = "$PWD\Assets\icon.ico"
        $icon = New-Object System.Drawing.Icon($iconPath, $width, $height)
        $rect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
        $graphics.DrawIcon($icon, $rect)
    } catch {
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::CornflowerBlue)
        $rect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
        $graphics.FillRectangle($brush, $rect)
    }
    
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bmp.Dispose()
}

Create-Image 150 150 "$stagingAssets\Square150x150Logo.png"
Create-Image 44 44 "$stagingAssets\Square44x44Logo.png"
Create-Image 50 50 "$stagingAssets\StoreLogo.png"

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

$msixOut = ".\dist\PermaNotes_v1.4.2.msix"
& $makeAppxPath pack -d ".\dist\msix_staging" -p $msixOut -o

if ($LASTEXITCODE -ne 0) {
    Write-Host "makeappx packaging failed!" -ForegroundColor Red
    exit 1
}

Write-Host "MSIX package created successfully at $msixOut" -ForegroundColor Green
