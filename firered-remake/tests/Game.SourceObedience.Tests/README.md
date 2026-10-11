# Source obedience tests

Run with the authenticated main project content and the pinned local source:

```sh
bash tests/Game.SourceObedience.Tests/run.sh /path/to/firered-remake /path/to/audio-reference-build/pokefirered /tmp/source-obedience-evidence
```

The runner uses the main project's pinned .NET toolchain, takes the existing
build lock with a 240-second timeout, and does not copy content or run upstream
scripts. Outputs include `source-obedience-vectors.json`, `result.json` and
`public-turns.json`. Empty suites, missing source/authenticated content,
nonzero failures and absent complete public-turn branches fail the executable.

The source probe extracts unchanged `IsOtherTrainer`,
`IsBattlerModernFatefulEncounter` and `IsMonDisobedient` from git at
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. Its 65 bounded vectors deliberately
stub `CheckMoveLimitations`, `GetMoveTarget` and `CalculateBaseDamage`. Three
link/tutorial vectors are retained in the C artifact but outside the managed API.
The suite checks 60 exact ordinary-battle comparisons and two intentional
Mew/Deoxys event-origin deviations approved by the user. The battle context has
no fateful/event-origin field. Eleven inspected source checks document
later sleep/damage/target draws and history flags separately from executed C.

See `docs/source-obedience.md` for context facts, profile/save boundaries,
shared rule ownership and what the suite does not establish.
