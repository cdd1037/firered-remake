#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
source scripts/env.sh
mkdir -p tests/Game.CalledPartyEffects.Tests/evidence
cc -std=c11 -Wall -Wextra -Werror tests/Game.CalledPartyEffects.Tests/source-oracle.c -o /tmp/called-party-source-oracle
/tmp/called-party-source-oracle > /tmp/called-party-source-vectors.json
cmp tests/Game.CalledPartyEffects.Tests/source-vectors.json /tmp/called-party-source-vectors.json
for suite in Game.CalledPartyEffects.Tests Game.CalledMoveKernel.Tests Game.CalledTurnLocks.Tests Game.CalledVolatileEffects.Tests Game.TransformBattle.Tests Game.FuchsiaPhysicalMoves.Tests Game.FuchsiaSecondaryBoundary.Tests; do
  log="tests/Game.CalledPartyEffects.Tests/evidence/$suite"
  if ! flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build "tests/$suite/$suite.csproj" --configfile .toolhome/recovery-local-only.config -m:1 -nr:false > "$log.build.log" 2>&1; then tail -60 "$log.build.log"; exit 1; fi
  arguments=(.)
  if [[ "$suite" == Game.TransformBattle.Tests ]]; then arguments=(. .); fi
  if ! "$DOTNET_ROOT/dotnet" "tests/$suite/bin/Debug/net10.0/$suite.dll" "${arguments[@]}" > "$log.results.log" 2>&1; then tail -80 "$log.results.log"; exit 1; fi
  tail -1 "$log.results.log"
done
