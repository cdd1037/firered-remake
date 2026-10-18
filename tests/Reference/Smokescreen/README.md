# Smokescreen source reference

`generate.py` extracts the pinned standalone `src/battle_anim_smokescreen.c`, its wrapper and smoke callbacks, original shared projectile/arc/linear helpers, sprite frame engine, coordinate Y helper and source script delay/wait handlers. It builds an authored bounded harness at O0 and O2 and rejects differing results. It does not execute upstream scripts or managed product code.

Explicit inputs are in `cases.json`. The ordinary nonpersistent raw impact cases are separate from two native-only observations (`persist=true` and an already-loaded sheet). The managed closure deliberately does not admit those two modes or overlapping tag owners. Resource stubs record original load/free calls; they do not model native heap/VRAM exhaustion. Managed validation rejects unproved logical-sprite and OAM capacity before effects begin.

Tick -1 means the helper has explicitly called AnimateSprite once for each quadrant. Normal move Cmd_createvisualtask invokes its initializer synchronously before AnimateSprites/BuildOamBuffer, so tick 0 includes the second frame-player update and is visible in that frame. The children finish their frame program at tick 11, destroy at tick 12, then the earlier-slot invisible parent frees both tags and destroys at tick 13. General animations running after OAM retain the existing separate scene phase behavior.

The six full-root schedules use original projectile callbacks and source wait/delay dispatch. Their narrow pointer-free command wrappers preserve source argument/register data; sound calls are logged and no audio is considered playing. They therefore prove callback and command timing, not audible duration or hardware raster equivalence.

Generate local ignored output explicitly:

```
python tests/Reference/Smokescreen/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/smokescreen-source-vectors.json
```

The generator refuses to overwrite an existing fixture. Every expected trajectory remains outside Git; only source extraction, source identities and explicit cases are tracked.
