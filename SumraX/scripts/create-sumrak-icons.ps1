# Build Sumrak VPN app icons from mascot source PNG
param(
    [string]$SourcePng = "",
    [string]$AppAssets = (Join-Path $PSScriptRoot "..\src\SumraX.App\Assets"),
    [string]$DesktopAssets = (Join-Path $PSScriptRoot "..\src\SumraX.App.Desktop\Assets")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $SourcePng) {
    $SourcePng = Join-Path $AppAssets "sumrak-mascot-source.png"
}

if (-not (Test-Path $SourcePng)) {
    throw "Source image not found: $SourcePng"
}

New-Item -ItemType Directory -Force -Path $AppAssets, $DesktopAssets | Out-Null

function Get-SquareCrop([System.Drawing.Bitmap]$bmp) {
    $side = [Math]::Min($bmp.Width, $bmp.Height)
    $x = [int](($bmp.Width - $side) / 2)
    $y = [int](($bmp.Height - $side) / 2)
    $y = [Math]::Max(0, $y - [int]($side * 0.04))
    if ($y + $side -gt $bmp.Height) { $y = $bmp.Height - $side }
    $rect = New-Object System.Drawing.Rectangle $x, $y, $side, $side
    return $bmp.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Resize-HighQuality([System.Drawing.Bitmap]$src, [int]$size) {
    $dest = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($dest)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $pad = [Math]::Max(1, [int]($size * 0.04))
    $g.DrawImage($src, $pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $g.Dispose()
    return $dest
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return $bytes
}

function Save-MultiIco([System.Drawing.Bitmap]$square, [string]$icoPath, [int[]]$sizes) {
    $payloads = New-Object System.Collections.Generic.List[byte[]]
    foreach ($s in $sizes) {
        $resized = Resize-HighQuality $square $s
        $payloads.Add((Get-PngBytes $resized)) | Out-Null
        $resized.Dispose()
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dim = 0
        if ($s -lt 256) { $dim = $s }
        $bw.Write([byte]$dim)
        $bw.Write([byte]$dim)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $len = $payloads[$i].Length
        $bw.Write([uint32]$len)
        $bw.Write([uint32]$offset)
        $offset += $len
    }

    foreach ($bytes in $payloads) {
        $bw.Write($bytes)
    }

    $bw.Flush()
    [IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
}

$srcBmp = New-Object System.Drawing.Bitmap $SourcePng
$square = Get-SquareCrop $srcBmp

$iconPath = Join-Path $AppAssets "sumrax-icon.png"
$logoPath = Join-Path $AppAssets "sumrax-logo.png"
$icoPath = Join-Path $AppAssets "sumrax.ico"
$sourceCopy = Join-Path $AppAssets "sumrak-mascot-source.png"

$icon32 = Resize-HighQuality $square 32
$icon48 = Resize-HighQuality $square 48
$icon64 = Resize-HighQuality $square 64
$icon256 = Resize-HighQuality $square 256
$icon512 = Resize-HighQuality $square 512
$icon32.Save((Join-Path $AppAssets "sumrax-icon-32.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$icon48.Save((Join-Path $AppAssets "sumrax-icon-48.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$icon64.Save((Join-Path $AppAssets "sumrax-icon-64.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$icon256.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)
$icon512.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
Save-MultiIco $square $icoPath @(16, 20, 24, 32, 48, 64, 128, 256)

if ((Resolve-Path $SourcePng).Path -ne (Resolve-Path $sourceCopy -ErrorAction SilentlyContinue).Path) {
    Copy-Item $SourcePng $sourceCopy -Force
}

Copy-Item $iconPath (Join-Path $DesktopAssets "sumrax-icon.png") -Force
Copy-Item $icoPath (Join-Path $DesktopAssets "sumrax.ico") -Force

$icon32.Dispose()
$icon48.Dispose()
$icon64.Dispose()
$icon256.Dispose()
$icon512.Dispose()
$square.Dispose()
$srcBmp.Dispose()

Write-Host "Created:" -ForegroundColor Green
Write-Host "  $iconPath"
Write-Host "  $logoPath"
Write-Host "  $icoPath"
Get-Item $iconPath, $logoPath, $icoPath | Format-Table Name, Length
