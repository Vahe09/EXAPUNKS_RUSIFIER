param(
    [string]$ManualDir = "C:\GOG Games\EXAPUNKS\Content\manual",
    [string]$OutputDir = "C:\GOG Games\EXAPUNKS\RU_Exapunks_Rusifier\manual_ocr"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Runtime.WindowsRuntime
[void][Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
[void][Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]
[void][Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
[void][Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
[void][Windows.Media.Ocr.OcrEngine,Windows.Media.Ocr,ContentType=WindowsRuntime]
[void][Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]

$asTask1 = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and
        $_.IsGenericMethodDefinition -and
        $_.GetGenericArguments().Count -eq 1 -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1

$asTask0 = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq 'AsTask' -and
        -not $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1

$ocr = [Windows.Media.Ocr.OcrEngine,Windows.Media.Ocr,ContentType=WindowsRuntime]::TryCreateFromLanguage(
    [Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]::new('en-US')
)

function Invoke-AsyncGeneric {
    param(
        [System.Reflection.MethodInfo]$Method,
        [Type]$TypeArg,
        [object]$Operation
    )
    return $Method.MakeGenericMethod($TypeArg).Invoke($null, @($Operation)).Result
}

function Get-OcrPageText {
    param(
        [object]$Document,
        [int]$PageIndex
    )

    $page = $Document.GetPage($PageIndex)
    try {
        $stream = [Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]::new()
        try {
            $asTask0.Invoke($null, @($page.RenderToStreamAsync($stream))).Wait()
            $stream.Seek(0) | Out-Null
            $decoder = Invoke-AsyncGeneric `
                -Method $asTask1 `
                -TypeArg ([Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]) `
                -Operation ([Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]::CreateAsync($stream))
            $bitmap = Invoke-AsyncGeneric `
                -Method $asTask1 `
                -TypeArg ([Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime]) `
                -Operation ($decoder.GetSoftwareBitmapAsync())
            $result = Invoke-AsyncGeneric `
                -Method $asTask1 `
                -TypeArg ([Windows.Media.Ocr.OcrResult,Windows.Media.Ocr,ContentType=WindowsRuntime]) `
                -Operation ($ocr.RecognizeAsync($bitmap))
            return ($result.Text -replace "`r", "").Trim()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $page.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Get-ChildItem -LiteralPath $ManualDir -Filter *.pdf | Sort-Object Name | ForEach-Object {
    $pdfPath = $_.FullName
    $baseName = $_.BaseName
    $outPath = Join-Path $OutputDir ($baseName + ".txt")

    $file = Invoke-AsyncGeneric `
        -Method $asTask1 `
        -TypeArg ([Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]) `
        -Operation ([Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]::GetFileFromPathAsync($pdfPath))

    $doc = Invoke-AsyncGeneric `
        -Method $asTask1 `
        -TypeArg ([Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]) `
        -Operation ([Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]::LoadFromFileAsync($file))

    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $doc.PageCount; $i++) {
        [void]$builder.AppendLine(("===== PAGE " + ($i + 1) + " ====="))
        [void]$builder.AppendLine((Get-OcrPageText -Document $doc -PageIndex $i))
        [void]$builder.AppendLine("")
    }

    [System.IO.File]::WriteAllText($outPath, $builder.ToString(), [System.Text.Encoding]::UTF8)
    Write-Host ("[OCR] " + $outPath)
}
