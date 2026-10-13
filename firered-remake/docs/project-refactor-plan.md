# Project refactor: completed slice and remaining work

**Status, 2026-10-13: the project-level refactor is incomplete.** Commit `8940b9b` completed the four bounded changes listed below, source synchronization and the stage10 backup. Those results do not establish that regional composition, Application wiring or battle execution have been consolidated. Earlier wording describing the overall refactor as completed was too broad.

The original first-clear instruction included project cleanup and a pause. Development resumed on the user's explicit 2026-10-12 23:42 UTC instruction. The latest 2026-10-13 direction explicitly requests deeper refactoring afterward, prioritizes project quality over historical baggage, and permits invalidating old saves; useful milestone saves may be converted manually. Finish the in-progress Five Island / Celio and Ruby/Sapphire delivery, synchronize its source and complete Drive package, then execute the remaining refactor below. This correction does not stop the current product work or reinstate the old pause.

This plan is based on static inspection of `9fba4616` plus the in-progress Five Island integration. It changes no product code and supplies no new test result. The four historical changes remain accepted within their original scope; all remaining work below is open.

## First-story exit reached

The actual eight-badge route through Victory Road is retained. The inherited natural League test party won Lorelei, lost to Bruno, legitimately captured a Golbat and won Lorelei again, but exhausted its recovery resources. Its complete history stops at generation 41923 without claiming a natural first clear.

The separate, explicitly controlled reasonable-level party and supplies fixture actually defeated the unchanged original Bruno, Agatha, Lance and Champion through public commands. EXP and money settled normally; the Hall file was written at generation 42922 and loaded by a separate process. Original credits and ResetRequested completed at generation 42943. The 1,020-command chain contains zero synthetic victory receipts. This closes the bounded product-development exit while preserving the natural generation-41923 file unchanged. It does not claim that the inherited natural party cleared the League. See [the controlled report](../tests/Game.PokemonLeagueControlledRoute.Tests/evidence/REPORT.md) and [remaining scope](first-clear-remaining-scope.md).

Clearly distinguish source/logic checks, headless view checks and unavailable native visual/audio comparison. Remaining visual fidelity and regional service gaps stay explicit.

## Four completed changes at `8940b9b`

1. `Game.Tooling.SaveUpgradeProfiles` now owns 37 exact predecessor/target/migration entries and seven optional-root admissions. The atomic staging/publication block and migration algorithms are unchanged. Seven late-profile launchers share a single helper with their original argument, environment, save-folder and exit behavior; `launch-current.sh` selects the complete first-League profile and delegates its one required import check.
2. `CampaignSession.OccupiedFieldMaps` retains all 218 map keys but overlays actor occupancy only on the current map and direct neighbors, which are the maps the movement operation can read. The bounded candidate measured 88.57–92.46% lower allocation across five locations, with exact transitions/events/RNG preserved in 35 cases. These are allocation measurements, not elapsed-time claims or a new cache.
3. Department-store elevator coordinates use `GetSourceCell` directly in two call sites, avoiding implicit compact-map expansion. Current elevator maps are rich maps, so no large current speedup is claimed. All ten elevator entries and compact/rich equivalence were checked.
4. Removed only the completed one-off `package-victory-road-rules.py` and `package-victory-road-escape-rope.py` patch assemblers. Permanent importers, `prepare-victory-road-hooks.py`, the first-Sevii preparation contract and all pinned reconstruction inputs remained. Unique source, save and execution evidence was preserved.

Candidate characterization remains in [the compact proof index](../evidence/project-refactor-20261012/candidate-proof-index.json): 557 registry assertions, 1,303 routing/publication assertions and 11 actual CLI comparisons; 154 launcher before/after pairs; 4,993 cell checks; 305 movement checks. These are earlier candidate results, not newly repeated integration passes.

The final normal Godot and Tooling builds passed with zero warnings/errors (117.29 and 10.01 seconds). The final registry host passed all 557 checks. The final Application smoke used one public East move from the real generation-40778 Indigo Center checkpoint under both pre-refactor and final DLLs. All 18 checks per cohort passed with identical complete states, outcomes, events, both RNG streams and strict-save bytes. The final normal Godot cohort authenticated the unchanged controlled battle observation and passed all 17 Hall/save/Continue checks across six headless Draw phases; its newly written save is byte-identical to the original true Hall file. No historical route or backup runtime was replayed. See [field comparison](../evidence/project-refactor-20261012/final-field-smoke/comparison-summary.json), [normal builds](../evidence/pokemon-league-combination-20261012/final-refactor-build.log) and [final view result](../tests/Game.PokemonLeagueCompletionView.Tests/evidence/controlled-20261012T191519/controlled-run-result.json).

These counts and timings are historical evidence for that slice, not acceptance of later code or of the remaining refactor.

## Target and compatibility policy

The target is one maintained current-game composition and one authoritative ordinary-battle execution path, with explicit differences for singles, doubles and special encounters. Historical regional milestones are evidence and optional conversion inputs, not a requirement to keep every old runtime path forever. Preserve actual current gameplay and product quality; old save formats may intentionally become invalid.

- Back up original saves before changing persisted state. Define the new schema/rules version and reject unsupported old versions explicitly. The user has authorized this break; do not ask again merely because a historical save stops loading, and never silently reinterpret its old payload as a new version.
- Select only useful milestone checkpoints for a one-time offline/manual conversion into the new representation. Write a new destination, preserve the original and record the conversion. Validate party identity/stats/HP/PP, inventory, money, story flags/variables, map position and other relevant progress, then strictly load the result. This is not a promise of automatic conversion for every old save and must not introduce a permanent compatibility layer.
- A temporary adapter needs named current production consumers and a removal condition. Remove it when those consumers use the current model; historical save compatibility alone is not a reason to keep it. A permanent boundary is justified only by a current product responsibility, such as presentation or persistence, not historical age alone.
- Preserve authenticated content provenance, ownership versus opponent admission, command transactions, event order and RNG behavior. These are correctness boundaries. Removing an old regional feature gate must not grant unsupported story content or silently change active mechanics.
- Consolidate in reviewable slices. No framework migration, arbitrary line-count target, generic plug-in system or wholesale battle rewrite is required.

## Remaining execution list, ordered by benefit and risk

### R1 — Consolidate profile, capability and catalog selection

**Status: first current-composition slice accepted; historical diagnostic/runtime-construction retirement remains open. Benefit: high. Risk: medium; current admission and selected checkpoint conversion require care.**

Evidence of remaining duplication:

- [CampaignView.cs](../src/Game.Godot/CampaignView.cs), `_Ready`: the long `FiveIslandProfileRequested ? ... : ...` loader chain combines current selection with many historical environment switches. [CampaignSixIslandView.cs](../src/Game.Godot/CampaignSixIslandView.cs), `SixIslandProfileRequested`, also inherits its successor's request; similar chaining is distributed across the regional view files.
- [CampaignSession.cs](../src/Game.Application/CampaignSession.cs), constructors: regional catalog validation calls are repeated, and machine/shop authentication uses another latest-to-earliest region chain.
- [CampaignWildBattles.cs](../src/Game.Application/CampaignWildBattles.cs), `CampaignWildRulesVersion`: another region-to-rules selection chain. [OwnedBattlePokemonCatalog.cs](../src/Game.Domain/Pokemon/OwnedBattlePokemonCatalog.cs), `For`, `ForMechanics`, `IsCampaignSuccessorOf` and `AdmitsTrainerOpponent`, separately encode related profile relationships.
- [SourceBattleAdmission.cs](../src/Game.Domain/Battle/SourceBattleAdmission.cs) already separates `BattleRulesProfile` / `SourceBattleCapability` from campaign admission. This separation is useful and must survive consolidation. [CampaignCatalog.cs](../src/Game.Domain/World/CampaignCatalog.cs) is a BCL-only interface; it must not acquire Content or Godot dependencies.

Execution:

1. Freeze the newly delivered current profile and inventory which historical launch modes still have real consumers. Define one explicit current profile description and new save/schema boundary. Audit [SaveUpgradeProfiles.cs](../src/Game.Tooling/SaveUpgradeProfiles.cs): retain only any useful offline conversion work, and retire obsolete upgrade entries with their old runtime modes. Do not build a second permanent upgrade router.
2. Resolve command-line/environment inputs once in composition, with documented precedence and conflicting-input handling. Pass the resolved selection onward. Retire obsolete launch modes after original saves are backed up and useful checkpoints are identified; keep diagnostic fixtures separate from the normal launch path.
3. Consolidate current catalog bindings, validation and capability selection within their existing assembly boundaries. Keep authenticated object identity and owned/opponent admission checks. Do not replace these with a species count, a profile-name prefix or an unrestricted dictionary of user-provided delegates.
4. Remove the superseded selection branches and historical runtime modes in the same accepted slice, or record a specific remaining consumer and removal condition. Adding a new registry while leaving every old chooser active is not completion.

Acceptance: current startup selects exactly one profile; new saves strictly Continue; selected converted checkpoints preserve the intended gameplay payload; old/unknown versions fail explicitly; wrong owned/opponent catalogs remain rejected; the retired selections have no production callers. Check representative precedence/conflict cases and affected catalog boundaries, not every historical launch permutation.

### R2 — Consolidate repeated Application wiring

**Status: open. Benefit: high. Risk: medium.**

Evidence:

- [CampaignOrdinaryTrainers.cs](../src/Game.Application/CampaignOrdinaryTrainers.cs), `OrdinaryTrainer` and `OrdinaryTrainerScript`, manually route each region through repeated selections.
- [CampaignFiveIslandTrainers.cs](../src/Game.Application/CampaignFiveIslandTrainers.cs) and [CampaignSixIslandTrainers.cs](../src/Game.Application/CampaignSixIslandTrainers.cs) repeat trainer lookup, source-script projection, ordinary actor/wait validation and history-origin checks.
- Their differences are real: Five Island supports a post-battle callback argument; Six Island validates original rematch wrappers and a double-battle wrapper. [CampaignFourIslandTrainers.cs](../src/Game.Application/CampaignFourIslandTrainers.cs) validates a scene-driven no-intro fight with exact coordinates, actor state and story prerequisites. It cannot be reduced to ordinary talk-to-trainer validation.

Execution:

1. Introduce one current trainer binding/lookup path for definition, party creation and source-script projection, using the already shared Domain factories rather than another battle executor.
2. Share the ordinary single-trainer occurrence and history checks where their conditions match. Keep explicit scene/callback/double policies at the boundary; preserve region-specific source authentication and fault meaning.
3. Route the current regions through the shared path, then delete their duplicated plumbing. Retire unused historical Application branches through the R1 version/conversion policy. Apply the same pattern to other repeated wiring only when actual callers and equivalent semantics are identified.

Acceptance: current ordinary trainer, Five callback, Six double/rematch refusal and Four no-intro scene cases preserve events, rewards and source provenance; malformed actor/script/line combinations still fail. Build normally and run the affected existing cases plus only missing boundary cases. Do not replay the entire regional journey for a wiring change.

### R3 — Unify battle execution and retire transitional bridges

**Status: open. Benefit: high. Risk: high; perform after R1/R2 establish the current boundaries.**

What is already shared, and what remains distinct:

- [SourceBattleRecipes.Primitives.cs](../src/Game.Domain/Battle/SourceBattleRecipes.Primitives.cs) and the other `SourceBattleRecipes.*` files implement shared `SourceRecipeContext` commands. [WildBattleSinglesRecipeBridge.cs](../src/Game.Domain/WildBattle/WildBattleSinglesRecipeBridge.cs) and [SourceFrameRecipeContext.cs](../src/Game.Domain/Battle/SourceFrameRecipeContext.cs) both implement that context. These are not two independent copies of every move recipe.
- The singles bridge still selects historical behavior through gates such as `EarlyParty`, `Route11`, `RocketHideout` and canonical rules versions. Some methods only translate state/events; others select old mechanics. This is where historical baggage must be classified and removed after current callers migrate.
- [SinglesBattleAdapter.cs](../src/Game.Domain/Battle/SinglesBattleAdapter.cs) converts `WildBattleState` and `SourceBattleFrame`, preserving actual parties, slot/entry identity, revision, events and RNG. It currently has a correctness role. Keep it only while those current consumers require conversion, with an explicit exit condition.
- [WildBattleSourceWildTurns.cs](../src/Game.Domain/WildBattle/WildBattleSourceWildTurns.cs) owns singles wild turns; [SourceFourActionTurns.cs](../src/Game.Domain/Battle/SourceFourActionTurns.cs) owns actual four-action turns and reward/replacement continuations. The latter currently admits preset doubles and trainer battles explicitly. It cannot become the singles owner merely by removing its admission check.

Execution:

1. List current entry, move, item, switch, end-turn, reward and continuation owners, and their consumers. Separate shared game rules, layout-dependent scheduling, serialization conversion and obsolete historical gates. Start with the actual current ordinary singles/doubles path; keep genuinely special encounter rules explicit.
2. Extend the existing shared frame/kernel model into a single authoritative current battle lifecycle in bounded phases. Preserve real doubles ordering and simultaneous occupancy; never simulate doubles by calling singles once per target. Preserve wild capture/escape, trainer rewards and scene callbacks as explicit encounter policies.
3. Move current Application callers and persistence to the canonical model, increasing schema/rules versions when useful. Convert only selected milestone checkpoints offline. Remove obsolete regional mechanics gates and duplicated owner implementations as each phase moves. Keep shared recipes in one place.
4. Remove `SinglesBattleAdapter` and transitional singles bridges when no current production caller or necessary event boundary needs them. A one-off old-checkpoint converter can remain outside production and does not justify retaining the legacy executor. If a small conversion boundary remains useful for current consumers, document its responsibility and keep it free of gameplay-rule decisions. Do not retain a full legacy executor solely to satisfy archived milestone tests.

Acceptance: selected complete current singles and real doubles flows retain ordered events, RNG traces, HP/PP/status, inventory, rewards and continuation semantics. Cover a switch, KO/replacement, interrupted/resumed turn and a terminal outcome, plus each materially affected special boundary. Validate selected offline conversions, new-version strict save/Continue and explicit old-version refusal. Reuse current shared-rule/source-oracle cases; do not rerun every historical matrix. The result must remove competing current execution ownership, not just rename adapters.

### R4 — Retire one-off tooling and compact delivery evidence

**Status: open. Benefit: medium. Risk: low for proven duplicates; high if a reconstruction dependency is removed.**

Evidence and limits:

- Only the two `package-victory-road-*` assemblers were removed in the accepted slice. This is not an audit of all remaining tooling.
- [prepare-victory-road-hooks.py](../scripts/prepare-victory-road-hooks.py) stages predecessor-specific before/after files and hashes. It is a candidate for retirement when the current reconstruction contract no longer uses those staged hooks, not an automatically deletable file.
- [generate-victory-road-domain.py](../scripts/generate-victory-road-domain.py) authenticates predecessor templates and emits deterministic declarations. [generate-sevii-ruby-trainer-factory.py](../scripts/generate-sevii-ruby-trainer-factory.py) and [generate-six-island-trainer-factory.py](../scripts/generate-six-island-trainer-factory.py) provide `--check` reproduction described by the Ruby and Six catalog documents. Their names do not make them disposable one-off tools.
- [candidate-proof-index.json](../evidence/project-refactor-20261012/candidate-proof-index.json), [final-result.json](../evidence/project-refactor-20261012/final-result.json) and [final-pack-file-index.json](../evidence/project-refactor-20261012/final-pack-file-index.json) already distinguish summaries, evidence and package inventories. Use such indices instead of retaining repeated product assembly cohorts in every active delivery.

Execution:

1. Classify remaining scripts by actual callers and reconstruction contracts: current importer/generator, current verification, or completed patch assembly. For an obsolete predecessor-based tool, replace its current reconstruction dependency first, then archive or remove it.
2. Separate fixture provenance from production admission: regenerated checkpoints carry their producer source, strict-save result and exact input identity, without hard-coding every historical stage hash into product rules. Keep only useful milestone converters and do not force a new lawful route to reproduce lost historical bytes.
3. List cleanup candidates with path, purpose, reproducibility or identical-byte proof, references and retention destination. Keep the current required source inputs, resource locks, backed-up original/converted milestone saves and concise acceptance evidence. Archive unique historical proof once; avoid copying every old build/cohort into each new backup.
4. Consolidate duplicated generator scaffolding only where it reduces current maintenance. Preserve each source lock and exact output contract. Remove confirmed unused one-offs and rebuildable/identical duplicates without deleting the only recovery source or evidence of a failure.
5. Update reconstruction and evidence links alongside retirement. Run relevant `--check`/manifest checks and verify the uploaded package structure and hashes. Packaging does not trigger a full restored runtime or gameplay replay.

Acceptance: the current package can be reconstructed from documented retained inputs; no active script/document references a removed dependency; each removal has an explicit reason; unique saves and proof remain locatable. Static inspection alone has not established that any additional particular script is safe to delete.

## Execution order and completion gate

1. Finish and verify the current Five Island / Celio and Ruby/Sapphire scope, then synchronize source and the complete Drive package. Keep its acceptance separate from refactor acceptance.
2. Execute R1, then R2, then the bounded R3 lifecycle consolidation. R4 inventory can proceed alongside them; removal follows verified retirement of consumers. Give each product file one owner and avoid simultaneous feature/refactor edits.
3. For each slice, record the changed production responsibility, deleted obsolete path, current-save impact, exact build/check results and unresolved work. Build the affected normal product targets and run focused checks. Broaden testing only when new changes or failures justify it.
4. At the final current commit, run focused startup, new-save/Continue, selected converted-checkpoint, field and current single/double battle checks. Old-version rejection is expected where documented; no historical full-chain replay is required. Record the actual compiler/runtime. Native visual and physical audio limits stay explicit; source or headless evidence does not close them.
5. Synchronize the refactored source and verified Drive delivery, and update README/roadmap/status wording together. Historical reports stay labeled historical. The plan is complete only when R1–R4 have concrete accepted results and no unexplained remaining duplicate current path; writing this document or passing the earlier four changes does not close it.

No completion claim is made for the remaining refactor in this revision.

## 2026-10-13 current composition slice

After the Ruby/Sapphire full backup, normal startup resolves one current game, deletes24 regional startup selectors and two competing selection chains, and uses one cached Application binding. New Game and Restore share catalog validation; Restore has no map-entry or RNG side effect. Current saves use an explicit new rules ID; original Celio files are rejected by normal loading and can be copied through the narrow offline converter. The owner normal Godot/Tooling builds and five real CLI conversion cases passed; independent57 focused checks and headless current startup passed. See [current composition](current-game-composition.md). This closes neither R1's remaining diagnostic/predecessor retirement nor R2/R3/R4.
