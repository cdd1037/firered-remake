# Plain field transition presentation

The accepted movement producer now supplies a transient typed cause and both endpoints. Ordinary connected edges bypass the old blanket map-change fade. This does not yet implement continuous cross-edge camera/player interpolation.

For an immediate plain warp with a plain arrival, ordinary NONE/SUNNY map headers in the same nonempty region section share one departure/arrival controller. The source `fldeff_flash.c` region-section condition proves that this bounded subset does not request a map preview. Animated doors, automatic door steps, stairs, cave/preview and weather-specific branches retain their existing presentation; their complete source sequences remain open.

`FieldWarpPresentation` extracts the existing common-fall palette scheduler into one authority used by live warp, common-fall and recorded playback. The two palette banks update on separate callbacks. Departure waits for both palette completion and actual requested music-stop completion. An unchanged song is ready while still playing; absent/faulted audio cannot strand the cosmetic controller. Arrival follows the ordinary exit/weather/palette order. Both minimum departure and plain arrival consume 24 callbacks. The shared music adapter retains the existing source fade and admits its ordinary indoor/outdoor speeds 2/4.

The live view captures detached origin/destination field projections and uses the same map/actor/palette renderer as recap. Gameplay has already committed at the ordinary command boundary. Input and saves are locked during presentation; play-time observations continue and are committed after release. No movement, damage, RNG or map script is rerun by presentation.

## Recording and compatibility

Only the exact accepted receipt and its last provisional map marker can receive a completed observation. The observed switch/end callbacks are reconciled through the next existing accepted transaction, or projected into an immediate field save. Rejected commands do not publish it. No direct command-state setter or separate revision is introduced.

The optional timing occupies eight logical bytes in the existing bounded recording budget. Overflow closes recording with the unknown marker intact, rather than truncating an actual audio wait. Older, unobserved and unsupported transitions remain unknown and switch immediately during replay; no cause is inferred from destination identity. Authentic same-map warps can now produce a marker. Replay uses the same phase sampler and the recorded switch boundary, without consulting current audio.

## Validation

The portable source generator extracts the pinned original palette/warp/weather callbacks: ready departure, delayed music departure and plain arrival, 117 rows. Its generated output remains local and ignored. Managed tests compare both palette banks and each callback, recorded/live equivalence, RGB555 grayscale-then-black arithmetic, music fade cadence, actual movement receipts, save projection/identity, budgets, malformed timing and old-record behavior. Compilation and gate results are recorded with the checkpoint.

No new native visual/audio acceptance is claimed. Full animated-door sequences, seamless-edge pixel continuity, preview/weather transition branches and complete special movement poses remain separate bounded follow-ups.

### Checkpoint gate

- `dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: zero warnings/errors, 60.64s (`/tmp/plain-warp-refresh.log`). The initial build stopped at the obsolete MSTest `DataTestMethod` attribute in a new test; replacing it with `TestMethod` changed no assertion.
- Presentation phase/palette/music cadence and existing Quest grayscale: **19/19**, 104ms. The initial run correctly reported one missing local generated Quest palette fixture; its maintained generator restored the ignored file and the unchanged test passed. New warp traces and every RGB555 restoration tone remain independently source-generated.
- Domain movement/connection receipts: **6/6**, 72ms. Application Quest/clock regressions: **185/185**, 17s, including 20 new transition observation/timeline cases.
- Explicit opt-in audio host/driver regression: **44/44**, 1s. This tests runtime behavior, not speaker output. Its isolated test project was restored from the existing local package cache before compilation; no new package download was needed.
- Official Godot 4.7.2/.NET 10.0.12 headless startup: exit 0, no runtime errors, MVID `dbe55390-349b-425f-968b-e55092a96b5d` (`/tmp/plain-warp-headless.log`). DISPLAY and Xvfb are absent. New window-level warp/recap interaction and audible fade remain unverified.
