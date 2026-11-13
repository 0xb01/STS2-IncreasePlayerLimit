#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${ROOT_DIR}/build"
RELEASE_DIR="${BUILD_DIR}/IncreasePlayerLimit"
DLL_SOURCE="${ROOT_DIR}/.godot/mono/temp/bin/Release/IncreasePlayerLimit.dll"
PCK_SOURCE="${BUILD_DIR}/IncreasePlayerLimit.pck"
MANIFEST_PATH="${ROOT_DIR}/IncreasePlayerLimit.json"

echo "Building IncreasePlayerLimit (.NET Release)..."
dotnet build "${ROOT_DIR}/IncreasePlayerLimit.csproj" -c Release

if command -v godot >/dev/null 2>&1; then
  echo "Packing PCK via Godot CLI..."
  godot --headless --path "${ROOT_DIR}" --script "res://tools/build_pck.gd"
elif command -v python3 >/dev/null 2>&1; then
  echo "Packing PCK via Python 3..."
  python3 "${ROOT_DIR}/tools/pack_pck.py"
elif command -v python >/dev/null 2>&1; then
  echo "Packing PCK via Python..."
  python "${ROOT_DIR}/tools/pack_pck.py"
fi

mkdir -p "${RELEASE_DIR}"
rm -rf "${RELEASE_DIR:?}"/*

cp "${DLL_SOURCE}" "${RELEASE_DIR}/IncreasePlayerLimit.dll"
cp "${PCK_SOURCE}" "${RELEASE_DIR}/IncreasePlayerLimit.pck"
cp "${MANIFEST_PATH}" "${RELEASE_DIR}/IncreasePlayerLimit.json"

VERSION="$(python3 -c "import json; print(json.load(open('${MANIFEST_PATH}'))['version'])" 2>/dev/null || python -c "import json; print(json.load(open('${MANIFEST_PATH}'))['version'])")"

# Remove existing zip outputs
rm -f "${BUILD_DIR}/IncreasePlayerLimit-"*.zip

# --- Package 1: Windows ---
STAGE_WIN="${BUILD_DIR}/_zip_stage_win"
rm -rf "${STAGE_WIN}"
mkdir -p "${STAGE_WIN}/IncreasePlayerLimit"
cp -r "${RELEASE_DIR}"/* "${STAGE_WIN}/IncreasePlayerLimit/"

cat << 'EOF' > "${STAGE_WIN}/Install.bat"
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0helper.ps1"
pause
EOF

cat << 'EOF' > "${STAGE_WIN}/helper.ps1"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$host.UI.RawUI.WindowTitle = 'IncreasePlayerLimit Installer'

Write-Host '============================================'
Write-Host '  IncreasePlayerLimit (by 0xb1)'
Write-Host '  One-Click Windows Mod Installer'
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
EOF

(cd "${STAGE_WIN}" && zip -r "${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-Windows.zip" .)
rm -rf "${STAGE_WIN}"
echo "Built Windows package: ${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-Windows.zip"

# --- Package 2: macOS ---
STAGE_MAC="${BUILD_DIR}/_zip_stage_mac"
rm -rf "${STAGE_MAC}"
mkdir -p "${STAGE_MAC}/IncreasePlayerLimit"
cp -r "${RELEASE_DIR}"/* "${STAGE_MAC}/IncreasePlayerLimit/"

cat << 'EOF' > "${STAGE_MAC}/install.sh"
#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "============================================"
echo "  IncreasePlayerLimit (by 0xb1)"
echo "  macOS Mod Installer"
echo "============================================"
echo ""

MOD_SRC="IncreasePlayerLimit"
if [ ! -d "$MOD_SRC" ] || [ ! -f "$MOD_SRC/IncreasePlayerLimit.dll" ]; then
    echo "[ERROR] Missing IncreasePlayerLimit folder or binaries."
    echo "Please ensure this script is run from the extracted zip archive."
    exit 1
fi

echo "Locating Slay the Spire 2 installation..."

DEFAULT_STEAM_PATH="$HOME/Library/Application Support/Steam"
GAME_DIR=""

if [ -d "$DEFAULT_STEAM_PATH/steamapps/common/Slay the Spire 2" ]; then
    GAME_DIR="$DEFAULT_STEAM_PATH/steamapps/common/Slay the Spire 2"
fi

if [ -z "$GAME_DIR" ] && [ -f "$DEFAULT_STEAM_PATH/steamapps/libraryfolders.vdf" ]; then
    while IFS= read -r line; do
        if [[ $line =~ \"path\"[[:space:]]+\"([^\"]+)\" ]]; then
            CANDIDATE="${BASH_REMATCH[1]}/steamapps/common/Slay the Spire 2"
            if [ -d "$CANDIDATE" ]; then
                GAME_DIR="$CANDIDATE"
                break
            fi
        fi
    done < "$DEFAULT_STEAM_PATH/steamapps/libraryfolders.vdf"
fi

if [ -n "$GAME_DIR" ]; then
    echo "Found game directory: $GAME_DIR"

    APP_BUNDLE="$GAME_DIR/SlayTheSpire2.app"
    if [ -d "$APP_BUNDLE" ]; then
        DEST_DIR="$APP_BUNDLE/Contents/MacOS/mods/IncreasePlayerLimit"
        STEAM_APPID_FILE="$APP_BUNDLE/Contents/MacOS/steam_appid.txt"
    else
        DEST_DIR="$GAME_DIR/mods/IncreasePlayerLimit"
        STEAM_APPID_FILE="$GAME_DIR/steam_appid.txt"
    fi

    mkdir -p "$DEST_DIR"
    cp -R "$MOD_SRC/"* "$DEST_DIR/"

    if [ ! -f "$STEAM_APPID_FILE" ]; then
        echo "2868840" > "$STEAM_APPID_FILE"
        echo "Created steam_appid.txt (bypasses direct launch Steam errors)."
    fi

    echo ""
    echo "============================================"
    echo "  Installation successful!"
    echo "============================================"
    echo "Installed to: $DEST_DIR"
    echo ""
    echo "Note: On Apple Silicon, ensure the game is opened using Rosetta 2:"
    echo "  Right-click SlayTheSpire2.app > Get Info > check 'Open using Rosetta'"
else
    echo "============================================"
    echo "  Auto-detection failed"
    echo "============================================"
    echo ""
    echo "Could not locate Slay the Spire 2 automatically."
    echo "Please copy the 'IncreasePlayerLimit' folder manually to:"
    echo "  <Slay the Spire 2>/SlayTheSpire2.app/Contents/MacOS/mods/IncreasePlayerLimit/"
fi

echo ""
read -n 1 -s -r -p "Press any key to close..." || true
echo ""
EOF

cat << 'EOF' > "${STAGE_MAC}/Install.command"
#!/usr/bin/env bash
cd "$(dirname "$0")"
chmod +x ./install.sh 2>/dev/null || true
./install.sh
EOF

chmod +x "${STAGE_MAC}/install.sh" "${STAGE_MAC}/Install.command"
(cd "${STAGE_MAC}" && zip -r "${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-macOS.zip" .)
rm -rf "${STAGE_MAC}"
echo "Built macOS package:   ${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-macOS.zip"

# --- Package 3: Linux ---
STAGE_LINUX="${BUILD_DIR}/_zip_stage_linux"
rm -rf "${STAGE_LINUX}"
mkdir -p "${STAGE_LINUX}/IncreasePlayerLimit"
cp -r "${RELEASE_DIR}"/* "${STAGE_LINUX}/IncreasePlayerLimit/"

cat << 'EOF' > "${STAGE_LINUX}/install.sh"
#!/usr/bin/env bash
set -e

cd "$(dirname "$0")"

echo "============================================"
echo "  IncreasePlayerLimit (by 0xb1)"
echo "  Linux / Steam Deck Mod Installer"
echo "============================================"
echo ""

MOD_SRC="IncreasePlayerLimit"
if [ ! -d "$MOD_SRC" ] || [ ! -f "$MOD_SRC/IncreasePlayerLimit.dll" ]; then
    echo "[ERROR] Missing IncreasePlayerLimit folder or binaries."
    echo "Please ensure this script is run from the extracted zip archive."
    exit 1
fi

echo "Locating Slay the Spire 2 installation..."

CANDIDATES=(
    "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2"
    "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
    "$HOME/.steam/root/steamapps/common/Slay the Spire 2"
    "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Slay the Spire 2"
    "/run/media/mmcblk0p1/steamapps/common/Slay the Spire 2"
)

GAME_DIR=""
for c in "${CANDIDATES[@]}"; do
    if [ -d "$c" ]; then
        GAME_DIR="$c"
        break
    fi
done

if [ -z "$GAME_DIR" ]; then
    VDF_PATHS=(
        "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf"
        "$HOME/.steam/steam/steamapps/libraryfolders.vdf"
        "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/libraryfolders.vdf"
    )
    for vdf in "${VDF_PATHS[@]}"; do
        if [ -f "$vdf" ]; then
            while IFS= read -r line; do
                if [[ $line =~ \"path\"[[:space:]]+\"([^\"]+)\" ]]; then
                    cand="${BASH_REMATCH[1]}/steamapps/common/Slay the Spire 2"
                    if [ -d "$cand" ]; then
                        GAME_DIR="$cand"
                        break 2
                    fi
                fi
            done < "$vdf"
        fi
    done
fi

if [ -n "$GAME_DIR" ]; then
    echo "Found game directory: $GAME_DIR"
    DEST_DIR="$GAME_DIR/mods/IncreasePlayerLimit"
    mkdir -p "$DEST_DIR"
    cp -R "$MOD_SRC/"* "$DEST_DIR/"

    echo ""
    echo "============================================"
    echo "  Installation successful!"
    echo "============================================"
    echo "Installed to: $DEST_DIR"
else
    echo "============================================"
    echo "  Auto-detection failed"
    echo "============================================"
    echo ""
    echo "Could not locate Slay the Spire 2 automatically."
    echo "Please copy the 'IncreasePlayerLimit' folder manually to:"
    echo "  <Slay the Spire 2>/mods/IncreasePlayerLimit/"
fi

echo ""
read -n 1 -s -r -p "Press any key to close..." || true
echo ""
EOF

chmod +x "${STAGE_LINUX}/install.sh"
(cd "${STAGE_LINUX}" && zip -r "${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-Linux.zip" .)
rm -rf "${STAGE_LINUX}"
echo "Built Linux package:   ${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}-Linux.zip"

# --- Package 4: Universal ---
STAGE_UNIV="${BUILD_DIR}/_zip_stage_univ"
rm -rf "${STAGE_UNIV}"
mkdir -p "${STAGE_UNIV}/IncreasePlayerLimit"
cp -r "${RELEASE_DIR}"/* "${STAGE_UNIV}/IncreasePlayerLimit/"
(cd "${STAGE_UNIV}" && zip -r "${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}.zip" .)
rm -rf "${STAGE_UNIV}"
echo "Built Universal package: ${BUILD_DIR}/IncreasePlayerLimit-v${VERSION}.zip"
