# Punch trace: native callbacks and approved initialization

Mach Punch and Shadow Punch share unchanged source task/clone trajectory semantics: five outward attempts, four return attempts, raw children with eight callbacks, then palette release after the child counter reaches zero. The local fixture contains 10 deliberately selected cases / 173 full callback rows and verifies O0/O2 parity.

The initializer is an approved presentation policy, not native equivalence. It preserves the actor's actual x2 instead of subtracting an allocator-dependent sprite ID. It takes the actor's normal color state before tinting instead of blending a freed palette bank's historical normal colors. The native initializer is executed unchanged in the oracle and its historical offset/palette remain separate fields. The authored harness then sets the explicit safe boundary before invoking the unchanged source callbacks. Tests name these distinctions rather than calling the entire initializer original-equivalent.

The runtime uses an immutable RGB555 palette transform over detached actor pixels, including its legitimate normal-state prefix. Transform/load colors and sequential faded-to-normal rounding remain intact. This is exact for opaque pixels under the reviewed no-later-clone-palette-writes closure; it does not reconstruct unavailable native index numbers. The source palette tag still has one task owner. Copies are raw scene sprites and cannot hold the global visual count. The shared battler compositor, source affine matrices and pre-task OAM phase remain authoritative.

Explicit preparation from the repo root (output must not already exist):

```
python tests/Reference/PunchTrace/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/punch-trace-source-vectors.json
```

Generated JSON/C/binaries are local evidence only. Keep only this README, generator, harness and pinned-input hashes in Git. Ordinary tests do not regenerate expected vectors. Cases cover all four positions, both sides, hidden-present actors, nonidentity native sprite IDs, free capacity 0/1/8, signed16 overflow, actual shifted base positions, raw lifetime and cell reuse. Managed tests separately cover immutable palette/material retention, affine snapshots, full-root pixel composition, cancellation and genuine controlled kernel dispatch. None establishes full-route play, native Godot display, or audible output.
