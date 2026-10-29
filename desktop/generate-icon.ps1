$ErrorActionPreference = 'Stop'
$assets = Split-Path -Parent $MyInvocation.MyCommand.Path
$iconPath = Join-Path $assets 'assets\icon.ico'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $iconPath) | Out-Null
Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = New-Object System.Drawing.Bitmap($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(6, 26, 49))

$background = New-Object System.Drawing.Drawing2D.GraphicsPath
$background.AddArc(8, 8, 48, 48, 180, 90)
$background.AddArc(200, 8, 48, 48, 270, 90)
$background.AddArc(200, 200, 48, 48, 0, 90)
$background.AddArc(8, 200, 48, 48, 90, 90)
$background.CloseFigure()
$graphics.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 179, 0))), $background)

$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 6, 26, 49), 18)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($pen, 78, 181, 172, 87)
$graphics.DrawArc($pen, 129, 35, 88, 88, 210, 235)
$graphics.DrawLine($pen, 174, 85, 211, 48)
$graphics.DrawLine($pen, 204, 51, 222, 69)
$graphics.DrawLine($pen, 192, 63, 210, 81)
$graphics.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 6, 26, 49))), 48, 160, 38, 38)

$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create($iconPath)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
Write-Host "Generated $iconPath"
