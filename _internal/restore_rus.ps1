Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[RUS-RESTORE] $Message"
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$parentConfig = Join-Path (Split-Path -Parent $scriptRoot) "config.json"
$configPath = if (Test-Path -LiteralPath $parentConfig) {
    $parentConfig
} else {
    Join-Path $scriptRoot "config.json"
}
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$gameDir = [System.IO.Path]::GetFullPath($config.game_dir)
$backupRoot = Join-Path $gameDir $config.backup_dir_name

if (-not (Test-Path -LiteralPath $backupRoot)) {
    throw "Backup directory not found: $backupRoot"
}

$latest = Get-ChildItem -LiteralPath $backupRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $latest) {
    throw "No backups found."
}

Get-ChildItem -LiteralPath $latest.FullName -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($latest.FullName.Length).TrimStart('\')
    $destination = Join-Path $gameDir $relative
    $destinationDir = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    Write-Info "Restored: $relative"
}

$cleanupTargets = @(
    (Join-Path $gameDir "EXAPUNKS.original.exe"),
    (Join-Path $gameDir "EXAPUNKS.runtime.tsv"),
    (Join-Path $gameDir "EXAPUNKS.runtime.log")
)

foreach ($target in $cleanupTargets) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Force
        Write-Info "Removed: $target"
    }
}

Write-Info "Restore complete. Backup used: $($latest.FullName)"
