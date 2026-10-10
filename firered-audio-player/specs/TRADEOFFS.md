# Fidelity / development tradeoffs

The current private prototype profile is **modern-dry experimental**, not an original-hardware renderer. It executes source sequences and source samples live. No fallback exists yet. Both selected songs request reverb=50; this profile deliberately omits reverb and does not reproduce PWM. Those audible differences prevent a fidelity pass and must be disclosed when auditioning it.

## Required for useful recreation
- Correct notes, running status, patterns, tempo, ties, loops and gate timing. Missing commands fail loudly.
- Original PCM sample identity, loop points, pitch/bend/modulation and envelope shape.
- Shared five PCM allocation and original priority/tie-breaking, plus separate required PSG hardware channels. Unlimited polyphony can change arrangements and SFX contention.
- Square duty and SE_SELECT sweep: a static generic oscillator is not sufficient.
- Bounded external data, deterministic block-independent output, explicit start/stop/restart, no render-path file IO.

## Modern alternatives worth isolated A/B tests
- Direct software sample mixing into Godot PCM instead of ARM, DMA, FIFO, hardware registers and CPU emulation. There is no value in emulating infrastructure purely for its own sake.
- Exact rational sequence frame clock retained, while output can be 48 kHz float. Compare 13,378.96 Hz internal integer mixer against modern interpolation before selecting sound quality. Current nearest-sample 48 kHz profile is only a starting point.
- Replace old low-rate reverb with a small explicit modern delay/reverb profile only after hearing A/B and measuring tails. Dry output is not claimed equivalent and is not a product decision.
- Offline pointer/resource normalization and prevalidation instead of a generic ROM scanner. Preserve compact 8-bit samples rather than expanding whole libraries to floats.
- Logical source track-order keys for priority decisions instead of actual emulated RAM addresses.

## Explicit recorded fallback candidates
- Uncommon cry modes with reverse/progress/chorus/runtime XCMD combinations, if exact source behavior costs disproportionate maintenance.
- A small enumerated list of exceptional songs/effects after command inventory and failures demonstrate need.
- Fallback must be named and tagged in the manifest/API and counted separately from synthesized coverage. Normal cry is not currently supported; it errors rather than silently using a recording.

## Not useful scope now
Generic GBA/other-generation support, arbitrary ROM inference, ARM emulation, hardware bit-perfect certification, MIDI/SoundFont translation, editing UI, or every output-device backend. Godot can own device output.

## Package accounting
Original full audio data is 2,565,436 bytes (2.446590 MiB), before index and C# code. Full existing base recordings plus metadata are ~28.011 MiB. Actual hybrid benefit must include selected fallback bytes, executable/resources, platform runtime already required by Godot, and any normalized-data expansion. No final savings claim until that inventory exists.

## Readable implementation size budget (rough, not a commitment)

For a fixed-FireRed useful implementation rather than hardware-bit-perfect/general GBA:
- Resource safety/normalization:400–1000 readable C# lines
- Sequence/scheduling:500–1200
- PCM/PSG/envelopes/resampling:800–1800
- Allocation/control/normal cry/game audio policy:600–1500
- Thin Godot adapter:200–600

Rough core total2500–6100 lines; tests/development verification may add1500–4000, giving approximately4000–10000+ lines for the project. These are decomposition estimates, not a prediction from the tiny first prototype. Special modes, error handling, maintainable APIs and device tests can increase them. Raw LOC varies strongly with formatting; generated resource/event tables are not handwritten engine complexity.
