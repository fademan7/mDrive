# Deterministic vector geometry matching assets/mdrive.svg; no downloaded art.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$bitmap=New-Object System.Drawing.Bitmap 256,256
$g=[System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#101820'))
$rim=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#45e0c2')),18
$spoke=New-Object System.Drawing.Pen ($rim.Color),16
$spoke.StartCap=$spoke.EndCap=[System.Drawing.Drawing2D.LineCap]::Round
$ink=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#101820')),6
$hub=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#eef9ff'))
try {
    $g.DrawEllipse($rim,44,44,168,168)
    $g.DrawLine($spoke,48,112,103,128); $g.DrawLine($spoke,208,112,153,128); $g.DrawLine($spoke,128,151,128,210)
    $g.FillEllipse($hub,101,101,54,54)
    $points=[System.Drawing.Point[]]@([System.Drawing.Point]::new(111,136),[System.Drawing.Point]::new(111,120),[System.Drawing.Point]::new(128,131),[System.Drawing.Point]::new(145,120),[System.Drawing.Point]::new(145,136))
    $g.DrawLines($ink,$points)
    $png=New-Object System.IO.MemoryStream
    $bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
    $bytes=$png.ToArray()
    $ico=New-Object System.IO.MemoryStream
    $writer=New-Object System.IO.BinaryWriter $ico
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1)
    $writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$bytes.Length);$writer.Write([uint32]22);$writer.Write($bytes)
    [System.IO.File]::WriteAllBytes((Join-Path $root 'assets\mdrive.ico'),$ico.ToArray())
    $writer.Dispose();$ico.Dispose();$png.Dispose()
} finally {$hub.Dispose();$ink.Dispose();$spoke.Dispose();$rim.Dispose();$g.Dispose();$bitmap.Dispose()}
