# Encore / Imprison focused tests

`Program.cs` is the authored input corpus. The first 24 cases use Encore and
Imprison, seeds 0/31/127, and valid/no-history/forbidden/empty-PP target histories.
They compare complete serialized state, events, invocation and RNG against the
pre-edit frozen DLL output in `singles-baseline.json`. Capture refuses an assembly
containing the new recipe; the baseline is not regenerated to fit the candidate.
The baseline helper was compiled against copied, hash-recorded Domain/Content
DLLs, so no extra baseline Domain compilation was needed.

The remaining focused cases cover:

- all four actor positions and true previous move/PP-slot identity
- invalid and unavailable history, first-slot/zero-PP failure, duration RNG
- Encore's Substitute and Protect commands
- opposing versus allied Imprison, both Pressure positions and failure PP
- actual Transform and permanent Sketch changing live seal moves
- full four-action Encore and Imprison turns, forced Struggle, subsequent turns
- exact JSON round-trip continuation states/events/RNG
- PP expiry, retained learning identity and overwritten pending Encore action
- KO, replacement and resume; ordinary switch cleanup; earned Struggle retention
- unsupported caller gating and atomic unresolved standalone Encore rejection

`source_probe.py` checks the fixed source SHA, extracts four unchanged C function
bodies and compiles them with a small authored command-boundary harness. It runs
nine Encore inputs (all four duration residues, forbidden/unavailable history,
zero PP, existing Encore, duplicate first-match with zero PP) and six Imprison
inputs (opposite-flank overlap, no overlap, ally-only overlap, two Pressure users,
existing Imprison, Pressure plus no overlap). `source-vectors.json` retains the
results and exact extracted-function hashes. No upstream scripts are executed.

Run from a selected checkout using the existing authenticated content and toolchain:

```sh
bash tests/Game.SharedEncoreImprison.Tests/run.sh \
  /path/to/content-root /path/to/pokefirered /new/evidence-directory
```

Only restore/build holds `/tmp/firered-remake-build.lock`; source probes and .NET
runtime tests run outside it. The final report measures the hosted .NET 10 runtime.
Frozen baseline helper compile errors and a too-fragile low-HP switch fixture
were fixed before delivery; they were test setup errors, not replaced golden data.
The final review added a kernel guard for unresolved Encore selections and ran the
necessary incremental build. No historical campaign or native suite was rerun.
