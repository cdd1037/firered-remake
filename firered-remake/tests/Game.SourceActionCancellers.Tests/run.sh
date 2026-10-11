#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 3 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_ROOT PINNED_SOURCE_ROOT OUTPUT' >&2; exit 2; fi
CANCELLER_CONTENT=$(realpath "$1")
CANCELLER_SOURCE=$(realpath "$2")
CANCELLER_OUTPUT=$(realpath -m "$3")
CANCELLER_PROJECT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
source "$CANCELLER_CONTENT/scripts/env.sh"
cd "$CANCELLER_PROJECT"
mkdir -p "$CANCELLER_OUTPUT"
python3 tests/Game.SourceActionCancellers.Tests/source_probe.py "$CANCELLER_SOURCE" "$CANCELLER_OUTPUT"
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceActionCancellers.Tests --configfile tests/Game.SourceActionCancellers.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceActionCancellers.Tests --no-restore -m:1 -p:UseSharedCompilation=false
dotnet tests/Game.SourceActionCancellers.Tests/bin/Debug/net10.0/Game.SourceActionCancellers.Tests.dll "$CANCELLER_CONTENT" "$CANCELLER_OUTPUT"
