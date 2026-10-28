<#
.SYNOPSIS
    Draws the Halyard logo and writes the app icon and PNGs.
.DESCRIPTION
    The logo is geometry, so every size is drawn crisp from the same shapes: a flat dark rounded
    square with one flat accent glyph, no gradients: an "H" whose crossbar is an upward chevron
    (the halyard hoists, the launcher launches).
#>
param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Add-Type -AssemblyName System.Drawing

# Flat colors only: one ground, one accent.
$Ground = [System.Drawing.Color]::FromArgb(255, 24, 25, 31)
$Accent = [System.Drawing.Color]::FromArgb(255, 124, 134, 255)

function New-RoundedSquare([float]$Size, [float]$Radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($Size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($Size - $d, $Size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $Size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-AccentPen([float]$Width) {
    $pen = New-Object System.Drawing.Pen($Accent, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-Glyph($g) {
    # H monogram: two stems, crossbar raised into a chevron.
    $pen = New-AccentPen 26
    $g.DrawLine($pen, 82, 64, 82, 192)
    $g.DrawLine($pen, 174, 64, 174, 192)
    [System.Drawing.PointF[]]$chevron = @(
        (New-Object System.Drawing.PointF 82, 148),
        (New-Object System.Drawing.PointF 128, 108),
        (New-Object System.Drawing.PointF 174, 148))
    $g.DrawLines($pen, $chevron)
}

function New-LogoBitmap([int]$Size) {
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Designed on a 256 grid.
    $g.ScaleTransform($Size / 256.0, $Size / 256.0)

    # Flat dark ground with a hairline edge (the edge keeps it visible on dark taskbars).
    $g.FillPath((New-Object System.Drawing.SolidBrush $Ground), (New-RoundedSquare 256 58))

    if ($Size -ge 32) {
        $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(36, 255, 255, 255)), 2
        $g.DrawPath($edge, (New-RoundedSquare 255 58))
    }

    Draw-Glyph $g
    $g.Dispose()
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$Bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$images = foreach ($size in $sizes) { , (Get-PngBytes (New-LogoBitmap $size)) }

$icon = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $icon
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

$iconPath = Join-Path $Root 'src\Halyard\Assets\AppIcon.ico'
[System.IO.File]::WriteAllBytes($iconPath, $icon.ToArray())

$pngPath = Join-Path $Root 'assets\logo.png'
New-Item -ItemType Directory -Force (Split-Path $pngPath) | Out-Null
[System.IO.File]::WriteAllBytes($pngPath, (Get-PngBytes (New-LogoBitmap 512)))

"Wrote $iconPath and $pngPath"
