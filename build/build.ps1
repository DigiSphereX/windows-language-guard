# LanguageGuard - build script (portable, needs only the .NET Framework that ships with Windows).
# Produces LanguageGuard.exe (single file, zero dependencies) + build\LanguageGuard.ico.

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Src = Join-Path $Root 'src\LanguageGuard.cs'
$Out = Join-Path $Root 'LanguageGuard.exe'
$Ico = Join-Path $Root 'build\LanguageGuard.ico'

# ---------------------------------------------------------------- icon generation
function New-LanguageGuardIcon {
    Add-Type -AssemblyName System.Drawing
    $sizes = 16, 24, 32, 48, 64, 128, 256
    $pngs = @()
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        # rounded blue backdrop
        $r = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
        $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList ($r, [System.Drawing.Color]::FromArgb(30, 120, 228), [System.Drawing.Color]::FromArgb(15, 70, 150), 45)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = [Math]::Max(2, [int]($size * 0.22))
        $path.AddArc(0, 0, $d, $d, 180, 90)
        $path.AddArc($size - $d, 0, $d, $d, 270, 90)
        $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
        $path.AddArc(0, $size - $d, $d, $d, 90, 90)
        $path.CloseFigure()
        $g.FillPath($grad, $path)
        # white 'A' glyph (language) - simple stroke using the letter A + a scribe-like underline
        $fontSize = $size * 0.62
        $font = New-Object System.Drawing.Font -ArgumentList ('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rcf = New-Object System.Drawing.RectangleF -ArgumentList (0, (-$size * 0.06), $size, $size)
        $g.DrawString('A', $font, [System.Drawing.Brushes]::White, $rcf, $sf)
        $pen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::White, [Math]::Max(1, [int]($size * 0.06)))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $y = $size * 0.74
        $g.DrawLine($pen, $size * 0.22, $y, $size * 0.78, $y)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += , $ms.ToArray()
        $bmp.Dispose()
    }
    # write a multi-image ICO (Vista+ PNG entries)
    $fs = [System.IO.File]::Create($Ico)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
    $offset = 6 + 16 * $pngs.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $w = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$w); $bw.Write([byte]$w); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($png in $pngs) { $bw.Write($png) }
    $bw.Close(); $fs.Close()
    "icon generated: $Ico"
}

if (-not (Test-Path -LiteralPath $Ico)) { New-LanguageGuardIcon } else { "icon up to date: $Ico" }

# ---------------------------------------------------------------- compile
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) { throw 'csc.exe (.NET Framework 4) not found.' }

$args = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu',
    "/win32icon:$Ico",
    "/out:$Out",
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
    $Src
)
& $csc $args
if ($LASTEXITCODE -ne 0) { throw "csc failed (exit $LASTEXITCODE)." }
"built: $Out"