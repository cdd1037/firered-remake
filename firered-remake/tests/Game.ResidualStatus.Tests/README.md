# Bounded shared residual statuses

Run from the candidate root:

```sh
bash tests/Game.ResidualStatus.Tests/run.sh PRODUCT_CONTENT_ROOT PINNED_PRET_ROOT NEW_EVIDENCE_DIRECTORY
```

The script uses the official .NET 10 toolchain from the product's `.tools/toolchain-root` (or `FIRERED_TOOLCHAIN_ROOT`), empty local NuGet sources for these BCL-only projects, and `/tmp/firered-remake-build.lock` for short restore/build commands. No campaign or GUI runner is invoked.

`legacy-baseline.json` was captured before extraction from a byte-hashed copy of main HEAD `e911e56` with the already-applied Conversion/Color Change work. It stores SHA-256 fingerprints of 31 whole canonical singles results, including state, typed events, RNG and residual cursor. The baseline mode refuses to overwrite existing output. The normal mode verifies those same fingerprints and compares 24 application cases through the singles frame bridge.

`source_probe.py` extracts unchanged C from pin `037335f4c725d7c9aecdac87066f2002b4bd7e14`, compares source bytes with git's pinned versions, and compiles a local host scaffold. Its full 7,648-row output contains broader inputs (including Substitute) than four-slot admission. The C# test consumes only admitted application and residual cases; raw helper tests deliberately vary HP bounds outside catalog stats to cover exact division/clamping. Complete-turn tests use actual authenticated Pokémon. `source-manifest.json` freezes source and fragment hashes and the complete vector-file hash. Regeneration must match the snapshot. Both generated JSON and the final test report are parsed immediately.

The oracle is not an emulator: it does not execute generic accuracy, full faint scripts, EXP or replacement callbacks. Script evidence records ordering; selected real C# turns verify those boundaries. The source absent guard on `tryfaintmon` is read from pinned `battle_script_commands.c:2867`; the oracle's faint columns only observe HP. The test explicitly adds the source absence condition before comparing actual faint callbacks.

Supported: Leech Seed, Ingrain, actual slot/source storage, clamped drain/heal, Liquid Ooze double KO, immediate source/target switches, rooted/Roar behavior, Baton transfer, real EXP and deferred KO entries, exact serialized resume. Not enabled: Nightmare, Curse, Yawn, Uproar, Substitute, move callers or Transform. See `docs/source-four-residual-status.md` for accuracy compatibility details.
