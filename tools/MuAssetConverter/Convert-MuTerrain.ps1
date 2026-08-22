#Requires -Version 5.1
<#
.SYNOPSIS
    Converts MU Online terrain files to PNG for use in Godot 4.
.PARAMETER InputDir
    Path to the MU client Data directory (contains World1, World2, etc.)
.PARAMETER OutputDir
    Output directory. Maps are placed in <OutputDir>/<MapName>/.
.PARAMETER Map
    Single map to convert (e.g. "World1"). If omitted, discovers all maps.
.EXAMPLE
    .\Convert-MuTerrain.ps1 -InputDir "C:\MuOnline\Data" -OutputDir "C:\out" -Map World1
#>
param(
    [Parameter(Mandatory)][string]$InputDir,
    [Parameter(Mandatory)][string]$OutputDir,
    [string]$Map = ""
)

Add-Type -AssemblyName System.Drawing

# ── helpers ──────────────────────────────────────────────────────────────────

function Save-Bitmap([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

# ── MU map file decryption (from MuMain ZzzLodTerrain.h / _crypt.h) ──────────

function Invoke-MapFileDecrypt([byte[]]$src) {
    # Step 1: XOR key table + rolling key (MapFileDecrypt)
    [byte[]]$xorKey = 0xD1,0x73,0x52,0xF6,0xD2,0x9A,0xCB,0x27,
                      0x3E,0xAF,0x59,0x31,0x37,0xB3,0xE7,0xA2
    $rollingKey = 0x5E
    $dst = New-Object 'byte[]' $src.Length
    for ($i = 0; $i -lt $src.Length; $i++) {
        $dst[$i] = [byte]((($src[$i] -bxor $xorKey[$i % 16]) - $rollingKey) -band 0xFF)
        $rollingKey = ($src[$i] + 0x3D) -band 0xFF
    }
    # Step 2: BuxConvert (XOR with {0xFC, 0xCF, 0xAB})
    [byte[]]$bux = 0xFC,0xCF,0xAB
    for ($i = 0; $i -lt $dst.Length; $i++) {
        $dst[$i] = [byte]($dst[$i] -bxor $bux[$i % 3])
    }
    return $dst
}

# ── ATT → walkable.png ───────────────────────────────────────────────────────
# Tile bit flags (from MuMain _define.h):
#   0x01 TW_SAFEZONE  — safe zone (still walkable)
#   0x02 TW_CHARACTER — character collision
#   0x04 TW_NOMOVE    — obstacle/wall (cannot walk)
#   0x10 TW_WATER     — water (not walkable)
# Output pixel encoding:
#   255 = normal walkable  (shader: bright green)
#   128 = safezone         (shader: olive green)
#     0 = wall / water     (shader: dark stone)

function Resolve-AttBuffer([byte[]]$buf) {
    # Returns [headerSize, isWord] if buf looks like valid ATT (Version==0, size matches), else $null.
    if ($buf[0] -ne 0) { return $null }
    $dataSize = 256 * 256
    $extSize  = 256 * 256 * 2
    foreach ($hdr in @(3, 4, 7)) {
        $avail = $buf.Length - $hdr
        if ($avail -eq $dataSize) { return @($hdr, $false) }
        if ($avail -eq $extSize)  { return @($hdr, $true)  }
    }
    return $null
}

function Convert-Att([string]$attPath, [string]$outPath) {
    $fileBytes = [System.IO.File]::ReadAllBytes($attPath)

    # Try plaintext first (MuMain dev assets are unencrypted),
    # fall back to MapFileDecrypt+BuxConvert (production client files).
    $resolved = Resolve-AttBuffer $fileBytes
    $raw = $fileBytes
    if ($null -eq $resolved) {
        $raw      = Invoke-MapFileDecrypt $fileBytes
        $resolved = Resolve-AttBuffer $raw
    }
    if ($null -eq $resolved) {
        throw "ATT: unrecognised format for file of $($fileBytes.Length) bytes"
    }
    $headerSize = $resolved[0]
    $isWord     = $resolved[1]

    $stride = if ($isWord) { 2 } else { 1 }

    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 256; $y++) {
            for ($x = 0; $x -lt 256; $x++) {
                $idx  = $headerSize + ($y * 256 + $x) * $stride
                $tile = if ($isWord) { [int]$raw[$idx] -bor ([int]$raw[$idx + 1] -shl 8) } else { [int]$raw[$idx] }
                $v    = if ($tile -band 0x14) { 0 } elseif ($tile -band 0x01) { 128 } else { 255 }
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v, $v, $v))
            }
        }
        Save-Bitmap $bmp $outPath
    } finally { $bmp.Dispose() }
}

# ── OZB → heightmap.png ──────────────────────────────────────────────────────

function Convert-Ozb([string]$ozbPath, [string]$outPath) {
    $raw   = [System.IO.File]::ReadAllBytes($ozbPath)
    $count = 257 * 257

    if ($raw.Length -ge $count * 2) {
        $stride = 2; $offset = $raw.Length - $count * 2
    } elseif ($raw.Length -ge $count) {
        $stride = 1; $offset = $raw.Length - $count
    } else {
        throw "OZB too small: $($raw.Length) bytes"
    }

    $heights = New-Object 'int[]' $count
    $min = [int]::MaxValue; $max = [int]::MinValue
    for ($i = 0; $i -lt $count; $i++) {
        $idx = $offset + $i * $stride
        $h = if ($stride -eq 2) {
            [int]($raw[$idx]) -bor ([int]($raw[$idx+1]) -shl 8)
        } else { [int]$raw[$idx] }
        $heights[$i] = $h
        if ($h -lt $min) { $min = $h }
        if ($h -gt $max) { $max = $h }
    }

    $range = $max - $min
    $bmp = New-Object System.Drawing.Bitmap(257, 257, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 257; $y++) {
            for ($x = 0; $x -lt 257; $x++) {
                $h = $heights[$y * 257 + $x]
                $v = if ($range -eq 0) { 0 } else { [int](($h - $min) * 255 / $range) }
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v, $v, $v))
            }
        }
        Save-Bitmap $bmp $outPath
        Write-Host "  heightmap.png (257x257, height range $min-$max)"
    } finally { $bmp.Dispose() }
}

# ── TGA decoder (used by OZT) ─────────────────────────────────────────────────

function Convert-TgaBytes([byte[]]$raw, [int]$hdrOff, [string]$outPath) {
    $idLen  = $raw[$hdrOff]
    $imgType = $raw[$hdrOff + 2]
    $cmLen  = [System.BitConverter]::ToUInt16($raw, $hdrOff + 5)
    $cmSize = $raw[$hdrOff + 7]
    $width  = [int][System.BitConverter]::ToUInt16($raw, $hdrOff + 12)
    $height = [int][System.BitConverter]::ToUInt16($raw, $hdrOff + 14)
    $bpp    = [int]$raw[$hdrOff + 16]
    $imgDesc = $raw[$hdrOff + 17]
    $topOrigin = ($imgDesc -band 0x20) -ne 0  # bit5=1 means top-to-bottom

    if ($imgType -ne 2) { throw "Only type-2 (true-color) TGA supported; got type $imgType" }
    if ($bpp -notin @(24, 32)) { throw "Only 24/32-bit TGA supported; got $bpp bpp" }

    $cmBytes   = [int]([Math]::Ceiling($cmLen * $cmSize / 8.0))
    $pixStart  = $hdrOff + 18 + $idLen + $cmBytes
    $bppBytes  = $bpp / 8
    $rowBytes  = $width * $bppBytes

    $fmt = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $bmp = New-Object System.Drawing.Bitmap($width, $height, $fmt)
    $rect    = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $bmpData = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $fmt)
    try {
        $dstStride = $bmpData.Scan0.ToInt64()
        for ($y = 0; $y -lt $height; $y++) {
            # TGA rows default to bottom-to-top; flip unless top-origin flag is set
            $srcRow = if ($topOrigin) { $y } else { $height - 1 - $y }
            $srcOff = $pixStart + $srcRow * $rowBytes
            $dstPtr = [IntPtr]::new($dstStride + $y * $bmpData.Stride)

            if ($bppBytes -eq 4) {
                # TGA stores BGRA. Copy row then force alpha=255 (MU uses 4th byte for engine data, not transparency)
                $row32 = New-Object 'byte[]' $rowBytes
                [System.Array]::Copy($raw, $srcOff, $row32, 0, $rowBytes)
                for ($x = 3; $x -lt $rowBytes; $x += 4) { $row32[$x] = 255 }
                [System.Runtime.InteropServices.Marshal]::Copy($row32, 0, $dstPtr, $rowBytes)
            } else {
                # 24-bit BGR → need to synthesize alpha=255
                $row32 = New-Object 'byte[]' ($width * 4)
                for ($x = 0; $x -lt $width; $x++) {
                    $s = $srcOff + $x * 3; $d = $x * 4
                    $row32[$d]   = $raw[$s]      # B
                    $row32[$d+1] = $raw[$s + 1]  # G
                    $row32[$d+2] = $raw[$s + 2]  # R
                    $row32[$d+3] = 255            # A
                }
                [System.Runtime.InteropServices.Marshal]::Copy($row32, 0, $dstPtr, $row32.Length)
            }
        }
    } finally { $bmp.UnlockBits($bmpData) }

    Save-Bitmap $bmp $outPath
    $bmp.Dispose()
}

function Convert-Ozt([string]$oztPath, [string]$outPath) {
    $raw = [System.IO.File]::ReadAllBytes($oztPath)
    # OZT = 4-byte WebZen header + standard TGA
    Convert-TgaBytes $raw 4 $outPath
}

# ── OZJ (JPEG with optional prepended header) ─────────────────────────────────
# OZJ files contain an outer JPEG wrapper (APP markers only, no image data) followed
# by the actual JPEG at a fixed +24 byte offset, and sometimes a thumbnail after that.
# Strategy: collect all FF D8 FF offsets, try each, keep the largest decoded image.
# Fallback: patch non-standard DQT table indices (WebZen sometimes uses index 15).

function Repair-JpegDqt([byte[]]$src, [int]$offset, [int]$length) {
    $buf = New-Object 'byte[]' $length
    [System.Array]::Copy($src, $offset, $buf, 0, $length)
    $i = 0
    while ($i -lt $buf.Length - 3) {
        if ($buf[$i] -ne 0xFF) { $i++; continue }
        $marker = $buf[$i + 1]
        if ($marker -eq 0xDB) {
            $segLen = ([int]$buf[$i + 2] -shl 8) -bor [int]$buf[$i + 3]
            $segEnd = $i + 2 + $segLen
            $j = $i + 4
            while ($j -lt $segEnd -and $j -lt $buf.Length) {
                $precIdx = $buf[$j]
                $tblIdx  = $precIdx -band 0x0F
                $prec    = ($precIdx -shr 4) -band 0x0F
                if ($tblIdx -gt 3) { $buf[$j] = [byte](($precIdx -band 0xF0)) }
                $j += 1 + 64 * ($prec + 1)
            }
            $i = $segEnd
        } elseif ($marker -in @(0xD0,0xD1,0xD2,0xD3,0xD4,0xD5,0xD6,0xD7,0xD8,0xD9,0x01)) {
            $i += 2
        } else {
            if ($i + 3 -lt $buf.Length) {
                $segLen = ([int]$buf[$i + 2] -shl 8) -bor [int]$buf[$i + 3]
                $i += [Math]::Max(2, 2 + $segLen)
            } else { break }
        }
    }
    return $buf
}

function Convert-Ozj([string]$ozjPath, [string]$outPath) {
    $raw = [System.IO.File]::ReadAllBytes($ozjPath)

    # Collect all FF D8 FF offsets (outer wrapper at 0, actual image at +24, thumbnail later)
    [int[]]$offsets = @()
    for ($i = 0; $i -le $raw.Length - 3; $i++) {
        if ($raw[$i] -eq 0xFF -and $raw[$i+1] -eq 0xD8 -and $raw[$i+2] -eq 0xFF) {
            $offsets += [int]$i
        }
    }
    if ($offsets.Length -eq 0) { throw "No JPEG magic bytes found" }

    $bestImg  = $null
    $bestArea = 0

    for ($oi = 0; $oi -lt $offsets.Length; $oi++) {
        [int]$off = $offsets[$oi]
        [int]$len = $raw.Length - $off
        $ms = New-Object System.IO.MemoryStream($raw, $off, $len, $false)
        try {
            $img = [System.Drawing.Image]::FromStream($ms)
            # Copy to standalone Bitmap — System.Drawing requires the source stream to stay
            # open for the lifetime of the Image, so we detach immediately via DrawImage.
            $copy = New-Object System.Drawing.Bitmap($img.Width, $img.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [System.Drawing.Graphics]::FromImage($copy)
            try { $g.DrawImage($img, 0, 0) } finally { $g.Dispose() }
            $img.Dispose()
            [int]$area = $copy.Width * $copy.Height
            if ($area -gt $bestArea) {
                if ($null -ne $bestImg) { $bestImg.Dispose() }
                $bestImg = $copy; $bestArea = $area
            } else { $copy.Dispose() }
        } catch { }
        finally { $ms.Dispose() }
    }

    # Fallback: DQT index patch (WebZen sometimes uses non-standard table indices)
    if ($null -eq $bestImg) {
        for ($oi = 0; $oi -lt $offsets.Length; $oi++) {
            [int]$off = $offsets[$oi]
            [int]$len = $raw.Length - $off
            $patched = Repair-JpegDqt $raw $off $len
            $ms = New-Object System.IO.MemoryStream($patched, 0, $patched.Length, $false)
            try {
                $img = [System.Drawing.Image]::FromStream($ms)
                $copy = New-Object System.Drawing.Bitmap($img.Width, $img.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $g = [System.Drawing.Graphics]::FromImage($copy)
                try { $g.DrawImage($img, 0, 0) } finally { $g.Dispose() }
                $img.Dispose()
                [int]$area = $copy.Width * $copy.Height
                if ($area -gt $bestArea) {
                    if ($null -ne $bestImg) { $bestImg.Dispose() }
                    $bestImg = $copy; $bestArea = $area
                } else { $copy.Dispose() }
            } catch { }
            finally { $ms.Dispose() }
        }
    }

    if ($null -eq $bestImg) { throw "Could not decode any JPEG in the file" }
    try { Save-Bitmap ([System.Drawing.Bitmap]$bestImg) $outPath }
    finally { $bestImg.Dispose() }
}

# ── TerrainMapping (.map) → layer1/layer2/alpha PNGs ─────────────────────────
# Format (unencrypted dev assets — same auto-detect pattern as ATT):
#   1 byte  version (0x00)
#   256×256 Layer1  — index into terrain texture array (main layer)
#   256×256 Layer2  — index into terrain texture array (blend layer)
#   256×256 Alpha   — blend weight Layer1→Layer2 (0 = full Layer1, 255 = full Layer2)

function Resolve-MapBuffer([byte[]]$buf) {
    if ($buf[0] -ne 0) { return $null }
    $data = 3 * 256 * 256
    foreach ($hdr in @(1, 2)) {
        if ($buf.Length -eq $hdr + $data) { return $hdr }
    }
    return $null
}

function Convert-TerrainMap([string]$mapPath, [string]$outDir) {
    $raw = [System.IO.File]::ReadAllBytes($mapPath)

    $hdrSize = Resolve-MapBuffer $raw
    $decrypted = $raw
    if ($null -eq $hdrSize) {
        $decrypted = Invoke-MapFileDecrypt $raw
        $hdrSize   = Resolve-MapBuffer $decrypted
    }
    if ($null -eq $hdrSize) { throw "Terrain.map: unrecognised format ($($raw.Length) bytes)" }

    $stride = 256 * 256
    $bmpL1 = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmpL2 = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmpAl = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 256; $y++) {
            for ($x = 0; $x -lt 256; $x++) {
                $base = $hdrSize + $y * 256 + $x
                $v1 = [int]$decrypted[$base]
                $v2 = [int]$decrypted[$base + $stride]
                $va = [int]$decrypted[$base + $stride * 2]
                $bmpL1.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v1, $v1, $v1))
                $bmpL2.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v2, $v2, $v2))
                $bmpAl.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $va, $va, $va))
            }
        }
        Save-Bitmap $bmpL1 (Join-Path $outDir "mapping_layer1.png")
        Save-Bitmap $bmpL2 (Join-Path $outDir "mapping_layer2.png")
        Save-Bitmap $bmpAl (Join-Path $outDir "mapping_alpha.png")
        Write-Host "  mapping_layer1/2/alpha.png (256x256, hdr=$hdrSize bytes)"
    } finally { $bmpL1.Dispose(); $bmpL2.Dispose(); $bmpAl.Dispose() }
}

# ── main ──────────────────────────────────────────────────────────────────────

$maps = if ($Map) { @($Map) } else {
    Get-ChildItem $InputDir -Directory | Where-Object {
        (Get-ChildItem $_.FullName -Filter "*.att" -EA SilentlyContinue |
         Where-Object { $_.Length -ge (256*256) }).Count -gt 0
    } | Select-Object -ExpandProperty Name
}

if (-not $maps) { Write-Error "No maps found in '$InputDir'."; exit 1 }
Write-Host "Maps to process: $($maps -join ', ')"

foreach ($mapName in $maps) {
    $mapDir = Join-Path $InputDir $mapName
    if (-not (Test-Path $mapDir)) { Write-Warning "[$mapName] Directory not found"; continue }

    $outMapDir = Join-Path $OutputDir $mapName
    New-Item -ItemType Directory -Force $outMapDir | Out-Null
    Write-Host "`n[$mapName] Processing..."

    # ATT → walkable.png  (prefer non-encrypted Terrain.att)
    $attFile = Get-ChildItem $mapDir -Filter "Terrain.att" | Select-Object -First 1
    if (-not $attFile) { $attFile = Get-ChildItem $mapDir -Filter "*.att" | Where-Object { $_.Name -notlike "Enc*" } | Select-Object -First 1 }
    if (-not $attFile) { $attFile = Get-ChildItem $mapDir -Filter "*.att" | Select-Object -First 1 }
    if ($attFile) {
        try {
            Convert-Att $attFile.FullName (Join-Path $outMapDir "walkable.png")
            Write-Host "  walkable.png (256x256) from $($attFile.Name)"
        } catch { Write-Warning "  ATT failed: $_" }
    } else { Write-Host "  [SKIP] No .att file" }

    # Terrain.map → mapping_layer1/2/alpha.png
    $mapFile = Get-ChildItem $mapDir -Filter "Terrain.map" | Select-Object -First 1
    if (-not $mapFile) { $mapFile = Get-ChildItem $mapDir -Filter "*.map" | Where-Object { $_.Name -notlike "Enc*" } | Select-Object -First 1 }
    if (-not $mapFile) { $mapFile = Get-ChildItem $mapDir -Filter "*.map" | Select-Object -First 1 }
    if ($mapFile) {
        try { Convert-TerrainMap $mapFile.FullName $outMapDir }
        catch { Write-Warning "  TerrainMap failed: $_" }
    } else { Write-Host "  [SKIP] No .map file" }

    # OZB → heightmap.png
    $ozbFile = Get-ChildItem $mapDir | Where-Object { $_.Extension -ieq ".ozb" } | Select-Object -First 1
    if ($ozbFile) {
        try { Convert-Ozb $ozbFile.FullName (Join-Path $outMapDir "heightmap.png") }
        catch { Write-Warning "  OZB failed: $_" }
    } else { Write-Host "  [SKIP] No .ozb file" }

    # Textures: build stem→{ozj, ozt} map, try OZJ first, fall back to OZT
    $byExt = @{}
    Get-ChildItem $mapDir | Where-Object { $_.Extension -imatch "^\.(ozj|ozt)$" } | ForEach-Object {
        $stem = $_.BaseName
        $ext  = $_.Extension.ToLower()
        if (-not $byExt.ContainsKey($stem)) { $byExt[$stem] = @{} }
        $byExt[$stem][$ext] = $_.FullName
    }
    $texOk = 0; $texFail = 0
    foreach ($stem in ($byExt.Keys | Sort-Object)) {
        $outTex = Join-Path $outMapDir "texture_$stem.png"
        $ok = $false
        foreach ($ext in @(".ozj", ".ozt")) {
            if (-not $byExt[$stem].ContainsKey($ext)) { continue }
            try {
                if ($ext -eq ".ozt") { Convert-Ozt $byExt[$stem][$ext] $outTex }
                else                  { Convert-Ozj $byExt[$stem][$ext] $outTex }
                $texOk++; $ok = $true; break
            } catch {
                if ($ext -eq ".ozj" -and $byExt[$stem].ContainsKey(".ozt")) {
                    # silently retry with OZT
                } else {
                    Write-Warning "  $stem$ext`: $_"; $texFail++
                }
            }
        }
        if (-not $ok -and -not $byExt[$stem].ContainsKey(".ozt")) { }  # already warned above
    }
    Write-Host "  textures: $texOk exported, $texFail failed"
}

Write-Host "`nDone."