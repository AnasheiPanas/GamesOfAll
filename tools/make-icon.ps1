# Genera assets\gamesofall.ico (multi-resolucion, frames PNG) sin dependencias externas.
param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) "assets\gamesofall.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()

foreach ($size in $sizes) {
    $s = [double]$size
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = $s * 0.03
    $side = $s - (2.0 * $pad)
    $radius = $s * 0.23
    $d = $radius * 2.0

    $x = [float]$pad
    $y = [float]$pad
    $w = [float]$side
    $dd = [float]$d

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($x, $y, $dd, $dd, 180, 90)
    $path.AddArc($x + $w - $dd, $y, $dd, $dd, 270, 90)
    $path.AddArc($x + $w - $dd, $y + $w - $dd, $dd, $dd, 0, 90)
    $path.AddArc($x, $y + $w - $dd, $dd, $dd, 90, 90)
    $path.CloseFigure()

    $rect = [System.Drawing.RectangleF]::new($x, $y, $w, $w)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 88, 196, 255),
        [System.Drawing.Color]::FromArgb(255, 164, 112, 255),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($brush, $path)

    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $tri = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($s * 0.39), [float]($s * 0.28)),
        [System.Drawing.PointF]::new([float]($s * 0.39), [float]($s * 0.72)),
        [System.Drawing.PointF]::new([float]($s * 0.73), [float]($s * 0.50))
    )
    $g.FillPolygon($white, $tri)

    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()

    $white.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    $bmp.Dispose()
    $ms.Dispose()
}

$dir = Split-Path -Parent $Output
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$fs = [System.IO.File]::Create($Output)
$bw = [System.IO.BinaryWriter]::new($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Icono generado: $Output"
