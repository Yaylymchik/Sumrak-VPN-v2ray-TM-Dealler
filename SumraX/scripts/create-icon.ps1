# Creates sumrax.ico from PNG for Windows ApplicationIcon
param(
    [string]$PngPath = (Join-Path $PSScriptRoot "..\src\SumraX.App\Assets\sumrax-icon.png"),
    [string]$IcoPath = (Join-Path $PSScriptRoot "..\src\SumraX.App\Assets\sumrax.ico")
)

Add-Type -AssemblyName System.Drawing

$bitmap = New-Object System.Drawing.Bitmap($PngPath)
$size = 256
$scaled = New-Object System.Drawing.Bitmap($bitmap, $size, $size)
$hIcon = $scaled.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::OpenWrite($IcoPath)
try {
    $icon.Save($fs)
}
finally {
    $fs.Close()
    $icon.Dispose()
    $scaled.Dispose()
    $bitmap.Dispose()
}

Write-Host "Created $IcoPath" -ForegroundColor Green
