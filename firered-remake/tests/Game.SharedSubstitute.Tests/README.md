# Shared Substitute focused proof

`run.sh PRODUCT_ROOT PINNED_REFERENCE FRESH_EVIDENCE_DIRECTORY` regenerates authenticated original-C vectors, checks the frozen vector and singles hashes, then builds/runs only this small .NET 10 executable. Short builds respect `/tmp/firered-remake-build.lock`; no GUI or full old suite is used.

The fixed pre-change baseline is `1a3f6ae58b999beddcb2e1e284b0e5791753670b`. `singles-baseline-1a3f6ae.json` contains 32 complete canonical singles action-result hashes (state/events/RNG), captured before production edits. The `baseline` mode refuses to replace an existing file. Never regenerate expected hashes to follow new code.

The source-only probe has 81 vectors / 131 observations, 34 fixed assertions, 34 source contracts and 13 bypass checks. See `SOURCE-PROBE.md` for exact fixture boundaries. The product compares 28 creation/interception vectors via the real common command. Other vectors describe source invariants, not an assertion that all their unadmitted callers or deliberately stale states execute through the public kernel.

Public tests cover actual four-slot creation and cost failures, PP/attack-string order, wrapping release, two real turns with four identical species, breaking-hit status/stat/confusion/flinch blocking, script bypasses, multihit contact, Focus Band, delayed damage, residual bypasses, exact Baton transfer, ordinary/forced switching, and one residual faint/reward/replacement. Continuations are serialized and replayed for identical results. Invalid Substitute records, unrelated called state, callers/Transform, and four-slot King's Rock/Shell Bell reject atomically.

Content/species/move factories use the authenticated full source catalog. Explicit extreme-HP command fixtures and the stored delayed-attack fixture are distinguished from reached full-turn paths. This suite does not claim campaign, native presentation, animation, audio or exhaustive move acceptance.
