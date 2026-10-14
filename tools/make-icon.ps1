# Renders src\app.ico from the same geometry the app uses for its window icon,
# so the desktop shortcut, taskbar and title bar all match.

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$target = Join-Path $root 'src\app.ico'
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('sn-icon-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

$circle = [System.Drawing.Color]::FromArgb(24, 32, 46)
$glyph = [System.Drawing.Color]::White
$accent = [System.Drawing.Color]::FromArgb(214, 62, 53)

function New-IconPng {
    param([int]$Size, [string]$Path)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $bg = New-Object System.Drawing.SolidBrush $circle
        $graphics.FillEllipse($bg, 0, 0, $Size - 1, $Size - 1)
        $bg.Dispose()

        $pen = New-Object System.Drawing.Pen $glyph, ([Math]::Max(2.0, $Size / 12.0))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = New-Object 'System.Drawing.PointF[]' 4
        $points[0] = New-Object System.Drawing.PointF ([float]($Size * 0.20)), ([float]($Size * 0.62))
        $points[1] = New-Object System.Drawing.PointF ([float]($Size * 0.38)), ([float]($Size * 0.46))
        $points[2] = New-Object System.Drawing.PointF ([float]($Size * 0.55)), ([float]($Size * 0.56))
        $points[3] = New-Object System.Drawing.PointF ([float]($Size * 0.78)), ([float]($Size * 0.28))
        $graphics.DrawLines($pen, $points)
        $pen.Dispose()

        $dotBrush = New-Object System.Drawing.SolidBrush $accent
        $dot = [Math]::Max(4.0, $Size * 0.16)
        $graphics.FillEllipse($dotBrush, ([float]($Size * 0.70)), ([float]($Size * 0.20)), ([float]$dot), ([float]$dot))
        $dotBrush.Dispose()
    }
    finally {
        $graphics.Dispose()
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()

foreach ($size in $sizes) {
    $file = Join-Path $tempDir ("icon-" + $size + ".png")
    New-IconPng -Size $size -Path $file
    $images += , @{ Size = $size; Bytes = [System.IO.File]::ReadAllBytes($file) }
}

# Multi-image ICO: 6-byte header, one 16-byte directory entry per size,
# then the PNG payloads. Vista and later read PNG data inside .ico files.
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)

$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
    $dimension = [byte]$(if ($image.Size -ge 256) { 0 } else { $image.Size })
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$image.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $image.Bytes.Length
}

foreach ($image in $images) {
    $writer.Write($image.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($target, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()

Remove-Item $tempDir -Recurse -Force

Write-Host ("Wrote {0} ({1} bytes, {2} sizes)" -f $target, (Get-Item $target).Length, $images.Count)
