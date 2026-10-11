#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
if [[ $# != 3 || -e "$3" ]]; then echo 'run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT' >&2; exit 2; fi
content_root="$(realpath "$1")"
reference_root="$(realpath "$2")"
output_root="$(realpath -m "$3")"
mkdir -p "$output_root"
source scripts/env.sh
cat > "$output_root/nuget-offline.config" <<'XML'
<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear /></packageSources></configuration>
XML
python3 tests/Game.SharedCopyState.Tests/source_probe.py "$reference_root" "$output_root/oracle" > "$output_root/source.log" 2>&1
python3 tests/Game.SharedCopyState.Tests/ability_probe.py "$reference_root" "$output_root/ability-oracle" >> "$output_root/source.log" 2>&1
python3 tests/Game.CopyMoveRules.Tests/source_probe.py "$reference_root" "$output_root/copy-source-contracts.json" >> "$output_root/source.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.SharedCopyState.Tests/Game.SharedCopyState.Tests.csproj --configfile "$output_root/nuget-offline.config" --locked-mode -p:NuGetAudit=false > "$output_root/restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedCopyState.Tests/Game.SharedCopyState.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false > "$output_root/build.log" 2>&1
dotnet tests/Game.SharedCopyState.Tests/bin/Debug/net10.0/Game.SharedCopyState.Tests.dll "$content_root" "$output_root/result.json" "$output_root/oracle/optimized.json" "$output_root/ability-oracle/optimized.json" > "$output_root/run.log" 2>&1
