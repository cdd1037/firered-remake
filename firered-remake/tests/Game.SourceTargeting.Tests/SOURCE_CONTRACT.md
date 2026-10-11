# M04 bounded execution contract

Product baseline: `3a0344d0231cf270801c34d5458944103c39f6cd`, source blobs restored equal to historical `790edea`. Reference: `pret/pokefirered@037335f4c725d7c9aecdac87066f2002b4bd7e14`.

## Source owners

- `SourceMoveTargeting`: distinct learned-selection and caller `GetMoveTarget` entry points. Selected, random, both opponents, all other battlers, self, caller user-or-selected, depends and opponent-field masks are represented. Caller resolution is a target calculation contract, not complete caller execution.
- `SourceBattleKernel`: authentic recipe admission and one immutable action transaction over the real frame. No paired-single projection.
- `SourceFrameRecipeContext`: BOTH reentry into the same shared recipe, per-target history reset and reduction, one final permanent-party synchronization; true numeric target access for Explosion.
- `SourceRecipeContext`: topology and all-body Plus/Minus query, numeric-target begin/complete hooks. Legacy singles defaults remain one target.
- `SourceBattleRecipes.Generic`: the existing one generic recipe and one Explosion recipe. The damage adapter supplies topology; Explosion loops that recipe's commands with one PP deduction/self HP zero and final attacker faint.
- `ForestBattleDamage`: one shared damage formula. Plus/Minus before stat stages; `2 * (damage / 3)` screens with two source-present defenders; BOTH `/2` in original physical/special order. Explosion does not get the BOTH reduction.
- `SourceFourActionTurns`: only BeginActions target preflight, ResolveSelectedTarget compatibility wrapper, and Advance target/RNG handoff. Cursor, capability/residual checks, clean-recipe gates and end-turn owner are outside M04 changes.

## Distinct source predicates

Do not merge `SourceAbsentMask`, current HP and `DeferredKoVacancyMask`. The approved delayed KO vacancy applies only at action target selection. It does not alter original CountAliveMonsInBattle, Damp/Plus/Minus/Lightning Rod ability scans, or source script traversal predicates.

Learned random target uses one low-bit RNG draw. Called selected target retains rejection sampling. A caller with no opposing target returns an explicit null before entering a loop without a possible result. RNG overflow is atomic. Learned Follow Me uses the original timer/selected/live-target condition; a nonzero timer suppresses ordinary Lightning Rod scanning even if that Follow Me target has zero HP. Called RANDOM honors Follow Me while ordinary RANDOM does not.

Lightning Rod has original Gen III redirection and announcement only, with retained action order for competing holders. It grants neither immunity nor a stat gain. Original ability body scans remain independent of absence/HP; the owner gates an unexecutable resulting target. A deferred target is a distinct no-target boundary.

BOTH reentry tests the partner's HP, including a live body marked source-absent. It does not pay PP or print an attack again. First-target immunity/miss/history cannot contaminate the second. A KO during the first pass does not silently change source defender count for damage. Pressure uses the source stored-ability targets and one command cost.

Explosion uses numeric slots excluding actor and source-absent targets. Damp scans every stored body's ability before HP zero. Every target's original damage-before-accuracy order is retained; the acting body's zero HP does not truncate the loop. All target hits finish before the action owner records KO vacancies and pauses for each opposing defeat's EXP boundary. A terminal draw/win is not published ahead of rewards.

Plus/Minus uses original `ABILITY_ON_FIELD2` all-body scanning, including opposing, fainted and source-absent donors. It is admitted only by this bounded shared kernel. Canonical singles' older scalar recipes continue to reject Plus/Minus effective abilities atomically; this patch does not widen `SourceBattleAdmission.SupportsAbility`.

## Explicit next owners and boundaries

- Follow Me state is interpreted; the Follow Me move's own shared recipe remains to migrate.
- Random/all/depends/field targeting calculation does not admit unported move effects. Earthquake, Magnitude, complete callers, Perish Song and Teeter Dance remain rejected. Existing legacy Earthquake is not duplicated into another recipe.
- A numeric-all action after an earlier KO deferred vacancy, or with a source-present zero-HP target, is explicitly rejected before costs/RNG. The next action-target-plan continuation must handle approved vacant positions without changing original field-scan/count predicates.
- No-target PP/cost continuation is still rejected, not treated as a successful skipped action.
- Opposing KO rewards pause at the existing explicit EXP boundary. Full reward continuation and end-turn replacement are separate owners.
- Full action cancellers, residual callbacks, switches/Roar, unsupported contact/volatile/item interactions, App/UI/AI, campaign doubles admission and presentation are not claimed closed by this block.

## Integration notes

Apply the restricted patch's M04 hunks to the restored baseline. Preserve newer weather, entry/Trace and end-turn work in common files. `SourceGenericExplosion` has one `SourceGenericContact` call after Rage; merge the separate Color Change worker's callback at that location exactly once. There is no M04 edit to global ability admission.

The one change to M02 tests removes Surf from the old unported-recipe rejection list. No frozen golden data is altered. Newly retained tests verify the existing exact singles parity, generic/stat recipes, full historical creation/turn/trainer hashes, and four-action ordering/KO suite.

## Original anchors

- `battle_main.c:4025–4133`: learned target, Follow Me, Lightning Rod, random and absent fallback.
- `battle_util.c:3054–3133`: `GetMoveTarget` masks/rejection/caller conditions.
- `battle_script_commands.c:825–900`: canceller ordering and Lightning Rod announcement.
- `battle_script_commands.c:4308–4336`: BOTH next-target HP test and reentry.
- `battle_script_commands.c:6259–6330`, `battle_scripts_1.s:377–419`: Damp and numeric Explosion loop.
- `pokemon.c:2544–2560,2595–2605,2651–2677`: screens, spread and absence-only count.
- `pokemon.c:2484–2487`, `battle_util.h:40`, `battle_util.c:2340–2351`: Plus/Minus all-body query.
