# Assist and Sweet Scent native references

The two generators extract the pinned source rather than executing upstream scripts. Existing source extraction and sprite-engine helpers are reused. No parallel script parser or runtime support table is added.

From the checkout root after importing the complete roots:

```
python tests/Reference/PetalPaw/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/petal-paw-source-vectors.json
python tests/Reference/PetalPaw/generate_schedule.py --root . --output tests/Game.Presentation.Tests/Fixtures/petal-paw-schedule-source-vectors.json
```

Both generators refuse to overwrite an existing result. Generated trajectories belong only in ignored local fixture folders or an external evidence directory. The managed comparison reads `FIRERED_SOURCE_VECTOR_DIR` when explicitly selected, with no fallback for missing files.

The callback corpus has 44 explicit cases and 1,833 complete frames: each of the five authored Assist trajectories in all four actor slots; both looping petal frame programs; the source finite third frame program; screen-origin and signed16-Y wrap boundaries; unused shared argument registers; duration1, tiny negative sign-bit velocity and maximum signed-coordinate deltas. The original native frame engine handles delay and flip progression. The shared harness's isolated destructor only marks the sprite inactive instead of clearing it, so the managed comparator checks lifetime but intentionally never compares retired coordinates or frame state. The original fast-linear callback handoff still governs the deferred destructor.

The setpan corpus executes the exact source command and panning function for all 256 signed-byte inputs and all 16 actor/target slot pairs (4,096 rows). Every case asserts exactly one SE12 control and two-byte script-pointer advancement. Move invocation status-animation flags are false. The existing audio backend deliberately applies the command only to this animation's owned SE1/SE2 tokens; no music or cry controls are added.

The schedule corpus runs native load/delay scheduling over the canonical importer's source command discovery. Single-level calls are flattened without inserting waits. It compares all 5 Assist and 24 Sweet Scent births. Complete runtime tests independently cover palette fade completion, six layouts, snapshot/replay equality, visible material pixels, normal idle handoff, next called-leaf playback and cancellation.
