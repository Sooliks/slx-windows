$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$frames = @()
foreach ($size in @(16,32,48,64,128,256)) {
 $bitmap = [Drawing.Bitmap]::new($size,$size)
 $graphics = [Drawing.Graphics]::FromImage($bitmap)
 $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#0f120c'))
 $graphics.ScaleTransform($size/64.0,$size/64.0)
 $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#e6f556'))
 foreach ($coords in @(@(12,10,24,10,38,31,31,42),@(42,10,54,10,39,30,32,21),@(34,43,41,33,54,54,41,54),@(25,35,31,46,25,54,12,54))) {
  $points = for ($i=0; $i -lt $coords.Count; $i+=2) { [Drawing.PointF]::new($coords[$i],$coords[$i+1]) }
  $graphics.FillPolygon($brush,[Drawing.PointF[]]$points)
 }
 $stream = [IO.MemoryStream]::new()
 $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
 $frames += ,$stream.ToArray()
 $stream.Dispose(); $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$out = [IO.File]::Create((Join-Path $root 'assets/slx.ico'))
$writer = [IO.BinaryWriter]::new($out)
try {
 $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
 $offset = 6 + 16 * $frames.Count
 $sizes = @(16,32,48,64,128,256)
 for ($i=0; $i -lt $frames.Count; $i++) {
  $writer.Write([byte]($sizes[$i] % 256)); $writer.Write([byte]($sizes[$i] % 256))
  $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
  $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
  $offset += $frames[$i].Length
 }
 foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $out.Dispose() }
