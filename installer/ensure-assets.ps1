#Requires -Version 5.1
<#
.SYNOPSIS
    Ensures WiX build assets exist: MilOps.ico, banner.bmp (493x58), dialog.bmp (493x312).
    Generates them from scratch if missing so the MSI build is reproducible.
#>
$ErrorActionPreference = "Stop"
$assets = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$icoPath = Join-Path $assets "MilOps.ico"
$bannerPath = Join-Path $assets "banner.bmp"
$dialogPath = Join-Path $assets "dialog.bmp"

Add-Type -AssemblyName System.Drawing

function New-ShieldBitmap([int]$w, [int]$h, [bool]$darkHeader) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::White)

    # Background header band
    if ($darkHeader) {
        $band = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 31, 58, 95))  # 1F3A5F
        $g.FillRectangle($band, 0, 0, $w, [int]($h * 0.4))
        $band.Dispose()
    }

    # Shield motif (rounded polygon)
    $cx = [int]($w * 0.5); $cy = [int]($h * 0.55)
    $s = [int]([Math]::Min($w, $h) * 0.32)
    $points = @(
        (New-Object System.Drawing.PointF ($cx), ($cy - $s)),
        (New-Object System.Drawing.PointF ($cx + $s * 0.8), ($cy - $s * 0.6)),
        (New-Object System.Drawing.PointF ($cx + $s * 0.8), ($cy + $s * 0.2)),
        (New-Object System.Drawing.PointF ($cx), ($cy + $s)),
        (New-Object System.Drawing.PointF ($cx - $s * 0.8), ($cy + $s * 0.2)),
        (New-Object System.Drawing.PointF ($cx - $s * 0.8), ($cy - $s * 0.6))
    )
    $shieldColor = [System.Drawing.Color]::FromArgb(255, 46, 125, 50)  # 2E7D32 accent green
    $brush = New-Object System.Drawing.SolidBrush $shieldColor
    $g.FillPolygon($brush, $points)
    $brush.Dispose()
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 3
    $g.DrawPolygon($pen, $points)
    $pen.Dispose()

    $g.Dispose()
    return $bmp
}

# Icon: generate a 256x256 shield and wrap as ICO.
if (-not (Test-Path $icoPath)) {
    Write-Host "    Generating MilOps.ico..." -ForegroundColor DarkGray
    $icoBmp = New-ShieldBitmap 256 256 $true
    $ms = New-Object System.IO.MemoryStream
    $icoBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    # Write a minimal ICO container (1 image, PNG-encoded).
    $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([UInt16]0)          # reserved
    $bw.Write([UInt16]1)          # type = icon
    $bw.Write([UInt16]1)          # count
    $bw.Write([byte]0)            # width (0 = 256)
    $bw.Write([byte]0)            # height (0 = 256)
    $bw.Write([byte]0)            # colors
    $bw.Write([byte]0)            # reserved
    $bw.Write([UInt16]1)          # planes
    $bw.Write([UInt16]32)         # bpp
    $bw.Write([UInt32]$png.Length)
    $bw.Write([UInt32]22)         # offset
    $bw.Write($png)
    $bw.Close(); $fs.Close(); $ms.Close()
    $icoBmp.Dispose()
}

# Banner: 493x58
if (-not (Test-Path $bannerPath)) {
    Write-Host "    Generating banner.bmp..." -ForegroundColor DarkGray
    $b = New-ShieldBitmap 493 58 $true
    $b.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $b.Dispose()
}

# Dialog: 493x312
if (-not (Test-Path $dialogPath)) {
    Write-Host "    Generating dialog.bmp..." -ForegroundColor DarkGray
    $b = New-ShieldBitmap 493 312 $true
    $b.Save($dialogPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $b.Dispose()
}

Write-Host "    Assets ready in: $assets" -ForegroundColor DarkGray
exit 0
