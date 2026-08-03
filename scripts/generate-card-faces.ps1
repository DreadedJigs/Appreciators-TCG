param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$Scale = 2
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$cardDataPath = Join-Path $RepositoryRoot 'unity-client\Assets\Resources\prototype-cards.json'
$templatePath = Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\CardTemplate\templates\full_card_template_blank.png'
$headerReferencePath = Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\CardTemplate\reference\pathetic_kid_full_card_reference.png'
$artRoot = Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Cards'
$outputRoot = Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\GeneratedCards'

if (-not (Test-Path -LiteralPath $cardDataPath)) { throw "Missing card data: $cardDataPath" }
if (-not (Test-Path -LiteralPath $templatePath)) { throw "Missing official card template: $templatePath" }
if (-not (Test-Path -LiteralPath $headerReferencePath)) { throw "Missing official card header reference: $headerReferencePath" }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$cards = (Get-Content -LiteralPath $cardDataPath -Raw | ConvertFrom-Json).cards
$template = [System.Drawing.Image]::FromFile($templatePath)
$headerReference = [System.Drawing.Image]::FromFile($headerReferencePath)
$canvasWidth = $template.Width * $Scale
$canvasHeight = $template.Height * $Scale
$headingFamily = New-Object System.Drawing.FontFamily('Arial Black')
$bodyFamily = New-Object System.Drawing.FontFamily('Arial')
$artIcon = [System.Drawing.Image]::FromFile((Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\CardTemplate\components\icons\icon_heart_art.png'))
$blockchainIcon = [System.Drawing.Image]::FromFile((Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\CardTemplate\components\icons\icon_sword_blockchain.png'))
$communityIcon = [System.Drawing.Image]::FromFile((Join-Path $RepositoryRoot 'unity-client\Assets\Resources\Art\Official\CardTemplate\components\icons\icon_star_community.png'))

function New-ScaledRectangle([float]$x, [float]$y, [float]$width, [float]$height) {
    return New-Object System.Drawing.RectangleF(
        ($x * $Scale),
        ($y * $Scale),
        ($width * $Scale),
        ($height * $Scale))
}

function New-CenteredFormat {
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
    return $format
}

function Get-FittedFontSize(
    [System.Drawing.Graphics]$graphics,
    [string]$text,
    [System.Drawing.FontFamily]$family,
    [System.Drawing.FontStyle]$style,
    [System.Drawing.RectangleF]$bounds,
    [float]$maximum,
    [float]$minimum,
    [bool]$wrap
) {
    $format = New-CenteredFormat
    if (-not $wrap) {
        $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    }

    for ($size = $maximum; $size -ge $minimum; $size -= 1) {
        $font = New-Object System.Drawing.Font($family, $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
        try {
            $measured = if ($wrap) {
                $graphics.MeasureString($text, $font, [System.Drawing.SizeF]::new($bounds.Width, $bounds.Height), $format)
            }
            else {
                $graphics.MeasureString($text, $font, [System.Drawing.PointF]::Empty, $format)
            }
            if ($measured.Width -le $bounds.Width -and $measured.Height -le $bounds.Height) {
                return $size
            }
        }
        finally {
            $font.Dispose()
        }
    }

    return $minimum
}

function Draw-OutlinedText(
    [System.Drawing.Graphics]$graphics,
    [string]$text,
    [System.Drawing.FontFamily]$family,
    [System.Drawing.FontStyle]$style,
    [System.Drawing.RectangleF]$bounds,
    [float]$maximumSize,
    [float]$minimumSize,
    [float]$outlineWidth,
    [System.Drawing.Color]$fillColor,
    [bool]$wrap = $false
) {
    $format = New-CenteredFormat
    if (-not $wrap) {
        $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    }

    $size = Get-FittedFontSize $graphics $text $family $style $bounds $maximumSize $minimumSize $wrap
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 5, 4, 24), $outlineWidth)
    $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $fill = New-Object System.Drawing.SolidBrush($fillColor)
    try {
        $path.AddString($text, $family, [int]$style, $size, $bounds, $format)
        $graphics.DrawPath($outline, $path)
        $graphics.FillPath($fill, $path)
    }
    finally {
        $path.Dispose()
        $outline.Dispose()
        $fill.Dispose()
        $format.Dispose()
    }
}

function Draw-CoveredArt(
    [System.Drawing.Graphics]$graphics,
    [System.Drawing.Image]$art,
    [System.Drawing.RectangleF]$target
) {
    $source = New-Object System.Drawing.RectangleF(0, 0, $art.Width, $art.Height)
    if ($art.Height -ge ($art.Width * 1.35)) {
        # Tall alpha mockups are complete card sheets. Extract only their art window.
        $source = New-Object System.Drawing.RectangleF(
            ($art.Width * 0.055),
            ($art.Height * 0.134),
            ($art.Width * 0.89),
            ($art.Height * 0.452))
    }

    $targetRatio = $target.Width / $target.Height
    $sourceRatio = $source.Width / $source.Height
    if ($sourceRatio -gt $targetRatio) {
        $croppedWidth = $source.Height * $targetRatio
        $source.X += ($source.Width - $croppedWidth) / 2
        $source.Width = $croppedWidth
    }
    else {
        $croppedHeight = $source.Width / $targetRatio
        $source.Y += ($source.Height - $croppedHeight) / 2
        $source.Height = $croppedHeight
    }

    $graphics.DrawImage($art, $target, $source, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-RoundedPath([System.Drawing.RectangleF]$bounds, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($bounds.X, $bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bounds.X, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-LanePlaque(
    [System.Drawing.Graphics]$graphics,
    [System.Drawing.RectangleF]$bounds,
    [System.Drawing.Color]$accent,
    [System.Drawing.Image]$icon,
    [System.Drawing.RectangleF]$iconSource,
    [string]$label,
    [int]$value,
    [bool]$darkLabel
) {
    $path = New-RoundedPath $bounds (16 * $Scale)
    $panelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 10, 70))
    $accentBrush = New-Object System.Drawing.SolidBrush($accent)
    $border = New-Object System.Drawing.Pen($accent, (5 * $Scale))
    $border.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    try {
        $graphics.FillPath($panelBrush, $path)
        $graphics.DrawPath($border, $path)
        $band = New-Object System.Drawing.RectangleF(
            ($bounds.X + (5 * $Scale)),
            ($bounds.Bottom - (47 * $Scale)),
            ($bounds.Width - (10 * $Scale)),
            (41 * $Scale))
        $graphics.FillRectangle($accentBrush, $band)

        $iconTarget = New-Object System.Drawing.RectangleF(
            ($bounds.X + (16 * $Scale)),
            ($bounds.Y + (17 * $Scale)),
            (82 * $Scale),
            (82 * $Scale))
        $iconClip = New-Object System.Drawing.Drawing2D.GraphicsPath
        $iconState = $graphics.Save()
        try {
            $iconClip.AddEllipse($iconTarget)
            $graphics.SetClip($iconClip)
            $graphics.DrawImage($icon, $iconTarget, $iconSource, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {
            $graphics.Restore($iconState)
            $iconClip.Dispose()
        }

        $valueBadge = New-Object System.Drawing.RectangleF(
            ($bounds.Right - (88 * $Scale)),
            ($bounds.Y + (20 * $Scale)),
            (70 * $Scale),
            (70 * $Scale))
        $valueBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 248, 247, 255))
        $valueBorder = New-Object System.Drawing.Pen($accent, (4 * $Scale))
        try {
            $graphics.FillEllipse($valueBrush, $valueBadge)
            $graphics.DrawEllipse($valueBorder, $valueBadge)
            Draw-OutlinedText $graphics ([string]$value) $headingFamily ([System.Drawing.FontStyle]::Regular) $valueBadge (38 * $Scale) (24 * $Scale) (2 * $Scale) ([System.Drawing.Color]::FromArgb(255, 15, 10, 70))
        }
        finally {
            $valueBrush.Dispose()
            $valueBorder.Dispose()
        }

        $labelColor = if ($darkLabel) { [System.Drawing.Color]::FromArgb(255, 15, 10, 70) } else { [System.Drawing.Color]::White }
        Draw-OutlinedText $graphics $label $headingFamily ([System.Drawing.FontStyle]::Regular) ([System.Drawing.RectangleF]::new(($bounds.X + (8 * $Scale)), ($bounds.Bottom - (46 * $Scale)), ($bounds.Width - (16 * $Scale)), (38 * $Scale))) (21 * $Scale) (14 * $Scale) (1.2 * $Scale) $labelColor
    }
    finally {
        $path.Dispose()
        $panelBrush.Dispose()
        $accentBrush.Dispose()
        $border.Dispose()
    }
}

function Draw-CombatHeader([System.Drawing.Graphics]$graphics, $card) {
    # The one-lane card face exposes only the two combat values players need.
    # Cover the retired three-domain header before drawing the new plaques.
    $headerWash = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 32, 18, 91))
    try {
        $graphics.FillRectangle($headerWash, (48 * $Scale), (42 * $Scale), (928 * $Scale), (184 * $Scale))
    }
    finally {
        $headerWash.Dispose()
    }

    $attack = [int]$card.power
    if ($null -ne $card.attack) { $attack = [int]$card.attack }
    $defense = [int]$card.appreciation
    if ($null -ne $card.defense) { $defense = [int]$card.defense }

    Draw-LanePlaque $graphics (New-ScaledRectangle 55 49 443 174) ([System.Drawing.ColorTranslator]::FromHtml('#FF2314')) $headerReference ([System.Drawing.RectangleF]::new(395, 55, 110, 120)) 'ATTACK' $attack $false
    Draw-LanePlaque $graphics (New-ScaledRectangle 526 49 443 174) ([System.Drawing.ColorTranslator]::FromHtml('#1769FF')) $headerReference ([System.Drawing.RectangleF]::new(83, 55, 110, 120)) 'DEFENSE' $defense $false
}

function Draw-NamePlate([System.Drawing.Graphics]$graphics, [string]$name) {
    # Cover the retired cost badge and use the full strip for the card name.
    $bounds = New-ScaledRectangle 55 1048 914 170
    $path = New-RoundedPath $bounds (22 * $Scale)
    $fill = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#5B36CE'))
    $border = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#FFC700'), (5 * $Scale))
    try {
        $graphics.FillPath($fill, $path)
        $graphics.DrawPath($border, $path)
        Draw-OutlinedText $graphics $name.ToUpperInvariant() $headingFamily ([System.Drawing.FontStyle]::Regular) (New-ScaledRectangle 82 1072 860 122) (48 * $Scale) (18 * $Scale) (3 * $Scale) ([System.Drawing.Color]::White)
    }
    finally {
        $path.Dispose()
        $fill.Dispose()
        $border.Dispose()
    }
}

try {
    foreach ($card in $cards) {
        $artPath = Join-Path $artRoot ($card.id + '.png')
        if (-not (Test-Path -LiteralPath $artPath)) {
            throw "Missing active card illustration: $artPath"
        }

        $bitmap = New-Object System.Drawing.Bitmap($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $art = [System.Drawing.Image]::FromFile($artPath)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

            $graphics.DrawImage($template, 0, 0, $canvasWidth, $canvasHeight)
            Draw-CombatHeader $graphics $card
            Draw-CoveredArt $graphics $art (New-ScaledRectangle 78 232 868 792)

            $white = [System.Drawing.Color]::White
            Draw-NamePlate $graphics ([string]$card.name)
            $buildText = if ([string]::IsNullOrWhiteSpace([string]$card.buildEffect)) { [string]$card.effectText } else { [string]$card.buildEffect }
            $discardText = if ([string]::IsNullOrWhiteSpace([string]$card.discardEffect)) { 'Reveal this card, then place it face-up in the discard pile.' } else { [string]$card.discardEffect }
            $rulesText = "BUILD: $buildText`nDISCARD: $discardText"
            Draw-OutlinedText $graphics $rulesText $bodyFamily ([System.Drawing.FontStyle]::Bold) (New-ScaledRectangle 104 1244 816 196) (29 * $Scale) (15 * $Scale) (2.2 * $Scale) $white $true

            $outputPath = Join-Path $outputRoot ($card.id + '.png')
            $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "Generated $($card.id) -> $outputPath"
        }
        finally {
            $art.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $template.Dispose()
    $headerReference.Dispose()
    $headingFamily.Dispose()
    $bodyFamily.Dispose()
    $artIcon.Dispose()
    $blockchainIcon.Dispose()
    $communityIcon.Dispose()
}

Write-Host "Generated $($cards.Count) production card faces at ${canvasWidth}x${canvasHeight}."
