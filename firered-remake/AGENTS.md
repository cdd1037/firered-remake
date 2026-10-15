# FireRed remake engineering contract

## Outcome and gates
- Target a complete single-player FireRed recreation: Kanto, main story, reachable post-game and side content. The first badge is a milestone, not final scope.
- Hard baseline: official Godot 4.7.2 .NET, target net10.0, actual hosted runtime major 10. Never substitute another engine or runtime without approval.
- Development SDK policy: stable .NET 10.0 SDK >= 10.0.100, with feature-band/patch roll-forward. The Linux download manifest is a historical toolchain baseline, not an exact SDK requirement. Discover installed Windows/Linux tools through `dev`; do not require cross-platform Godot package content hashes to match.
- P0 remains incomplete until actual audio output is verified. The user has authorized continued complete-game development while this gap stays explicit. Advance region by region with source and playable-flow checks; never call the full P0/audio gate passed from the Dummy backend. Keep failed checks and never label skipped work passed.
- No ROM downloads, public distribution or upload of Pokémon assets. Source research is local and pinned.

## Architecture
Five product assemblies: Game.Domain (BCL-only deterministic rules), Game.Application (Domain), Game.Content (Domain), Game.Godot (composition/presentation), Game.Tooling (headless commands).
Use stable string identifiers, authoritative command transactions, typed events, explicit RNG state and canonical hashes. Godot animations never determine game rules.

## Work discipline
Read README.md and docs/ before changes. One owner per file. Verify actual compiler/runtime and include full commands and evidence. Do not execute scripts from third-party reference repositories. Keep downloaded toolchains outside the project. Tests must fail on empty suites, missing data, incomplete evidence and unsupported operations. Never rewrite golden results merely to match implementation.
