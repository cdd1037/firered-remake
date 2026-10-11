# Shared two-turn charge moves

One shared source recipe now owns Fly, Bounce, Dig, Dive, Solar Beam, Razor Wind,
Sky Attack and Skull Bash for canonical singles and the retained four-slot owner.
The singles adapter supplies storage/event compatibility and its existing scalar
commands. Four-slot storage uses the existing position-typed charge lock, PP slot,
retained target and entry serial. No parallel called-only charge implementation remains.

The test freezes 24 complete canonical singles charge/release/normal-next-turn
state/event/RNG hashes before extraction. It reuses, by path and SHA256, 28 bounded
rows of the existing independent original-C damage/RNG vectors. A compact new
source probe authenticates the pinned checkout, extracts eight exact C commands
or cases, verifies 14 script/ordering facts, and compares 26 immutable vectors
under optimized and UBSan builds. Captures refuse to overwrite existing vectors.

## Exact source details

- All eight priority values are zero. A retained PP position whose learned move
  changes uses that current learned move's priority; execution still uses the lock.
- Setup spends PP once (Pressure included), without accuracy/damage RNG. Release
  accepts zero PP and suppresses PP reduction, including misses or Protect.
- Source Solar Beam checks effective sun before the existing-lock check and before
  attack cancellation. Sun appearing on release therefore spends another PP.
  Rain/sand/hail halve its base damage before the final +2; Cloud Nine/Air Lock
  suppress weather at the source checkpoints.
- First-turn Protect permits charge; release Protect cancels it. Skull Bash raises
  Defense during charge. Sky Attack gets its source higher critical stage and
  flinch effect; Bounce gets paralysis. Razor Wind uses two foe passes on release.
- Semi-invulnerability is derived from the authoritative charge lock. Source hit
  exceptions and multipliers are shared. Surf/Whirlpool already have shared move
  scripts and now reach Dive targets. Gust/Twister/Thunder/Sky Uppercut/Earthquake/
  Magnitude remain behind their existing four-slot recipe gates; the exception
  table does not claim those unported scripts are admitted. Lock-On's existing
  four-slot lifecycle gate also remains closed.
- Flinch, Disable, confusion self-hit, infatuation, Truant and full paralysis clear
  charge and its semi state. Freeze infliction clears it; a frozen action retains
  it. Sleeping charge cancels before weather at the end-turn boundary.
- Retained targets are positions; current replacement occupants and source absent
  fallback are respected. Under the approved deferred-KO policy, a no-target
  release clears its charge without a second PP cost. Forced departure clears
  the actor lock immediately. KO replacement remains an end-turn responsibility.

## Corrected compatibility expectation

The old called-charge regression treated the FRLG C canceller comment as evidence
that full paralysis retains charge. The actual BattleScript_MoveUsedIsParalyzed
executes cancelmultiturnmoves, which clears MULTIPLETURNS and all semi bits.
The implementation and that old charge assertion now follow the actual script.
The old normal-turn hashes are unchanged. This is a remake correction, not an
original-game bug choice. Other repeated-move families remain outside this slice.

## Run

bash tests/Game.SharedChargeMoves.Tests/run.sh PRODUCT_ROOT PINNED_PRET_ROOT NEW_OUTPUT

Uses the existing authenticated content tree by path, local official NuGet config,
actual .NET 10, and /tmp/firered-remake-build.lock. No GUI/campaign run is involved.
The parent owns solution/aggregate registration and completion-matrix integration.
