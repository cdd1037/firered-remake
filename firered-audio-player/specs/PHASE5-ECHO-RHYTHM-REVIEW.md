# Independent echo/rhythm checkpoint review

Reviewed frozen commit `b093ff8d9909a67035651ca065dd62fab8b9c997`, rather than the concurrently edited PSG working tree. Runtime code was not edited. Original reference: `pret/pokefirered` revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`; contract: `PHASE5-VOICE-GAPS.md`.

## Result and verification

No newly introduced blocking defect found for the bounded echo/rhythm expansion and its selected-song claims. Two inherited EOT semantic gaps were independently reproduced below; this is not blanket approval of all original-driver tie semantics.

A separate temporary source snapshot of the frozen commit rebuilt with the installed SDK, zero warnings/errors. Its executable passed:

- **116 self-tests:**79 prior plus37 echo/rhythm checks.
- **386 public catalog render hashes:** exact against phase4; full cache count386 and3,136,467 sample bytes.
- Independent hash verification of all21 frozen result artifacts listed in `results/phase5-music-expanded/checkpoint-hashes.json`.

The frozen coverage is correctly **5/12**, with PC On newly successful. The other seven fail explicitly on the then-unsupported wave/noise families. In-flight PSG additions and the evolving Godot queue experiment were excluded from this review.

## Source/implementation checks

XCMD8/9 consume one full unsigned byte, retain XCMD running status, and update track state. Note allocation copies that state into independent voice fields. PCM release uses the original absolute envelope floor; zero-sustain decay enters the same branch. Echo entry preserves sample position/phase and does not prematurely decrement length. PCM zero/one-length behavior and CGB signed-byte length128/129 behavior agree with the original branches.

CGB echo uses the specified ceiling calculation. Countdown advances on sound-frame calls and bypasses ordinary envelope stepping while echoing. `SourceFiltered` still uses the already-declared continuous64-Hz envelope approximation for entry timing; the expansion report correctly avoids claiming hardware-exact CGB timing. Natural sample exhaustion still stops a PCM voice despite an outstanding echo countdown.

Rhythm resolution indexes by untransposed raw MIDI key, validates the child before selecting/mutating a channel, takes the child sounding root, sign-extends the source per-note pan and applies track transpose. Key-split-only keeps sequence pitch. Nested composite and out-of-image child paths still throw. Raw MIDI identity is now retained separately and used for EOT matching. The ordinary centered path and zero-echo path preserve the recorded legacy regressions.

There is no repeated recording, delayed sample replay, prerecorded fallback or newly substituted oscillator in this checkpoint. Pseudo-echo is held envelope state on the current source voice.

## Reproduced inherited EOT gaps

These behaviors were already present before this checkpoint; neither is covered by the37 new checks. The source reference is `src/m4a_1.s:1818–1856`, `ply_endtie`.

1. **Explicit EOT does not update the track's remembered key.** Original `ply_endtie` stores an explicit operand into `MusicPlayerTrack.key`. Native `Sequence.cs` reads a local `key` but leaves `t.Key` unchanged. A fixture `TIE60; wait; EOT61; wait; TIE` with the final key omitted produces raw keys60,60 natively; the original remembered key makes the latter61.
2. **One EOT releases all matching active voices.** Original traverses the newest-first channel chain, releases the first eligible non-stopped matching MIDI key, then returns. Native `EndTie` visits all matching voices. A fixture with two overlapping TIE60 notes followed by one EOT60 releases both natively, rather than only the newest eligible one.

These do not invalidate the frozen selected-song results or the raw-key-versus-sounding-key improvement. They should be fixed and tested before claiming general original EOT equivalence. Keep the correction scoped to explicit-key memory plus newest eligible matching-channel selection, preserving existing rollback gates.

## Reference comparison boundary

The new song probe uses a different launch setup from the older cry probe. The cry's855-frame offset is not transferable to these song references. Command-frame origin comparison without gain fitting or phase chasing, supplemented by onset/duration/envelope/band metrics, is an appropriate bounded analysis. Low noise/PSG sample correlation alone must not be interpreted as decoded-asset failure or corrected by optimizing phase.
