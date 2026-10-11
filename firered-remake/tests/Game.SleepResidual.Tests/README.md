# Bounded shared sleep/residual verification

Run against the existing authenticated product content and the pinned local reference:

```sh
bash tests/Game.SleepResidual.Tests/run.sh PRODUCT_ROOT PINNED_PRET_ROOT FRESH_OUTPUT_DIRECTORY
```

The runner parses JSON/XML first, verifies exact reference file bytes and extracted fragments, compiles its locally authored C harness, authenticates the complete vector snapshot, then builds the pure .NET 10 test executable under the shared build lock. No GUI, campaign navigation, original repository scripts, downloaded ROMs, or broad historical suite is used.

`legacy-baseline.json` was captured from fixed product commit `1a3f6ae58b999beddcb2e1e284b0e5791753670b` before modifying domain code. Its 42 hashes must not be regenerated to fit the implementation. `LegacyParity` additionally compares 36 selected actions through canonical singles and the shared frame adapter. A separate negative test verifies the family-specific source correction for Nightmare through Protect; that case was not part of the unchanged normal baseline.

The C vector snapshot contains 184 cases. The suite compares 176 admitted commands and retains eight Uproar observations as source-only evidence, since Uproar is explicitly gated in four-slot execution. Raw arithmetic vectors intentionally use small synthetic HP maxima and invoke actual internal recipe hosts; separate public action/full-turn tests use factory-valid source Pokémon. The source harness's output observes flags before full faint cleanup; full-turn tests cover the real faint/reward/deferred-replacement owners, JSON restoration and no residual replay.

The first candidate run exposed a test RNG fixture error: it selected Shed Skin's seed without accounting for the end-turn speed-tie sort. The corrected fixture obtains the RNG after the real source order calculation, then verifies Shed Skin before Nightmare. No product logic or frozen expectation was changed for that correction.

When the separate Substitute capability is installed, the suite registers one additional combined group covering Nightmare's Protect+Substitute ordering, unblocked Nightmare with retained Protect history, Yawn immunity before Substitute, Yawn Substitute before Safeguard, and Ghost Curse's blocked HP cost. `substituteIntegration` in the result explicitly reports whether that group ran; the baseline result does not claim that separate capability passed. The frame's generic Substitute script gate defers this family, while its existing Protect miss-marker command remains in place. Actual Substitute failure reaches resultmessage, where a prior Protect miss wins the printed result; direct ability and Safeguard messages retain their separate script order. Ghost Curse is not Protect-affected.
