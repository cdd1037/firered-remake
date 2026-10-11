#!/usr/bin/env bash
set -euo pipefail
[[ $# -eq 3 ]] || { echo 'Usage: run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT' >&2; exit 2; }
content_root="$(realpath "$1")"
reference_root="$(realpath "$2")"
output="$(realpath -m "$3")"
project_root="$(cd "$(dirname "$0")/../.." && pwd)"
[[ ! -e "$output" ]] || { echo 'Use a new evidence directory.' >&2; exit 2; }
mkdir -p "$output"
source "$content_root/scripts/env.sh"
cd "$project_root"
config="$content_root/.toolhome/recovery-local-only.config"
python3 tests/Game.FourSwitching.Tests/build-source-oracle.py "$reference_root" "$output"
for suite in Game.FourSwitching.Tests Game.FourSwitchingParity.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "tests/$suite" --configfile "$config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$output/$suite-build.log" 2>&1
done
dotnet tests/Game.FourSwitching.Tests/bin/Debug/net10.0/Game.FourSwitching.Tests.dll "$content_root" "$reference_root" "$output/source-switch-oracle" > "$output/four-switching.log"
dotnet tests/Game.FourSwitchingParity.Tests/bin/Debug/net10.0/Game.FourSwitchingParity.Tests.dll "$content_root" "$output/current.jsonl" > "$output/parity-current.log"
# Fixed previous production source, no copied content or toolchain.
mkdir -p "$output/baseline/tests/Game.FourSwitchingParity.Tests"
git -C "$content_root" archive 92a781df4b1a81966d643adad69c7eaf3e2cfbfd Directory.Build.props global.json NuGet.Config src/Game.Domain src/Game.Content | tar -x -C "$output/baseline"
cp tests/Game.FourSwitchingParity.Tests/Program.cs tests/Game.FourSwitchingParity.Tests/Game.FourSwitchingParity.Tests.csproj tests/Game.FourSwitchingParity.Tests/packages.lock.json "$output/baseline/tests/Game.FourSwitchingParity.Tests/"
flock /tmp/firered-remake-build.lock dotnet build "$output/baseline/tests/Game.FourSwitchingParity.Tests" --configfile "$config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$output/parity-baseline-build.log" 2>&1
dotnet "$output/baseline/tests/Game.FourSwitchingParity.Tests/bin/Debug/net10.0/Game.FourSwitchingParity.Tests.dll" "$content_root" "$output/baseline.jsonl" > "$output/parity-baseline.log"
cmp "$output/baseline.jsonl" "$output/current.jsonl"
sha256sum "$output/baseline.jsonl" "$output/current.jsonl" > "$output/parity.sha256"
tail -n 1 "$output/four-switching.log"
cat "$output/parity.sha256"
