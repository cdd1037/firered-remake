# Shared action canceller checks

Use the existing installed official toolchain, authenticated product data, and pinned source checkout. The selected snapshot does not copy tools or content.

```sh
bash tests/Game.SourceActionCancellers.Tests/run.sh /path/to/firered-remake /path/to/audio-reference-build/pokefirered /tmp/canceller-evidence
```

The runner compiles unmodified original C cancellation case blocks for 1,728 counter/precedence/RNG-count vectors, then checks the same actual slot-2 actions. It also executes real four-action turns for faster-to-slower status acquisition, Early Bird/natural thaw/Truant precedence, blocked recharge cleanup, self-KO occupant identity, stale continuations and exhaustion rollback. Canonical singles comparisons check exact serialized state, ordered events and RNG, including blocked/no-PP cases and the forced recharge continuation.

The original-C harness stubs damage calculation; it proves cancellation decisions and draw counts rather than claiming independent C damage parity. Historical singles comparisons and the frozen 64-phase baseline are separate checks. Tests exit nonzero on missing evidence, an empty vector suite, or failure.
