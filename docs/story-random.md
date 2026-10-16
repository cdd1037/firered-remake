# Shared script random command

`random max` is compiled as `StoryOp.Random` for every script. The interpreter
resolves its u16 operand with the same variable/constant rules as other story
commands and yields a native effect before any dependent branch runs.
`StoryRandomRules` consumes exactly one draw from the existing authoritative
`World.Rng`, stores the high-16-bit output modulo `max` in `VAR_RESULT`, and
returns the advanced RNG. The application applies both values in the same
command transaction and resumes through the ordinary native-effect pump.

No second random stream, actor-specific random handler, new save field, or
presentation-driven draw is introduced. Zero bounds, malformed instructions,
stale effect callbacks, and exhausted RNG cursors fail instead of producing a
fallback result. Preflight runs on immutable throwaway state.

Coverage is in `StoryRandomTests` and `RandomNpcInteractionTests`: literals and
variables, compiler arity, invalid bounds, exact modulo sequence, resumed story
state, preflight/replay, generated Cerulean Lass/Slowbro and S.S. Anne Chef4
branches, and public interactions with those actors. Test execution status
belongs in the batch verification report; this document does not claim native
window or audio acceptance.

The source operation is `ScrCmd_random` in the pinned FireRed source
`src/scrcmd.c`; generation uses the normal `scripts/imports/story/import-story.py`
entry point. Runtime reads generated IR only.

## Verified 2026-10-16

Environment: Linux .NET SDK 10.0.401, target net10.0. The shared batch serial
solution build completed with zero warnings/errors before the focused run.

The ordinary Python importer's `prepare()` produced the input supplied to:

```sh
source scripts/env.sh
dotnet tools/import-story/bin/Debug/net10.0/import-story.dll "$PWD" < /tmp/firered-random-story-input.json
dotnet tools/import-story/bin/Debug/net10.0/import-story.dll "$PWD" --check < /tmp/firered-random-story-input.json
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~StoryRandomTests|FullyQualifiedName~RandomNpcInteractionTests' --logger 'console;verbosity=normal'
```

Generation and unchanged-resource verification both reported 4,229 programs and
4,283 text labels. Focused tests passed 13/13. An earlier run passed 12/13:
Chef4's controlled fixture incorrectly used the representative checkpoint's
post-departure ship state. The fixture now places the ship before departure,
retaining real HM/rival ownership and history; normal player-save validation
and the public interaction pass. No production admission rule was relaxed.

The three public interaction cases use controlled validated actor-placement
fixtures, not a replay of natural travel to those actors. They exercise public
`interact` and acknowledgement commands, stale-command rejection, completion,
unlock, branch result and the exact one-draw world RNG transition. These results
do not establish native visual or audio acceptance.
