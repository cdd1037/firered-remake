# Shared four-slot weather and side protection

Run:

```sh
bash tests/Game.FourWeatherScreens.Tests/run.sh AUTHENTICATED_CONTENT_ROOT PINNED_SOURCE_ROOT NEW_EVIDENCE_DIRECTORY
```

The runner sources the product's official `scripts/env.sh`, uses the local empty NuGet configuration with locked restore, and serializes each short build with `/tmp/firered-remake-build.lock`. It runs six focused pure suites, not the campaign or native/UI checks. The selected implementation base is `4c9fd2dab41635eb98661d0f2896b9e9a9a22380`. No content or toolchain is copied.

## Positive admission

The four-action owner now accepts validated permanent rain/sandstorm/sun fields and both sides' Reflect, Light Screen, Mist and Safeguard timers. Its actual selected move entry point admits precisely eight setting recipes: Reflect, Light Screen, Mist, Safeguard, Rain Dance, Sandstorm, Sunny Day and Hail. The shared recipe also owns the existing singles source setting commands and legacy Reflect command; historical profile-specific rain/sandstorm compatibility scripts retain their prior behavior.

The setting action spends the actual selected PP once, performs no setting RNG, starts five turns, and fails without refreshing a nonzero timer. A different temporary weather replaces a permanent field and clears all competing durations; the same weather fails even when suppressed. The existing shared field owner ticks both sides in Reflect → Light Screen → Mist → Safeguard order, then weather and battler residuals. Permanent weather has no duration and does not acquire a countdown event. Action and end-turn speed ordering now use the same permanent-weather-aware queries as damage and Rain Dish.

Reflect and Light Screen use the actual source-present defender count, including zero-HP retained bodies and excluding source-absent positions. Mist and Safeguard protect the actual side. Cloud Nine/Air Lock scans include source-absent living spectator bodies; a fainted holder no longer suppresses residuals/speed. Existing damage's separate all-body suppression rule is unchanged.

Permanent fields are consumed, not synthesized by this owner. This increment does not open four-slot initial-entry/switch-entry ability callbacks: Drizzle/Drought/Sand Stream holders, Trace and Forecast remain outside this owner. Wish, Perish, unported turn locks and entry/reward continuation are not implemented here. Existing M06 ownership is separate; no cursor or reward fields are changed. This is bounded Domain acceptance, not encounter/AI/campaign/full doubles/native/audio acceptance.

## Verification

The new suite exercises authentic factory species and retained source level/TM moves. It covers all eight settings in each actual position, both-side countdown and expiry, duplicate PP/failure behavior, weather replacement, permanent weather beyond five complete four-action turns, Rain Dish phase order, source-absent and fainted suppressors, actual speed-order changes, actual screen-protected damage, Mist/Safeguard prevention, source presence counts, serialization and closed capabilities.

`source_probe.py` verifies the exact local source commit `037335f4c725d7c9aecdac87066f2002b4bd7e14` and each used file against Git bytes. It compiles all eight unchanged setting commands, the complete unchanged `DoFieldEndTurnEffects`, unchanged `CountAliveMonsInBattle`, and the unchanged Reflect/Light Screen arithmetic blocks. The 944 vectors cover 512 settings, 144 full field-owner timer cases and 288 screen/count cases. Source functions use bounded declared environment stubs; this is original-C execution, not ROM execution. Scalar damage vectors intentionally edit numeric stat inputs and do not claim admission of those edited instances.

Regression suites retain the source weather/entry singles suite, called field effects, four-action source ordering, the preceding shared residual suite, and all 64 frozen singles phase hashes. The golden file is not rewritten. The preceding residual suite changes only its former screen/permanent-weather rejection fixtures to still-unsupported Spikes and malformed permanent-weather version cases.

Initial failed fixture attempts are retained in isolated evidence: the stronger retained-move check rejected the preceding suite's Rattata/Scary Face fixture, which was corrected to Raticate; Screech was outside the selected recipe subset and its Mist case uses the already-admitted authentic Scary Face instead. No rejected recipe was admitted to make a fixture pass.
