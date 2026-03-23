Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[RUS] $Message"
}

function Read-Config {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "config.json not found: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ConfigPath {
    param([string]$ScriptRoot)
    $parentConfig = Join-Path (Split-Path -Parent $ScriptRoot) "config.json"
    if (Test-Path -LiteralPath $parentConfig) {
        return $parentConfig
    }

    return Join-Path $ScriptRoot "config.json"
}

function Assert-GameDir {
    param([string]$GameDir)
    $required = @(
        "EXAPUNKS.exe",
        "Content",
        "PackedContent"
    )
    foreach ($item in $required) {
        $target = Join-Path $GameDir $item
        if (-not (Test-Path -LiteralPath $target)) {
            throw "Game directory is invalid. Missing: $target"
        }
    }
}

function New-BackupRoot {
    param(
        [string]$GameDir,
        [string]$BackupDirName
    )
    $root = Join-Path $GameDir $BackupDirName
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $session = Join-Path $root $timestamp
    New-Item -ItemType Directory -Force -Path $session | Out-Null
    return $session
}

function Backup-FileIfNeeded {
    param(
        [string]$GameDir,
        [string]$BackupRoot,
        [string]$RelativePath
    )
    $source = Join-Path $GameDir $RelativePath
    if (-not (Test-Path -LiteralPath $source)) {
        return
    }
    $backupTarget = Join-Path $BackupRoot $RelativePath
    $backupDir = Split-Path -Parent $backupTarget
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    if (-not (Test-Path -LiteralPath $backupTarget)) {
        Copy-Item -LiteralPath $source -Destination $backupTarget -Force
    }
}

function Copy-PayloadTree {
    param(
        [string]$PayloadRoot,
        [string]$GameDir,
        [string]$BackupRoot
    )
    if (-not (Test-Path -LiteralPath $PayloadRoot)) {
        return
    }
    Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($PayloadRoot.Length).TrimStart('\')
        if ($relative.StartsWith("textures\")) {
            return
        }
        if ($relative.StartsWith("half_textures\")) {
            return
        }
        if ($relative.StartsWith("payload\")) {
            return
        }
        Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath $relative
        $destination = Join-Path $GameDir $relative
        $destinationDir = Split-Path -Parent $destination
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        Write-Info "Copied: $relative"
    }
}

function Patch-OriginalExeStrings {
    param(
        [string]$ScriptRoot,
        [string]$GameDir,
        [string]$OriginalExe
    )

    $patcherSource = Join-Path $ScriptRoot "data\PatchOriginalExe.cs"
    $cecilDll = Join-Path $GameDir "Mono.Cecil.dll"
    if (-not (Test-Path -LiteralPath $patcherSource) -or -not (Test-Path -LiteralPath $cecilDll)) {
        return
    }

    $compiler = Get-CSharpCompilerPath
    $tempExe = Join-Path $GameDir "__ru_patch_original.exe"
    try {
        & $compiler /nologo /target:exe /platform:anycpu /optimize+ /r:$cecilDll /out:$tempExe $patcherSource
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempExe)) {
            throw "Original EXE patcher compilation failed."
        }

        & $tempExe $OriginalExe
        if ($LASTEXITCODE -ne 0) {
            throw "Original EXE patcher failed."
        }

        Write-Info "Patched EXAPUNKS.exe string source."
    }
    finally {
        if (Test-Path -LiteralPath $tempExe) {
            Remove-Item -LiteralPath $tempExe -Force
        }
    }
}

function Initialize-TexTools {
    if ("ExapunksTexTools" -as [type]) {
        return
    }

    Add-Type -AssemblyName System.Drawing

    $source = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ExapunksTexTools
{
    private static byte[] Compress(byte[] input)
    {
        if (input == null)
        {
            throw new ArgumentNullException("input");
        }

        using (MemoryStream stream = new MemoryStream(input.Length + (input.Length / 255) + 32))
        {
            int literalLength = input.Length;
            byte token = (byte)(Math.Min(literalLength, 15) << 4);
            stream.WriteByte(token);

            if (literalLength >= 15)
            {
                int remaining = literalLength - 15;
                while (remaining >= 255)
                {
                    stream.WriteByte(255);
                    remaining -= 255;
                }
                stream.WriteByte((byte)remaining);
            }

            stream.Write(input, 0, input.Length);
            return stream.ToArray();
        }
    }

    private static byte[] LoadScaledRgba(string pngPath, int width, int height)
    {
        using (Bitmap source = new Bitmap(pngPath))
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));

            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = canvas.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] bgra = new byte[stride * height];
                Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
                byte[] rgba = new byte[width * height * 4];

                // TEX pixel rows are stored bottom-up, so flip vertically when packing.
                for (int y = 0; y < height; y++)
                {
                    int flippedY = height - 1 - y;
                    for (int x = 0; x < width; x++)
                    {
                        int sourceIndex = (y * stride) + (x * 4);
                        int targetIndex = ((flippedY * width) + x) * 4;
                        rgba[targetIndex + 0] = bgra[sourceIndex + 2];
                        rgba[targetIndex + 1] = bgra[sourceIndex + 1];
                        rgba[targetIndex + 2] = bgra[sourceIndex + 0];
                        rgba[targetIndex + 3] = bgra[sourceIndex + 3];
                    }
                }

                return rgba;
            }
            finally
            {
                canvas.UnlockBits(data);
            }
        }
    }

    public static void SaveFromTemplate(string templateTexPath, string outputTexPath, string pngPath)
    {
        byte[] template = File.ReadAllBytes(templateTexPath);
        if (template.Length < 60)
        {
            throw new InvalidOperationException("Template TEX file is too short: " + templateTexPath);
        }

        int width = BitConverter.ToInt32(template, 4);
        int height = BitConverter.ToInt32(template, 8);
        byte[] headerPrefix = new byte[56];
        Buffer.BlockCopy(template, 0, headerPrefix, 0, headerPrefix.Length);

        byte[] rgba = LoadScaledRgba(pngPath, width, height);
        byte[] compressed = Compress(rgba);

        string outDir = Path.GetDirectoryName(outputTexPath);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        using (FileStream stream = File.Create(outputTexPath))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(headerPrefix);
            writer.Write(compressed.Length);
            writer.Write(compressed);
        }
    }
}
"@

    Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @("System.dll", "System.Drawing.dll")
}

function Get-TexTargetPath {
    param(
        [string]$GameDir,
        [string]$RelativeTexPath,
        [switch]$Half
    )

    if ($Half) {
        return Join-Path $GameDir ("PackedContent\half\" + $RelativeTexPath)
    }
    return Join-Path $GameDir ("PackedContent\" + $RelativeTexPath)
}

function Get-TexCanvasSize {
    param([string]$TexPath)
    $bytes = [System.IO.File]::ReadAllBytes($TexPath)
    if ($bytes.Length -lt 12) {
        throw "TEX file is too short: $TexPath"
    }
    $width = [BitConverter]::ToInt32($bytes, 4)
    $height = [BitConverter]::ToInt32($bytes, 8)
    if ($width -le 0 -or $height -le 0) {
        throw "Failed to read TEX canvas size: $TexPath"
    }
    return [pscustomobject]@{
        Width = $width
        Height = $height
    }
}

function Parse-HexColor {
    param([string]$Color)
    $raw = $Color.TrimStart('#')
    if ($raw.Length -eq 6) {
        return [System.Drawing.Color]::FromArgb(
            255,
            [Convert]::ToInt32($raw.Substring(0, 2), 16),
            [Convert]::ToInt32($raw.Substring(2, 2), 16),
            [Convert]::ToInt32($raw.Substring(4, 2), 16)
        )
    }
    if ($raw.Length -eq 8) {
        return [System.Drawing.Color]::FromArgb(
            [Convert]::ToInt32($raw.Substring(0, 2), 16),
            [Convert]::ToInt32($raw.Substring(2, 2), 16),
            [Convert]::ToInt32($raw.Substring(4, 2), 16),
            [Convert]::ToInt32($raw.Substring(6, 2), 16)
        )
    }
    throw "Invalid color value: $Color"
}

function New-TextTexturePng {
    param(
        [string]$OutputPng,
        [int]$Width,
        [int]$Height,
        [string]$Text,
        [string]$Background,
        [string]$Foreground,
        [string]$Border,
        [string]$FontName,
        [float]$FontSize,
        [string]$Align,
        [int]$Padding
    )

    Add-Type -AssemblyName System.Drawing

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.Clear((Parse-HexColor $Background))

        if ($Border) {
            $pen = New-Object System.Drawing.Pen (Parse-HexColor $Border), 3
            try {
                $graphics.DrawRectangle($pen, 1, 1, $Width - 3, $Height - 3)
            }
            finally {
                $pen.Dispose()
            }
        }

        $font = New-Object System.Drawing.Font($FontName, $FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $brush = New-Object System.Drawing.SolidBrush (Parse-HexColor $Foreground)
        $format = New-Object System.Drawing.StringFormat

        switch ($Align) {
            "near" { $format.Alignment = [System.Drawing.StringAlignment]::Near }
            "far" { $format.Alignment = [System.Drawing.StringAlignment]::Far }
            default { $format.Alignment = [System.Drawing.StringAlignment]::Center }
        }
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center

        try {
            $drawWidth = [single]([int]$Width - ([int]$Padding * 2))
            $drawHeight = [single]([int]$Height - ([int]$Padding * 2))
            $rect = New-Object System.Drawing.RectangleF -ArgumentList @(
                [single][int]$Padding,
                [single][int]$Padding,
                $drawWidth,
                $drawHeight
            )
            $graphics.DrawString($Text, $font, $brush, $rect, $format)
        }
        finally {
            $format.Dispose()
            $brush.Dispose()
            $font.Dispose()
        }

        $outDir = Split-Path -Parent $OutputPng
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        $bitmap.Save($OutputPng, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Compile-TextureFromPng {
    param(
        [string]$GameDir,
        [string]$RelativeTexPath,
        [string]$SourcePng,
        [switch]$HalfOnly,
        [switch]$FullOnly
    )

    Initialize-TexTools

    foreach ($half in $false, $true) {
        if ($HalfOnly -and -not $half) {
            continue
        }
        if ($FullOnly -and $half) {
            continue
        }
        $templateTex = Get-TexTargetPath -GameDir $GameDir -RelativeTexPath $RelativeTexPath -Half:$half
        if (-not (Test-Path -LiteralPath $templateTex)) {
            if (-not $half) {
                throw "Missing TEX template: $templateTex"
            }
            continue
        }
        [ExapunksTexTools]::SaveFromTemplate($templateTex, $templateTex, $SourcePng)
    }
}

function Apply-PayloadTexturePngs {
    param(
        [string]$PayloadTextureRoot,
        [string]$GameDir,
        [string]$BackupRoot
    )

    if (-not (Test-Path -LiteralPath $PayloadTextureRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $PayloadTextureRoot -Recurse -File -Filter *.png | ForEach-Object {
        $relativePng = $_.FullName.Substring($PayloadTextureRoot.Length).TrimStart('\')
        $relativeTex = "textures\" + [System.IO.Path]::ChangeExtension($relativePng, ".tex")

        foreach ($backupRelative in @(
            ("PackedContent\" + $relativeTex),
            ("PackedContent\half\" + $relativeTex)
        )) {
            Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath $backupRelative
        }

        Compile-TextureFromPng -GameDir $GameDir -RelativeTexPath $relativeTex -SourcePng $_.FullName
        Write-Info "Rebuilt TEX from PNG: PackedContent\\$relativeTex"
    }
}

function Apply-HalfPayloadTexturePngs {
    param(
        [string]$PayloadHalfTextureRoot,
        [string]$GameDir,
        [string]$BackupRoot
    )

    if (-not (Test-Path -LiteralPath $PayloadHalfTextureRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $PayloadHalfTextureRoot -Recurse -File -Filter *.png | ForEach-Object {
        $relativePng = $_.FullName.Substring($PayloadHalfTextureRoot.Length).TrimStart('\')
        $relativeTex = "textures\" + [System.IO.Path]::ChangeExtension($relativePng, ".tex")

        Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath ("PackedContent\half\" + $relativeTex)

        Compile-TextureFromPng -GameDir $GameDir -RelativeTexPath $relativeTex -SourcePng $_.FullName -HalfOnly
        Write-Info "Rebuilt HALF TEX from PNG: PackedContent\\half\\$relativeTex"
    }
}

function Apply-TextureSpecs {
    param(
        [string]$SpecsPath,
        [string]$GameDir,
        [string]$BackupRoot,
        [bool]$CleanupGeneratedSources
    )

    if (-not (Test-Path -LiteralPath $SpecsPath)) {
        return
    }

    $specs = Get-Content -LiteralPath $SpecsPath -Raw | ConvertFrom-Json
    if (-not $specs -or $specs.Count -eq 0) {
        return
    }

    foreach ($spec in $specs) {
        $relativeTex = $spec.relative_tex
        Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath ("PackedContent\" + $relativeTex)
        Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath ("PackedContent\half\" + $relativeTex)

        $texPath = Get-TexTargetPath -GameDir $GameDir -RelativeTexPath $relativeTex
        $canvas = Get-TexCanvasSize -TexPath $texPath
        $sourcePng = Join-Path $env:TEMP ("exapunks_rus_" + [Guid]::NewGuid().ToString("N") + ".png")

        New-TextTexturePng `
            -OutputPng $sourcePng `
            -Width $canvas.Width `
            -Height $canvas.Height `
            -Text $spec.text `
            -Background $spec.background `
            -Foreground $spec.foreground `
            -Border $spec.border `
            -FontName $spec.font `
            -FontSize $spec.font_size `
            -Align $spec.align `
            -Padding $spec.padding

        Compile-TextureFromPng -GameDir $GameDir -RelativeTexPath $relativeTex -SourcePng $sourcePng
        Write-Info "Rebuilt texture: PackedContent\\$relativeTex"

        if ($CleanupGeneratedSources) {
            Remove-Item -LiteralPath $sourcePng -Force
        }
    }
}

function Compile-WrapperExe {
    param(
        [string]$ScriptRoot,
        [string]$GameDir,
        [string]$LiveExe
    )

    $wrapperSource = Join-Path $ScriptRoot "data\ExapunksWrapper.cs"
    if (-not (Test-Path -LiteralPath $wrapperSource)) {
        throw "Wrapper source not found: $wrapperSource"
    }

    $compiler = Get-CSharpCompilerPath
    $tempExe = Join-Path $GameDir "__ru_wrapper.exe"
    try {
        & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /out:$tempExe $wrapperSource
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempExe)) {
            throw "Wrapper compilation failed."
        }

        Copy-Item -LiteralPath $tempExe -Destination $LiveExe -Force
        Write-Info "Installed wrapper EXAPUNKS.exe."
    }
    finally {
        if (Test-Path -LiteralPath $tempExe) {
            Remove-Item -LiteralPath $tempExe -Force
        }
    }
}

function Get-CSharpCompilerPath {
    $candidates = @(
        "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "C# compiler not found."
}

function Install-PatchedExe {
    param(
        [string]$ScriptRoot,
        [string]$GameDir,
        [string]$BackupRoot
    )

    $liveExe = Join-Path $GameDir "EXAPUNKS.exe"
    $originalExe = Join-Path $GameDir "EXAPUNKS.original.exe"
    $runtimeTarget = Join-Path $GameDir "EXAPUNKS.runtime.tsv"
    $runtimeLog = Join-Path $GameDir "EXAPUNKS.runtime.log"
    $liveIsWrapper = $false
    if (Test-Path -LiteralPath $liveExe) {
        $liveIsWrapper = (Get-Item -LiteralPath $liveExe).Length -lt 200000
    }

    if ($liveIsWrapper) {
        if (-not (Test-Path -LiteralPath $originalExe)) {
            throw "Cannot restore direct EXAPUNKS.exe: EXAPUNKS.original.exe is missing."
        }

        $backupTarget = Join-Path $BackupRoot "EXAPUNKS.exe"
        $backupDir = Split-Path -Parent $backupTarget
        New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
        Copy-Item -LiteralPath $originalExe -Destination $backupTarget -Force
        Copy-Item -LiteralPath $originalExe -Destination $liveExe -Force
        Write-Info "Restored direct EXAPUNKS.exe from EXAPUNKS.original.exe."
    } else {
        Backup-FileIfNeeded -GameDir $GameDir -BackupRoot $BackupRoot -RelativePath "EXAPUNKS.exe"
    }

    Patch-OriginalExeStrings `
        -ScriptRoot $ScriptRoot `
        -GameDir $GameDir `
        -OriginalExe $liveExe

    foreach ($stalePath in @($originalExe, $runtimeTarget, $runtimeLog)) {
        if (Test-Path -LiteralPath $stalePath) {
            Remove-Item -LiteralPath $stalePath -Force
            Write-Info "Removed stale file: $([System.IO.Path]::GetFileName($stalePath))"
        }
    }

    Write-Info "Installed patched EXAPUNKS.exe."
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$config = Read-Config -Path (Get-ConfigPath -ScriptRoot $scriptRoot)
$gameDir = [System.IO.Path]::GetFullPath($config.game_dir)
Assert-GameDir -GameDir $gameDir

$backupRoot = $null
if ($config.create_backup) {
    $backupRoot = New-BackupRoot -GameDir $gameDir -BackupDirName $config.backup_dir_name
    Write-Info "Created backup: $backupRoot"
} else {
    $backupRoot = Join-Path $gameDir $config.backup_dir_name
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
}

Copy-PayloadTree `
    -PayloadRoot (Join-Path $scriptRoot "payload") `
    -GameDir $gameDir `
    -BackupRoot $backupRoot

$previousLocation = Get-Location
try {
    Set-Location $gameDir
    Apply-PayloadTexturePngs `
        -PayloadTextureRoot (Join-Path $scriptRoot "payload\textures") `
        -GameDir $gameDir `
        -BackupRoot $backupRoot
    Apply-HalfPayloadTexturePngs `
        -PayloadHalfTextureRoot (Join-Path $scriptRoot "payload\half_textures") `
        -GameDir $gameDir `
        -BackupRoot $backupRoot
    Apply-TextureSpecs `
        -SpecsPath (Join-Path $scriptRoot "data\ui_textures.json") `
        -GameDir $gameDir `
        -BackupRoot $backupRoot `
        -CleanupGeneratedSources ([bool]$config.cleanup_generated_sources)
}
finally {
    Set-Location $previousLocation
}

Install-PatchedExe `
    -ScriptRoot $scriptRoot `
    -GameDir $gameDir `
    -BackupRoot $backupRoot

Write-Info "Done."
