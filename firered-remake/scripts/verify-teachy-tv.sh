#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
source scripts/env.sh
reference_root="${1:-${FIRERED_ORIGINAL_REFERENCE:-}}"
if [[ -z "$reference_root" ]]; then echo 'Supply the pinned pokefirered checkout path.' >&2; exit 2; fi
report_root="${2:-evidence/teachy-tv-focused}"
mkdir -p "$report_root"
python3 scripts/import-teachy-tv.py --git-dir "$reference_root/.git" > "$report_root"/import.json
python3 scripts/import-teachy-tv-art.py --git-dir "$reference_root/.git" > "$report_root"/art-import.json
python3 scripts/test-teachy-tv-importer.py --git-dir "$reference_root/.git" > "$report_root"/importer-tests.json
flock --close -w 180 /tmp/firered-remake-build.lock dotnet build tests/Game.TeachyTv.Tests/Game.TeachyTv.Tests.csproj --nologo -m:1 -nr:false -p:UseSharedCompilation=false --configfile "$FIRERED_TOOLCHAIN_ROOT/NuGet.OfficialLocal.Config" > "$report_root"/build.log 2>&1
dotnet tests/Game.TeachyTv.Tests/bin/Debug/net10.0/Game.TeachyTv.Tests.dll content/generated/teachy-tv/teachy-tv.json > "$report_root"/rules-tests.json
printf 'Teachy TV catalog, pixels and outer-rule focused checks passed.\n'
