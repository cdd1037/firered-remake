# Isolated Godot PCM experiment

Uses the existing installed Godot4.7.2 Mono/.NET10 toolchain, the original C# synthesis core, and external original resource pack +6188B normal-cry metadata. No recorded-audio playback, FFmpeg, third-party synth, or main-game modification.

From `firered-audio-player`, build with `./demo/build.sh`. Supply external resource paths explicitly:

```sh
PACK="$(realpath ../firered-remake/content/generated/audio/synth/firered.pack)"
CATALOG="$(realpath ../firered-remake/content/generated/audio/synth/normal-cries.bin)"
./demo/start.sh --headless --audio-driver Dummy -- "$PACK" "$CATALOG"
./demo/launch-interactive.sh "$PACK" "$CATALOG"
```

The example uses the engineering package’s canonical pair; the demo accepts any externally supplied supported pair and never searches the game directory. Interactive launch retains the explicit Dummy backend in this cloud environment.

The interactive controls restart Pallet, overlay SELECT and Jynx normal cry, cancel an active cry, stop BGM, adjust master gain, and close/release resources. They are an isolated experiment, not a production game audio service.

Synth owns48000Hz PCM. Generator explicitly uses Custom48000Hz and Godot resamples to the server output rate (observed44100Hz with Dummy). The0.1s generator request rounds to8191 usable frames; the producer targets5120 queued frames (~106.67ms upper bound), not the full170.65ms ring. Reported bound excludes device latency and must not be called end-to-end audible latency.

Automated test drives BGM/SE/cry overlap, actual live-cry cancellation, gain/mute, output-ring replacement, restart, deliberate250ms producer stall/recovery, a full BGM loop, noise Door overlay and a Route1 programmable-wave switch. Run `./demo/verify-headless.sh "$PACK" "$CATALOG"` for machine-checked acceptance. `AudioEffectCapture` verifies the engine actually consumes nonzero PCM and mute reaches zero output. Capture adds development-only per-process-frame allocations; this is not a zero-allocation callback claim.

Initial experiments are retained: a4095-frame ring caused sustained underruns in this Dummy configuration; active `ClearBuffer()` was rejected by Godot. Current switching stops/replaces the ring. Shutdown waits250ms after stopping/disposal so the audio server can retire its playback reference; immediate process exit previously left a tracked playback reference.

No actual listening/device acceptance is possible with Dummy. GUI must be coordinated with the main-game worker before launching on the shared desktop.

Diagnostic sequence tracing is enabled in this demo to prove loop/event timing. A production host should disable Synth.EnableTrace and omit AudioEffectCapture unless diagnostics are requested; this demo deliberately measures control behavior rather than claiming a zero-allocation UI/audio loop.
