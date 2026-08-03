param(
    [string]$Source = "unity-client/Assets/Resources/Art/Official/Board/app_playmat.png",
    [string]$Output = "unity-client/Assets/Resources/Art/Official/Board/app_playmat_native.png"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Output))
$bitmap = [System.Drawing.Bitmap]::new($sourcePath)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function New-ComicBurstPath {
    param(
        [float]$CenterX,
        [float]$CenterY,
        [float]$RadiusX,
        [float]$RadiusY,
        [int]$Spikes = 18
    )

    $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
    for ($index = 0; $index -lt ($Spikes * 2); $index++) {
        $angle = (([Math]::PI * 2.0 * $index) / ($Spikes * 2.0)) - ([Math]::PI / 2.0)
        $radius = if (($index % 2) -eq 0) { 1.0 } else { 0.70 + (0.06 * [Math]::Sin($index * 1.7)) }
        $points.Add([System.Drawing.PointF]::new(
            $CenterX + ([Math]::Cos($angle) * $RadiusX * $radius),
            $CenterY + ([Math]::Sin($angle) * $RadiusY * $radius)))
    }

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddPolygon($points.ToArray())
    return $path
}

function Draw-NativeWordmark {
    param(
        [string]$Text,
        [float]$CenterX,
        [float]$CenterY,
        [float]$BurstRadiusX,
        [float]$BurstRadiusY,
        [float]$FontSize,
        [System.Drawing.Color]$BurstColor,
        [System.Drawing.Color]$CleanupColor
    )

    $cleanupBrush = [System.Drawing.SolidBrush]::new($CleanupColor)
    $graphics.FillEllipse(
        $cleanupBrush,
        $CenterX - ($BurstRadiusX * 0.90),
        $CenterY - ($BurstRadiusY * 0.74),
        $BurstRadiusX * 1.80,
        $BurstRadiusY * 1.48)
    $burst = New-ComicBurstPath -CenterX $CenterX -CenterY $CenterY -RadiusX $BurstRadiusX -RadiusY $BurstRadiusY
    $burstBrush = [System.Drawing.SolidBrush]::new($BurstColor)
    $graphics.FillPath($burstBrush, $burst)

    $fontFamily = [System.Drawing.FontFamily]::new("Impact")
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $layout = [System.Drawing.RectangleF]::new(
        $CenterX - $BurstRadiusX,
        $CenterY - $BurstRadiusY,
        $BurstRadiusX * 2.0,
        $BurstRadiusY * 2.0)
    $wordPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $wordPath.AddString($Text, $fontFamily, [int][System.Drawing.FontStyle]::Regular, $FontSize, $layout, $format)
    $outlinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(235, 255, 255, 255), 2.2)
    $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $inkBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 5, 10, 63))
    $graphics.DrawPath($outlinePen, $wordPath)
    $graphics.FillPath($inkBrush, $wordPath)

    $inkBrush.Dispose()
    $outlinePen.Dispose()
    $wordPath.Dispose()
    $format.Dispose()
    $fontFamily.Dispose()
    $cleanupBrush.Dispose()
    $burstBrush.Dispose()
    $burst.Dispose()
}

if ($bitmap.Width -ne 1672 -or $bitmap.Height -ne 941) {
    throw "Expected the official 1672x941 playmat, found $($bitmap.Width)x$($bitmap.Height)."
}

# Replace the baked category names with labels that are part of the same
# starburst artwork. No runtime plate or rectangular cover is required.
Draw-NativeWordmark -Text "LEARN" -CenterX 288 -CenterY 489 -BurstRadiusX 150 -BurstRadiusY 76 -FontSize 58 -BurstColor ([System.Drawing.Color]::FromArgb(255, 235, 249, 255)) -CleanupColor ([System.Drawing.Color]::FromArgb(255, 39, 196, 235))
Draw-NativeWordmark -Text "BUILD" -CenterX 850 -CenterY 489 -BurstRadiusX 215 -BurstRadiusY 78 -FontSize 62 -BurstColor ([System.Drawing.Color]::FromArgb(255, 238, 255, 230)) -CleanupColor ([System.Drawing.Color]::FromArgb(255, 100, 222, 77))
Draw-NativeWordmark -Text "GROW" -CenterX 1352 -CenterY 489 -BurstRadiusX 215 -BurstRadiusY 78 -FontSize 62 -BurstColor ([System.Drawing.Color]::FromArgb(255, 255, 239, 228)) -CleanupColor ([System.Drawing.Color]::FromArgb(255, 244, 78, 68))

# The two mirrored Resource buttons become native Appreciation buttons. The
# same revised pixels are later used by the vertical fill animation.
Draw-NativeWordmark -Text "APPRECIATION" -CenterX 480 -CenterY 167 -BurstRadiusX 138 -BurstRadiusY 50 -FontSize 27 -BurstColor ([System.Drawing.Color]::FromArgb(255, 232, 251, 225)) -CleanupColor ([System.Drawing.Color]::FromArgb(255, 208, 244, 196))
Draw-NativeWordmark -Text "APPRECIATION" -CenterX 480 -CenterY 799 -BurstRadiusX 138 -BurstRadiusY 50 -FontSize 27 -BurstColor ([System.Drawing.Color]::FromArgb(255, 232, 251, 225)) -CleanupColor ([System.Drawing.Color]::FromArgb(255, 208, 244, 196))

$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
Write-Output $outputPath
