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

# ── ATT → walkable.png ───────────────────────────────────────────────────────

function Convert-Att([string]$attPath, [string]$outPath) {
    $raw = [System.IO.File]::ReadAllBytes($attPath)
    $dataSize   = 256 * 256
    $headerSize = $raw.Length - $dataSize
    if ($headerSize -lt 0) { throw "ATT too small: $($raw.Length) bytes" }

    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 256; $y++) {
            for ($x = 0; $x -lt 256; $x++) {
                $tile = $raw[$headerSize + $x + $y * 256]
                $v = switch ($tile) { 0 { 255 } 1 { 128 } default { 0 } }
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

function Find-JpegOffset([byte[]]$raw) {
    for ($i = 0; $i -le $raw.Length - 3; $i++) {
        if ($raw[$i] -eq 0xFF -and $raw[$i+1] -eq 0xD8 -and $raw[$i+2] -eq 0xFF) { return $i }
    }
    return -1
}

function Convert-Ozj([string]$ozjPath, [string]$outPath) {
    $raw = [System.IO.File]::ReadAllBytes($ozjPath)
    $off = Find-JpegOffset $raw
    if ($off -lt 0) { throw "No JPEG magic bytes found" }

    $jpegLen = $raw.Length - $off
    $ms = New-Object System.IO.MemoryStream($raw, $off, $jpegLen, $false)
    try {
        $img = [System.Drawing.Image]::FromStream($ms)
        try { Save-Bitmap ([System.Drawing.Bitmap]$img) $outPath }
        finally { $img.Dispose() }
    } finally { $ms.Dispose() }
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