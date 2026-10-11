#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 3 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_ROOT PINNED_SOURCE_ROOT OUTPUT' >&2; exit 2; fi
OBEDIENCE_CONTENT=$(realpath "$1")
OBEDIENCE_SOURCE=$(realpath "$2")
OBEDIENCE_OUTPUT=$(realpath -m "$3")
OBEDIENCE_PROJECT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
source "$OBEDIENCE_CONTENT/scripts/env.sh"
cd "$OBEDIENCE_PROJECT"
if [[ -e "$OBEDIENCE_OUTPUT" ]]; then echo "Output already exists: $OBEDIENCE_OUTPUT" >&2; exit 2; fi
mkdir -p "$OBEDIENCE_OUTPUT"
python3 tests/Game.SourceObedience.Tests/source_probe.py "$OBEDIENCE_SOURCE" "$OBEDIENCE_OUTPUT"
flock -w 240 /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceObedience.Tests --configfile tests/Game.SourceObedience.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false
flock -w 240 /tmp/firered-remake-build.lock dotnet build tests/Game.SourceObedience.Tests --no-restore -m:1 -p:UseSharedCompilation=false
dotnet tests/Game.SourceObedience.Tests/bin/Debug/net10.0/Game.SourceObedience.Tests.dll "$OBEDIENCE_CONTENT" "$OBEDIENCE_OUTPUT"
