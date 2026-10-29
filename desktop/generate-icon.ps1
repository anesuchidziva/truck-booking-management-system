$ErrorActionPreference = 'Stop'
$assets = Split-Path -Parent $MyInvocation.MyCommand.Path
$iconPath = Join-Path $assets 'assets\icon.ico'
$logoPath = Join-Path $assets 'assets\amcotts-logo.png'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $iconPath) | Out-Null
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $logoPath)) {
    throw "The Amcotts logo source is missing: $logoPath"
}

$size = 256
$source = [System.Drawing.Bitmap]::new($logoPath)
$bitmap = New-Object System.Drawing.Bitmap($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(6, 26, 49))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.DrawImage(
    $source,
    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
    [System.Drawing.Rectangle]::new(8, 8, 48, 48),
    [System.Drawing.GraphicsUnit]::Pixel
)

$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create($iconPath)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$source.Dispose()
Write-Host "Generated $iconPath"
