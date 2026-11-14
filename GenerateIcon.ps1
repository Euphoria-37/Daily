$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x+$w-$d), $y, $d, $d, 270, 90)
    $path.AddArc(($x+$w-$d), ($y+$h-$d), $d, $d, 0, 90)
    $path.AddArc($x, ($y+$h-$d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(($size/256.0), ($size/256.0))
    $green = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#3F7059'))
    $cream = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#FCFBF8'))
    $soft = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#D8E5D9'))
    $tile = RoundedPath 8 8 240 240 56
    $g.FillPath($green, $tile)
    $paper = RoundedPath 56 44 144 174 23
    $g.FillPath($cream, $paper)
    $g.FillEllipse($soft, 83, 75, 34, 34)
    $g.FillEllipse($green, 82, 129, 57, 57)
    $line = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#8BA894'), 8)
    $line.StartCap = 'Round'; $line.EndCap = 'Round'
    $g.DrawLine($line, 132, 92, 171, 92)
    $g.DrawLine($line, 153, 155, 172, 155)
    $check = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#FCFBF8'), 7)
    $check.StartCap = 'Round'; $check.EndCap = 'Round'; $check.LineJoin = 'Round'
    $points = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(96,157), [System.Drawing.PointF]::new(106,167), [System.Drawing.PointF]::new(125,147))
    $g.DrawLines($check, $points)
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ Size=$size; Bytes=$memory.ToArray() }
    $memory.Dispose(); $check.Dispose(); $line.Dispose(); $tile.Dispose(); $paper.Dispose()
    $green.Dispose(); $cream.Dispose(); $soft.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create((Join-Path $PSScriptRoot 'Daily.ico'))
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $images) { $writer.Write([byte[]]$entry.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'Generated Daily.ico (7 sizes)'
