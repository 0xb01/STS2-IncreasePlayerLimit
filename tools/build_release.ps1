$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$godotCmd = Get-Command godot -ErrorAction SilentlyContinue
$godot = if ($godotCmd) { $godotCmd.Source } else { $null }
$buildRoot = Join-Path $root "build"
$releaseDir = Join-Path $root "build\IncreasePlayerLimit"
$dllSource = Join-Path $root ".godot\mono\temp\bin\Release\IncreasePlayerLimit.dll"
$pckSource = Join-Path $root "build\IncreasePlayerLimit.pck"
$manifestPath = Join-Path $root "IncreasePlayerLimit.json"

& $dotnet build (Join-Path $root "IncreasePlayerLimit.csproj") -c Release

if ($godot -and (Test-Path $godot)) {
    & $godot --headless --path $root --script "res://tools/build_pck.gd"
} else {
    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) {
        & python (Join-Path $root "tools\pack_pck.py")
    } else {
        Write-Warning "Neither Godot nor Python found to pack PCK."
    }
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

Get-ChildItem -Path $releaseDir -Force | Remove-Item -Recurse -Force
Get-ChildItem -Path $buildRoot -Filter "IncreasePlayerLimit-*.zip" -File -ErrorAction SilentlyContinue | Remove-Item -Force

Copy-Item $dllSource -Destination (Join-Path $releaseDir "IncreasePlayerLimit.dll") -Force
Copy-Item $pckSource -Destination (Join-Path $releaseDir "IncreasePlayerLimit.pck") -Force
Copy-Item $manifestPath -Destination (Join-Path $releaseDir "IncreasePlayerLimit.json") -Force

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$version = [string]$manifest.version
$modFolderName = if ([string]::IsNullOrWhiteSpace([string]$manifest.pck_name)) { [string]$manifest.name } else { [string]$manifest.pck_name }
if ([string]::IsNullOrWhiteSpace($version)) { throw "IncreasePlayerLimit.json missing version field" }
if ([string]::IsNullOrWhiteSpace($modFolderName)) { throw "IncreasePlayerLimit.json missing name/pck_name field" }

$zipName = "IncreasePlayerLimit-v$version.zip"
$zipPath = Join-Path $buildRoot $zipName
$zipStageRoot = Join-Path $buildRoot "_zip_stage"
$zipModFolder = Join-Path $zipStageRoot $modFolderName

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
if (Test-Path $zipStageRoot) { Remove-Item $zipStageRoot -Recurse -Force }

New-Item -ItemType Directory -Force -Path $zipModFolder | Out-Null
Copy-Item (Join-Path $releaseDir "*") -Destination $zipModFolder -Recurse -Force

@'
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0helper.ps1"
pause
'@ | Set-Content (Join-Path $zipStageRoot "Install.bat") -Encoding ASCII

@'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$host.UI.RawUI.WindowTitle = 'IncreasePlayerLimit Installer'

Write-Host '============================================'
Write-Host '  IncreasePlayerLimit (by 0xb1)'
Write-Host '  One-Click Mod Installer'
Write-Host '============================================'
Write-Host ''

$src = $PSScriptRoot
$modFolder = Join-Path $src 'IncreasePlayerLimit'
$dll  = Join-Path $modFolder 'IncreasePlayerLimit.dll'
$pck  = Join-Path $modFolder 'IncreasePlayerLimit.pck'
$json = Join-Path $modFolder 'IncreasePlayerLimit.json'

$missing = @()
if (-not (Test-Path $dll))  { $missing += 'IncreasePlayerLimit.dll' }
if (-not (Test-Path $pck))  { $missing += 'IncreasePlayerLimit.pck' }
if (-not (Test-Path $json)) { $missing += 'IncreasePlayerLimit.json' }

if ($missing.Count -gt 0) {
    Write-Host '[ERROR] Missing mod files:' -ForegroundColor Red
    foreach ($f in $missing) { Write-Host "  - $f" -ForegroundColor Red }
    Write-Host ''
    Write-Host 'Please ensure this script is in the same directory as the IncreasePlayerLimit folder.'
    exit 1
}

Write-Host 'Locating Slay the Spire 2 installation...'
Write-Host ''

$sp = $null
try { $sp = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -EA Stop).InstallPath } catch {}
if (-not $sp) {
    try { $sp = (Get-ItemProperty 'HKCU:\SOFTWARE\Valve\Steam' -EA Stop).SteamPath } catch {}
}

$gp = $null
if ($sp) {
    $vdf = Join-Path $sp 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($line in Get-Content $vdf) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $p = $Matches[1].Replace('\\', '\')
                $c = Join-Path $p 'steamapps\common\Slay the Spire 2'
                if (Test-Path $c) { $gp = $c; break }
            }
        }
    }
    if (-not $gp) {
        $c = Join-Path $sp 'steamapps\common\Slay the Spire 2'
        if (Test-Path $c) { $gp = $c }
    }
}

if ($gp) {
    Write-Host "Found game directory: $gp" -ForegroundColor Green
    Write-Host ''

    $dest = Join-Path $gp 'mods\IncreasePlayerLimit'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $modFolder '*') -Destination $dest -Recurse -Force

    Write-Host '============================================'
    Write-Host '  Installation successful!' -ForegroundColor Green
    Write-Host '============================================'
    Write-Host ''
    Write-Host "Installed to: $dest"
} else {
    Write-Host '============================================'
    Write-Host '  Auto-detection failed' -ForegroundColor Red
    Write-Host '============================================'
    Write-Host ''
    Write-Host 'Could not locate Slay the Spire 2 automatically.'
    Write-Host 'Please copy the "IncreasePlayerLimit" directory manually to:'
    Write-Host '  <Slay the Spire 2>\mods\IncreasePlayerLimit\'
}

Write-Host ''
'@ | Set-Content (Join-Path $zipStageRoot "helper.ps1") -Encoding UTF8

Compress-Archive -Path (Join-Path $zipStageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item $zipStageRoot -Recurse -Force
Write-Host "Built release package: $zipPath"
