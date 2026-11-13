#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${ROOT_DIR}/build"
RELEASE_DIR="${BUILD_DIR}/IncreasePlayerLimit"
DLL_SOURCE="${ROOT_DIR}/.godot/mono/temp/bin/Release/IncreasePlayerLimit.dll"
PCK_SOURCE="${BUILD_DIR}/IncreasePlayerLimit.pck"
MANIFEST_PATH="${ROOT_DIR}/IncreasePlayerLimit.json"

dotnet build "${ROOT_DIR}/IncreasePlayerLimit.csproj" -c Release

if command -v godot >/dev/null 2>&1; then
  godot --headless --path "${ROOT_DIR}" --script "res://tools/build_pck.gd"
elif command -v python3 >/dev/null 2>&1; then
  python3 "${ROOT_DIR}/tools/pack_pck.py"
elif command -v python >/dev/null 2>&1; then
  python "${ROOT_DIR}/tools/pack_pck.py"
fi

mkdir -p "${RELEASE_DIR}"
rm -rf "${RELEASE_DIR:?}"/*

cp "${DLL_SOURCE}" "${RELEASE_DIR}/IncreasePlayerLimit.dll"
cp "${PCK_SOURCE}" "${RELEASE_DIR}/IncreasePlayerLimit.pck"
cp "${MANIFEST_PATH}" "${RELEASE_DIR}/IncreasePlayerLimit.json"

VERSION="$(python3 -c "import json; print(json.load(open('${MANIFEST_PATH}'))['version'])" 2>/dev/null || python -c "import json; print(json.load(open('${MANIFEST_PATH}'))['version'])")"
ZIP_NAME="IncreasePlayerLimit-v${VERSION}.zip"
ZIP_PATH="${BUILD_DIR}/${ZIP_NAME}"
STAGE_DIR="${BUILD_DIR}/_zip_stage"

rm -f "${ZIP_PATH}"
rm -rf "${STAGE_DIR}"
mkdir -p "${STAGE_DIR}/IncreasePlayerLimit"
cp -r "${RELEASE_DIR}"/* "${STAGE_DIR}/IncreasePlayerLimit/"

(cd "${STAGE_DIR}" && zip -r "${ZIP_PATH}" .)
rm -rf "${STAGE_DIR}"

echo "Built release package: ${ZIP_PATH}"
