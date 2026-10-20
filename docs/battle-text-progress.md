# Ordinary battle progressive text

The native tutorial, ordinary single/trainer/wild and double battle message surfaces share `SourceBattleTextPrinter`, backed by the existing `SourceTextProgress`. `battle_message.c` in the pinned local `037335f4` reference selects the saved text-speed setting and enables A/B speed-up for `B_WIN_MSG` / `B_WIN_OAK_OLD_MAN`. The consumer maps Slow/Mid/Fast to 8/4/1, begins with no visible glyphs, and reveals Unicode codepoints through `SourceDialogText`.

Held state is sampled from the shared logical input owners (A/B, including configured button modes and mouse ownership). A new press is queued once until a source frame runs. Releasing the physical button stops held acceleration; a printer press cannot also ACK its semantic page. Focus loss clears the queued edge.

A visual continuation requires the current printer to finish and continues its source printer cadence. It changes only the native visual page, not `_battlePage`, `_wildPage`, `_doublePage`, gameplay events, or RNG. Each source-string expansion records its own continuation indices before the semantic arrays are flattened. Adjacent pages within the same array/key and the same source expansion retain the printer latch and delay; a new source string or event resets them. Text equality is not used to infer that boundary. Existing semantic ACK branches, silent-event automatic ACKs, animation blockers, cry waits, HP animation timings and `.10` / `.45` gates remain owned by their existing battle paths. This change does not retune the source printer's final-character/EOS timing or script waits. Continuation arrows appear only after the printer completes.

## Explicit boundaries

This is not a claim that every battle-adjacent text box has migrated. Legacy notice/error boxes, bag/action prompts, party/medicine/capture/replacement panels, trainer shift choices, level-up stats and move-learning choices keep their existing rendering/input. Move-learning introduction pages on ordinary native battle surfaces are progressive. Old Man, Safari and Teachy TV custom presentations retain their separate consumers. Braille and other field-specific surfaces are outside this battle change.

Stats have no progressive message owner: the visual-pagination helper explicitly bypasses them, preserving their two-stage totals/ACK flow rather than waiting for an invisible printer. Display-suppressed capture and curse-cost phases do not accrue hidden printing time.

`BattleTextPrinterTests` covers all configured cadences, blank initial pages, held acceleration/release, subframe presses, visual continuation readiness, new semantic reset and focus-loss input clearing. Narrow source-wiring guards protect the Teachy TV/stats exceptions and source-page metadata. These tests do not substitute for native window verification of battle input and layout.

## Previous integration evidence and recovery boundary

Before the 2026-10-20 workspace rollback, the coordinating serial solution build passed with zero warnings/errors and all six `BattleTextPrinterTests` passed. An external Godot 4.7.2 / hosted .NET 10.0.12 probe exercised the compiled real `CampaignView` in headless and rendered modes and reported `BATTLE_CONSUMER_PASS`. Those temporary evidence files did not survive the rollback. This is a historical record, not a claim that restored sources have already been revalidated; fresh integrated checks must be recorded separately.

The controlled fixture recipe reused `NativeMultiHitFixtureTests.CreateControlledField`: fresh schema-3, source-legal Clefairy/starter ownership with explicitly synthetic opening/capture receipts. The lead's experience was placed one point below the next level. The ordinary encounter, attacks and reward were produced through real campaign commands. No user save was modified. An old local checkpoint was rejected as unsupported and was not converted.

The historical consumer probe checked blank initial text, Slow/8 first-frame reveal, logical B-held acceleration/release, no revision/event/page mutation during printing, visual-only overflow without semantic ACK, and a real level-up stats result whose first Enter shows totals and second Enter advances. Rendered screenshots showed one glyph, three glyphs and the completed Chinese sentence with completion-only cursor. Controlled frame calls establish printer behavior, not physical keyboard latency. The temporary native harness reported one ObjectDB instance-leak warning at exit; it was not a clean-shutdown acceptance. Dummy audio and controlled ownership are not natural opening/story or real audio acceptance.

## Fresh recovery verification, 2026-10-20

After restoring the accepted changes, the final serial solution build again
passed with zero warnings/errors. The rebuilt external consumer probe passed its
headless check against that final assembly on Godot 4.7.2 / .NET 10.0.12. This is
new evidence, distinct from the historical checks above.

Local harness: `/workspace/shared/text-menu-native-recovery-20261020/battle-probe/`.
The external `Probe.csproj` references already-built production DLLs, not a
production project build. After the normal solution build, compile the probe
with `dotnet build Probe.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`;
`bash run.sh --headless` runs logic, while `bash run.sh` requires a graphical
desktop. The fixture and assertion scope are the same controlled recipe described
above, including real public encounter and level-up reward commands.

`headless-final.log` contains `BATTLE_CONSUMER_PASS`. `assembly-final.sha256`
records matching production and copied `Game.Godot.dll` SHA-256:
`2f06651f4bead53a10f30d324e20e4688772f39d8dd74665805dc774d4258d90`.
Headless success does not establish rendered pixels, physical keyboard timing,
full-story playability or actual audio output.

The fresh rendered run subsequently also reported `BATTLE_CONSUMER_PASS` in
`native-final.log` against the same recorded assembly. The four generated images
include `battle-first-glyph.png`, `battle-partial-release.png`,
`battle-complete.png` and `battle-after-stats.png`. Inspection confirms one glyph,
three glyphs without a continuation cursor, and the full Chinese encounter line
with the cursor. This restores rendered consumer evidence after the rollback;
it does not expand the controlled fixture/input/audio acceptance scope above.
