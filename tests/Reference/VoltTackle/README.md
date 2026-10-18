# Volt Tackle extracted-source oracle

Explicit local command:

```
python tests/Reference/VoltTackle/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/volt-tackle-source-vectors.json
```

This extracts original orb-slide, five-mode raw-bolt task/child, reappearance,
CreateSprite, CreateSpriteAndAnimate, AnimateSprites and frame/affine programs
from pret/pokefirered037335f4c725d7c9aecdac87066f2002b4bd7e14. All original inputs
are pinned. It does not execute upstream scripts or use managed runtime code to
produce expectations. Numerical JSON, generated C and binaries remain local ignored
artifacts. Normal tests never compile C. Existing reviewed fixtures are not overwritten.

The102 cases /3172 rows cover six single/double actor-target layouts, all five
sweep modes, visible/hidden actor flags, retained x2/y2 and real64-cell allocation
pressure: all failures, only two free cells, and capacity freed during a sweep.
The explicit generator was run at O0 and O2; every native output was byte-identical.

Frame0 is original orb initializer plus AnimateSprite, or original task state0.
Later frames run AnimateSprites followed by the original task callback. Successful
raw bolts own only their parent task's data7 counter; callback13 decrements it and
uses raw DestroySprite after freeing its affine matrix. Failed allocation still
advances the task's x and cycling field. That cycling field is never supplied to
StartSpriteAnim in the source: every child uses animation0. All four source frame
variants remain in imported data instead of inventing animation selection.

Platform coordinate/species lookup uses standard pinned positions, source picture
Y offset0 and the player+8 adjustment. Numeric battler sprite IDs are handles in
this family, unlike the deferred Punch task. Hardware affine matrix allocation
and BIOS output are explicit stubs; original affine scale/rotation/end state is
observed. Native matrix indices are not compared to the managed scene allocator.
Each raw bolt uses its own shared managed affine player and disappears at the
source lifetime; no frame-vector test claims exhausted hardware matrix behavior.

The managed root preflight conservatively reserves four battler matrices and22
ordinary scene objects. Each linear sweep has at most17 total attempts across
-16..256 in16-pixel steps, no more than two per task callback. The counted task
waits for all successful raw children, so waitforvisualfinish safely discharges
its bound. Root peak is19 sprite slots /17 matrices with puffs included. Foreign
retained objects/matrices count against that bound before any effect runs.
Primitive capacity-pressure tests independently exercise failure ownership.

Complete-root managed tests compare raw birth totals to these native cases and
exercise compositor output, callback transforms/visibility, deterministic replay,
separate pre-attachment idle restoration and cancellation. They do not claim a
full-root native scheduler trace or actual audio-device output.

## Gameplay entry boundary

The pinned daycare.c comments out both GiveVoltTackleIfLightBall(1006–1017) and its
call(1076–1077). No active level-up/egg acquisition is claimed. The direct tests
explicitly install a controlled typed source move slot and execute the public
kernel, checking hit/substitute/recoil ownership. Separate genuine Metronome344
selection proves a public source invocation and preserves the existing denial of
called leaves. Product counting requires the shared authenticated called-leaf
presentation contract, not a Volt Tackle exception or a fabricated natural learner.
