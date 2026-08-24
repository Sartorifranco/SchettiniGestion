$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$res = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..\SchettiniGestion.WPF\Resources'
$res = [IO.Path]::GetFullPath($res)
$srcIco = Join-Path $res 'sch-pos-logo (1).ico'
if (-not (Test-Path -LiteralPath $srcIco)) {
    $srcIco = Join-Path $res 'schpos-logo.ico'
}
$dstApp = Join-Path $res 'app.ico'
$dstPng = Join-Path $res 'app-icon.png'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-Dib32([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $row = New-Object byte[] $stride
        $xor = New-Object System.Collections.Generic.List[byte]
        for ($y = $h - 1; $y -ge 0; $y--) {
            [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + [int64]$y * $stride), $row, 0, $stride)
            $xor.AddRange($row)
        }
    }
    finally {
        $bmp.UnlockBits($data)
    }
    $andStride = [int][Math]::Floor(($w + 31) / 32) * 4
    $and = New-Object byte[] ($andStride * $h)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $xorBytes = $xor.ToArray()
    $bw.Write([int32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int32]0)
    $bw.Write([int32]$xorBytes.Length)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write([int32]0)
    $bw.Write($xorBytes)
    $bw.Write($and)
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    return $bytes
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $p = New-Object System.IO.MemoryStream
    $bmp.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $p.ToArray()
    $p.Dispose()
    return $bytes
}

function Save-WindowsIco([string]$outPath, [System.Drawing.Bitmap[]]$bitmaps) {
    $payloads = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($b in $bitmaps) {
        # BMP DIB: el compilador de .NET y la barra de tareas leen mal PNG-in-ICO
        $payloads.Add((Get-Dib32 $b))
    }
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$payloads.Count)
    $offset = 6 + 16 * $payloads.Count
    for ($i = 0; $i -lt $payloads.Count; $i++) {
        $b = $bitmaps[$i]
        $w = if ($b.Width -ge 256) { [byte]0 } else { [byte]$b.Width }
        $h = if ($b.Height -ge 256) { [byte]0 } else { [byte]$b.Height }
        $bw.Write($w)
        $bw.Write($h)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$payloads[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $payloads[$i].Length
    }
    foreach ($p in $payloads) { $bw.Write($p) }
    $bw.Flush()
    [IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
}

function New-ScaledSquare([System.Drawing.Bitmap]$src, [int]$size) {
    $b = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
    $pad = [Math]::Max(1, [int][Math]::Round($size * 0.08))
    $g.DrawImage($src, $pad, $pad, $size - 2 * $pad, $size - 2 * $pad)
    $g.Dispose()
    return $b
}

$icon = New-Object System.Drawing.Icon((Resolve-Path -LiteralPath $srcIco).Path, 256, 256)
$full = $icon.ToBitmap()
# Solo la marca SP (sin el wordmark SchPos, ilegible en 32px)
$mark = New-Object System.Drawing.Bitmap 160, 160
$gm = [System.Drawing.Graphics]::FromImage($mark)
$gm.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
$gm.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gm.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$gm.DrawImage($full, (New-Object System.Drawing.Rectangle 8, 16, 144, 96), (New-Object System.Drawing.Rectangle 58, 28, 140, 72), [System.Drawing.GraphicsUnit]::Pixel)
$gm.Dispose()

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$bmps = @()
foreach ($s in $sizes) { $bmps += New-ScaledSquare $mark $s }
Save-WindowsIco $dstApp $bmps
$bmps[-1].Save($dstPng, [System.Drawing.Imaging.ImageFormat]::Png)
foreach ($b in $bmps) { $b.Dispose() }

$bg     = [System.Drawing.Color]::FromArgb(30, 30, 30)
$accent = [System.Drawing.Color]::FromArgb(30, 136, 229)
$white  = [System.Drawing.Color]::FromArgb(238, 238, 238)
$muted  = [System.Drawing.Color]::FromArgb(176, 176, 176)
$bBg = New-Object System.Drawing.SolidBrush $bg
$bAccent = New-Object System.Drawing.SolidBrush $accent
$bWhite = New-Object System.Drawing.SolidBrush $white
$bMuted = New-Object System.Drawing.SolidBrush $muted
function New-PxFont([string]$family, [single]$px, [System.Drawing.FontStyle]$style) {
    return New-Object System.Drawing.Font $family, $px, $style, ([System.Drawing.GraphicsUnit]::Pixel)
}
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

# Badge completo (S + marca), recortando el negro exterior del .ico
$card = New-Object System.Drawing.Bitmap 144, 148
$gc = [System.Drawing.Graphics]::FromImage($card)
$gc.Clear($bg)
$gc.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gc.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$gc.DrawImage($full, (New-Object System.Drawing.Rectangle 0, 0, 144, 148), (New-Object System.Drawing.Rectangle 56, 6, 144, 148), [System.Drawing.GraphicsUnit]::Pixel)
$gc.Dispose()

# Panel izquierdo de Inno: 164x314 (x3 para que al escalar se lea bien)
$w = 492; $h = 942
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear($bg)
$g.FillRectangle($bAccent, 0, 0, 8, $h)

$pad = 36
$logoW = $w - (2 * $pad)
$logoH = [int][Math]::Round($logoW * 148 / 144)
$fBrand = New-PxFont 'Segoe UI' 44 ([System.Drawing.FontStyle]::Bold)
$fSmall = New-PxFont 'Segoe UI' 22 ([System.Drawing.FontStyle]::Regular)
$blockH = $logoH + 20 + 52 + 12 + 32 + 6 + 32
$logoY = [int](($h - $blockH) / 2)
$logoX = $pad
$g.DrawImage($card, $logoX, $logoY, $logoW, $logoH)
$card.Dispose()

$textX = 12
$textW = $w - 24
$brandY = $logoY + $logoH + 18
$g.DrawString('SCHPOS', $fBrand, $bWhite, (New-Object System.Drawing.RectangleF $textX, $brandY, $textW, 52), $fmt)
$g.DrawString('Gestion comercial', $fSmall, $bMuted, (New-Object System.Drawing.RectangleF $textX, ($brandY + 58), $textW, 32), $fmt)
$g.DrawString('Punto de venta', $fSmall, $bMuted, (New-Object System.Drawing.RectangleF $textX, ($brandY + 92), $textW, 32), $fmt)
$left = Join-Path $dir 'wizard-left.png'
$bmp.Save($left, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

$s = 256
$bmp2 = New-Object System.Drawing.Bitmap $s, $s
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.Clear($bg)
$g2.DrawImage($full, (New-Object System.Drawing.Rectangle 16, 14, 224, 228), (New-Object System.Drawing.Rectangle 56, 6, 144, 148), [System.Drawing.GraphicsUnit]::Pixel)
$small = Join-Path $dir 'wizard-small.png'
$bmp2.Save($small, [System.Drawing.Imaging.ImageFormat]::Png)
$g2.Dispose(); $bmp2.Dispose()

$mark.Dispose(); $full.Dispose(); $icon.Dispose()
Write-Host "OK app.ico $((Get-Item $dstApp).Length) bytes"
Write-Host "OK app-icon.png $((Get-Item $dstPng).Length) bytes"
Write-Host "OK $left"
Write-Host "OK $small"
