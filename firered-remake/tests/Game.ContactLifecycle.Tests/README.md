# Four-position contact lifecycle

This focused suite migrates Poison Point, Rough Skin, Flame Body and Effect Spore
through `SourceRecipeContext.ContactAbility`. The singles and frame adapters call
that same recipe. Static retains its existing path, and Cute Charm retains its
existing shared recipe. No species, encounter, asset, campaign or presentation
admission is added.

The product snapshot is `2c3e33e6d6b29a84c0b890d066d52d3899016b43`. The original
reference is pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
The 24 immutable canonical singles state/event/RNG SHA-256 values were captured
before any product edit, using the original product snapshot. They cover ordinary
hits against all six contact ability families at four fixed seeds. The existing
FourContact suite separately retains 30 historical whole-turn hashes.

Run with the official SDK and the authenticated original input roots:

```sh
bash tests/Game.ContactLifecycle.Tests/run.sh PRODUCT_ROOT PINNED_PRET_ROOT NEW_EVIDENCE_DIRECTORY
```

Only the three focused test projects compile. The shared build lock is held for
build commands only; original C execution and managed test runs hold no lock.
`source_probe.py` executes no reference-repository scripts and authenticates every
source file against the pinned commit before extracting C. It compiles original
contact cases, status gates/application, RNG, cancellation and HP update with
bounded controller/script stubs, using both optimized C and UBSan.

Expected focused result: 171 original C vectors; 165 C# frame comparisons;
six explicitly original-only inputs for unavailable Uproar/confusion-callback
phases; 25 real four-action turns; 24 immutable ordinary canonical singles hashes.
The separate existing FourContact suite covers 156 source vectors, 19 turns and
30 immutable historical hashes. The original SourceContactAbsorb suite checks
canonical singles contact/absorption regressions.

The frame differential covers all twelve distinct attacker/holder position pairs,
retained physical/special receipt versus current command damage, no-effect flags,
no-contact, attacker/holder HP guards, status/type/ability immunity, Shield Dust,
Safeguard, own Substitute, source chance/division/reroll order, all sleep durations
and repeated eligible callbacks. The direct command vectors are mechanics fixtures;
mutated stats/overlays are not claimed as admitted campaign identities. Genuine
kernel/turn cases use authenticated factory identities and genuine learned moves.

Actual four-action cases cover both sides and both partners, exact active-party
HP/status synchronization, transient frame serialization, later sleep cancellation,
per-hit callbacks, contact sleep cancelling an actual Jigglypuff Rollout lock, sleep/passive KO stopping multi-hit, source final-phase omission
of a duplicate callback, reward continuation, queued faint cancellation, and two
deferred same-species replacements with distinct new occupant tokens. Confusion
self-damage is additionally checked through a real admitted action. Unsupported
Color Change, fire-thaw and forged permanent abilities still reject atomically.

See `docs/source-four-contact-lifecycle.md` for the exact singles compatibility and
future Bide boundary. The original C host is not a ROM, emulator, native window,
full script engine or campaign test.
