param(
    [Parameter(Mandatory = $true)]
    [string]$MasterPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Get-UnlockedImage {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $stream = [System.IO.MemoryStream]::new($bytes)

    try {
        $sourceImage = [System.Drawing.Image]::FromStream($stream)

        try {
            return [System.Drawing.Bitmap]::new($sourceImage)
        }
        finally {
            $sourceImage.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function New-ResizedPngBytes {
    param([string]$Source, [int]$Width, [int]$Height)

    $sourceImage = Get-UnlockedImage -Path $Source
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $imageAttributes = [System.Drawing.Imaging.ImageAttributes]::new()
    $output = [System.IO.MemoryStream]::new()

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $imageAttributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)

        $scale = [Math]::Min($Width / $sourceImage.Width, $Height / $sourceImage.Height)
        $drawWidth = [Math]::Max(1, [int][Math]::Round($sourceImage.Width * $scale))
        $drawHeight = [Math]::Max(1, [int][Math]::Round($sourceImage.Height * $scale))
        $destinationRectangle = [System.Drawing.Rectangle]::new(
            [int](($Width - $drawWidth) / 2),
            [int](($Height - $drawHeight) / 2),
            $drawWidth,
            $drawHeight
        )

        $graphics.DrawImage(
            $sourceImage,
            $destinationRectangle,
            0,
            0,
            $sourceImage.Width,
            $sourceImage.Height,
            [System.Drawing.GraphicsUnit]::Pixel,
            $imageAttributes
        )
        $bitmap.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)

        return $output.ToArray()
    }
    finally {
        $output.Dispose()
        $imageAttributes.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
        $sourceImage.Dispose()
    }
}

function Export-Png {
    param([string]$Source, [string]$Destination, [int]$Width, [int]$Height)

    $directory = [System.IO.Path]::GetDirectoryName($Destination)
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    [byte[]]$pngBytes = New-ResizedPngBytes -Source $Source -Width $Width -Height $Height
    [System.IO.File]::WriteAllBytes($Destination, $pngBytes)
}

function Export-PngIco {
    param([string]$Source, [string]$Destination, [int[]]$Sizes)

    $directory = [System.IO.Path]::GetDirectoryName($Destination)
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null

    $payloads = [System.Collections.Generic.List[byte[]]]::new()

    foreach ($size in $Sizes) {
        [byte[]]$payload = New-ResizedPngBytes -Source $Source -Width $size -Height $size
        $payloads.Add($payload)
    }

    $iconStream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($iconStream, [System.Text.Encoding]::UTF8, $true)

    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)

        [uint32]$payloadOffset = 6 + (16 * $Sizes.Count)

        for ($index = 0; $index -lt $Sizes.Count; $index++) {
            $size = $Sizes[$index]
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $payload = $payloads[$index]

            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$payload.Length)
            $writer.Write([uint32]$payloadOffset)

            $payloadOffset += [uint32]$payload.Length
        }

        foreach ($payload in $payloads) {
            $writer.Write($payload)
        }

        $writer.Flush()
        [System.IO.File]::WriteAllBytes($Destination, $iconStream.ToArray())
    }
    finally {
        $writer.Dispose()
        $iconStream.Dispose()
    }
}

function Assert-ImageSize {
    param([string]$Path, [int]$Width, [int]$Height)

    $image = Get-UnlockedImage -Path $Path

    try {
        if ($image.Width -ne $Width -or $image.Height -ne $Height) {
            throw "Image '$Path' is $($image.Width)x$($image.Height); expected ${Width}x${Height}."
        }

        Write-Host "Verified PNG: $Path (${Width}x${Height})"
    }
    finally {
        $image.Dispose()
    }
}

function Assert-PngIco {
    param([string]$Path, [int[]]$ExpectedSizes)

    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)

    try {
        $reserved = $reader.ReadUInt16()
        $type = $reader.ReadUInt16()
        $count = $reader.ReadUInt16()

        if ($reserved -ne 0 -or $type -ne 1 -or $count -ne $ExpectedSizes.Count) {
            throw "ICO '$Path' has an invalid ICONDIR or entry count."
        }

        $entries = [System.Collections.Generic.List[hashtable]]::new()
        $actualSizes = for ($index = 0; $index -lt $count; $index++) {
            $width = $reader.ReadByte()
            $height = $reader.ReadByte()
            $reader.ReadByte() | Out-Null
            $reader.ReadByte() | Out-Null
            $reader.ReadUInt16() | Out-Null
            $reader.ReadUInt16() | Out-Null
            $payloadLength = $reader.ReadUInt32()
            $payloadOffset = $reader.ReadUInt32()

            $decodedWidth = if ($width -eq 0) { 256 } else { [int]$width }
            $decodedHeight = if ($height -eq 0) { 256 } else { [int]$height }

            if (
                $decodedWidth -ne $decodedHeight -or
                $payloadLength -lt 8 -or
                ([uint64]$payloadOffset + [uint64]$payloadLength) -gt [uint64]$stream.Length
            ) {
                throw "ICO '$Path' contains an invalid ICONDIRENTRY."
            }

            $entries.Add(@{ Offset = $payloadOffset; Length = $payloadLength })
            $decodedWidth
        }

        if ([string]::Join(',', $actualSizes) -ne [string]::Join(',', $ExpectedSizes)) {
            throw "ICO '$Path' sizes are $([string]::Join(',', $actualSizes)); expected $([string]::Join(',', $ExpectedSizes))."
        }

        $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)

        foreach ($entry in $entries) {
            $stream.Position = $entry.Offset
            $actualSignature = $reader.ReadBytes($pngSignature.Length)

            if ([string]::Join(',', $actualSignature) -ne [string]::Join(',', $pngSignature)) {
                throw "ICO '$Path' contains a non-PNG image payload."
            }
        }

        Write-Host "Verified ICO: $Path ($count PNG entries: $([string]::Join(', ', $actualSizes)))"
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Export-WhiteVariant {
    param([string]$Source, [string]$Destination)

    $sourceImage = Get-UnlockedImage -Path $Source
    $bitmap = [System.Drawing.Bitmap]::new($sourceImage)
    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $bitmapData = $bitmap.LockBits(
        $rectangle,
        [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    )

    try {
        $byteCount = [Math]::Abs($bitmapData.Stride) * $bitmap.Height
        $pixels = [byte[]]::new($byteCount)
        [System.Runtime.InteropServices.Marshal]::Copy($bitmapData.Scan0, $pixels, 0, $byteCount)

        for ($row = 0; $row -lt $bitmap.Height; $row++) {
            for ($column = 0; $column -lt $bitmap.Width; $column++) {
                $offset = ($row * $bitmapData.Stride) + ($column * 4)
                $blue = $pixels[$offset]
                $green = $pixels[$offset + 1]
                $red = $pixels[$offset + 2]
                $alpha = $pixels[$offset + 3]

                if ($alpha -gt 0) {
                    $isCyan = $blue -ge 120 -and $green -ge 120 -and $red -le 100 -and ($green - $red) -ge 50

                    if (-not $isCyan) {
                        $pixels[$offset] = 255
                        $pixels[$offset + 1] = 255
                        $pixels[$offset + 2] = 255
                    }
                }
            }
        }

        [System.Runtime.InteropServices.Marshal]::Copy($pixels, 0, $bitmapData.Scan0, $byteCount)
    }
    finally {
        $bitmap.UnlockBits($bitmapData)
    }

    try {
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
        $sourceImage.Dispose()
    }
}

$resolvedMasterPath = (Resolve-Path -LiteralPath $MasterPath).Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$storeAssetsDirectory = Join-Path $repositoryRoot 'ShareX.Setup\MicrosoftStore\Assets'
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)

Assert-ImageSize -Path $resolvedMasterPath -Width 1024 -Height 1024

$storeTargets = @(
    @{ Name = 'LargeTile.scale-100.png'; Width = 310; Height = 310 },
    @{ Name = 'LargeTile.scale-125.png'; Width = 388; Height = 388 },
    @{ Name = 'LargeTile.scale-150.png'; Width = 465; Height = 465 },
    @{ Name = 'LargeTile.scale-200.png'; Width = 620; Height = 620 },
    @{ Name = 'LargeTile.scale-400.png'; Width = 1240; Height = 1240 },
    @{ Name = 'SmallTile.scale-100.png'; Width = 71; Height = 71 },
    @{ Name = 'SmallTile.scale-125.png'; Width = 89; Height = 89 },
    @{ Name = 'SmallTile.scale-150.png'; Width = 107; Height = 107 },
    @{ Name = 'SmallTile.scale-200.png'; Width = 142; Height = 142 },
    @{ Name = 'SmallTile.scale-400.png'; Width = 284; Height = 284 },
    @{ Name = 'Square150x150Logo.scale-100.png'; Width = 150; Height = 150 },
    @{ Name = 'Square150x150Logo.scale-125.png'; Width = 188; Height = 188 },
    @{ Name = 'Square150x150Logo.scale-150.png'; Width = 225; Height = 225 },
    @{ Name = 'Square150x150Logo.scale-200.png'; Width = 300; Height = 300 },
    @{ Name = 'Square150x150Logo.scale-400.png'; Width = 600; Height = 600 },
    @{ Name = 'Square44x44Logo.scale-100.png'; Width = 44; Height = 44 },
    @{ Name = 'Square44x44Logo.scale-125.png'; Width = 55; Height = 55 },
    @{ Name = 'Square44x44Logo.scale-150.png'; Width = 66; Height = 66 },
    @{ Name = 'Square44x44Logo.scale-200.png'; Width = 88; Height = 88 },
    @{ Name = 'Square44x44Logo.scale-400.png'; Width = 176; Height = 176 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-16.png'; Width = 16; Height = 16 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-256.png'; Width = 256; Height = 256 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-32.png'; Width = 32; Height = 32 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-48.png'; Width = 48; Height = 48 },
    @{ Name = 'Square44x44Logo.targetsize-16.png'; Width = 16; Height = 16 },
    @{ Name = 'Square44x44Logo.targetsize-24_altform-unplated.png'; Width = 24; Height = 24 },
    @{ Name = 'Square44x44Logo.targetsize-24.png'; Width = 24; Height = 24 },
    @{ Name = 'Square44x44Logo.targetsize-256.png'; Width = 256; Height = 256 },
    @{ Name = 'Square44x44Logo.targetsize-32.png'; Width = 32; Height = 32 },
    @{ Name = 'Square44x44Logo.targetsize-48.png'; Width = 48; Height = 48 },
    @{ Name = 'StoreLogo.scale-100.png'; Width = 50; Height = 50 },
    @{ Name = 'StoreLogo.scale-125.png'; Width = 63; Height = 63 },
    @{ Name = 'StoreLogo.scale-150.png'; Width = 75; Height = 75 },
    @{ Name = 'StoreLogo.scale-200.png'; Width = 100; Height = 100 },
    @{ Name = 'StoreLogo.scale-400.png'; Width = 200; Height = 200 },
    @{ Name = 'Wide310x150Logo.scale-100.png'; Width = 310; Height = 150 },
    @{ Name = 'Wide310x150Logo.scale-125.png'; Width = 388; Height = 188 },
    @{ Name = 'Wide310x150Logo.scale-150.png'; Width = 465; Height = 225 },
    @{ Name = 'Wide310x150Logo.scale-200.png'; Width = 620; Height = 300 },
    @{ Name = 'Wide310x150Logo.scale-400.png'; Width = 1240; Height = 600 }
)

$expectedStoreNames = $storeTargets.Name | Sort-Object
$existingStoreNames = Get-ChildItem -LiteralPath $storeAssetsDirectory -Filter '*.png' | ForEach-Object Name | Sort-Object

if ([string]::Join('|', $expectedStoreNames) -ne [string]::Join('|', $existingStoreNames)) {
    throw 'The Store asset set differs from the explicit CapX target table. Update the table before regenerating assets.'
}

foreach ($target in $storeTargets) {
    $destination = Join-Path $storeAssetsDirectory $target.Name
    Export-Png -Source $resolvedMasterPath -Destination $destination -Width $target.Width -Height $target.Height
    Assert-ImageSize -Path $destination -Width $target.Width -Height $target.Height
}

$logoDestination = Join-Path $repositoryRoot 'ShareX.HelpersLib\Resources\ShareX_Logo.png'
Export-Png -Source $resolvedMasterPath -Destination $logoDestination -Width 256 -Height 256
Assert-ImageSize -Path $logoDestination -Width 256 -Height 256

$coloredIconDestinations = @(
    (Join-Path $repositoryRoot 'ShareX\ShareX_Icon.ico'),
    (Join-Path $repositoryRoot 'ShareX\ShareX_File_Icon.ico'),
    (Join-Path $repositoryRoot 'ShareX.Steam\ShareX_Icon.ico'),
    (Join-Path $repositoryRoot 'ShareX.HelpersLib\Resources\ShareX_Icon.ico')
)

foreach ($destination in $coloredIconDestinations) {
    Export-PngIco -Source $resolvedMasterPath -Destination $destination -Sizes $iconSizes
    Assert-PngIco -Path $destination -ExpectedSizes $iconSizes
}

$whiteVariantPath = Join-Path ([System.IO.Path]::GetTempPath()) "CapX_Logo_White_$([Guid]::NewGuid().ToString('N')).png"

try {
    Export-WhiteVariant -Source $resolvedMasterPath -Destination $whiteVariantPath
    $whiteIconDestination = Join-Path $repositoryRoot 'ShareX.HelpersLib\Resources\ShareX_Icon_White.ico'
    Export-PngIco -Source $whiteVariantPath -Destination $whiteIconDestination -Sizes $iconSizes
    Assert-PngIco -Path $whiteIconDestination -ExpectedSizes $iconSizes
}
finally {
    if (Test-Path -LiteralPath $whiteVariantPath) {
        Remove-Item -LiteralPath $whiteVariantPath
    }
}

Write-Host 'CapX branding assets generated and verified successfully.'
