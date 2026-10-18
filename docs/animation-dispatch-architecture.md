# Animation invocation and resource authority

The post-content audit identified two parallel authorities: a move-symbol/number support table in Application, and obsolete Physical/Elemental pack readers beside the maintained resource resolver. This refactor removes both rather than adding another compatibility route.

## Invocation boundary

`CampaignMoveAnimationPresentation` receives the actual `PokemonMoveDefinition` catalog and a predicate backed by the scene's resolved scripts. A definition supplies `SourceMoveId`; the resolved script must have both that ID and the corresponding source `Move_*` label. Missing definitions, invalid metadata, missing scripts, or a mismatched label fail closed. There is no copied list of admitted move IDs in Application.

Singles retain their existing exported event envelope. A small explicit adapter converts the receipt kinds consumed by invocation matching into canonical source receipts, including PP-owner identity. Unconsumed receipts retain sequence and turn. Both modes then use one invocation validator. Grouped/stat result callbacks continue to use their existing authoritative result validators and event-time projection; no fake double-battle frame or final-state reconstruction is created.

Real source distinctions remain explicit: doubles target revisits, setup-only Wish/Future Sight, self-destruction, primary status/recovery, and grouped stat timing. Per-hit scheduling and planned-hit bounds come from the source move effect (`MULTI_HIT`, `DOUBLE_HIT`, `TWINEEDLE`, `TRIPLE_KICK`) and still require an available matching script. Primary and grouped stat outputs also obtain numeric source identity from the supplied catalog.

## Resource boundary

Import tooling exposes elemental script discovery separately from pixel generation. The background producer defers to those complete discovered programs, so sharing a callback cannot publish a second root without its owning task/material metadata. This is source discovery, not another move-ID exclusion list.

`SourceAnimationResources` remains the product pack/dependency resolver. The removed Physical/Elemental catalog classes no longer supply parallel parsers, numeric `Supports` lists, or incomplete helper closures. Family callback partials remain source implementations, not separate pack readers. Positive rendering tests use the resolver-backed composition fixture; intentionally incomplete primitive tests remain explicit.

Application test fixtures read actual generated script IDs and labels and provide the current content catalog. Tests for missing metadata pass `null` explicitly; they do not receive an implicit fallback. New focused cases check single/double decision parity, foreign PP ownership, missing script support, missing definitions, and a valid-looking numeric ID pointing to another source root.

This is an authority/lifecycle refactor. It adds no move roots, gameplay receipts, RNG draws, save fields, or remote writes. Native UI and audible behavior require their own bounded acceptance; managed structural tests are not a substitute for that evidence.

## Result-owned recovery timing

The public result entry point is `AtResult`. A dispatch carries its genuine source boundary: attack text, caller text, retained attack text, or a hit/recovery/status result. The three text-owned boundaries acknowledge their current page after playback. The result-owned paths resume the same receipt, and the existing animation gate prevents replay at a repeated cursor. Only a hit boundary overrides the scene's target substitute result; recovery keeps the event-time displayed body.

Recovery-family selection now comes from effect metadata rather than another numeric move list. Ordinary recovery requires an owned PP command immediately followed by positive recovery of a living self-target. Rest has a different source order: `trysetrest`, sleep-result text, status-icon update, then `PresentHealTarget` and attackanimation. Its dedicated script is therefore authenticated by the ordered PP → rest-status (source sleep-counter value 3) → recovery prefix and starts at the recovery cursor. Later Lum/Chesto cures do not move that animation earlier or erase the committed sleep result. Neither healing amount nor weather is recomputed by presentation.

### Forced no-PP invocations and launch friendship

Forced Struggle retains the shared attack-text and committed-hit matcher. Only its source identity may use the synthetic move/PP slot -1 with 0→0 PP. The emitted ppreduce command still carries its nominal cost (1, or 2 under Pressure); NO_PPDEDUCT suppresses consumption rather than changing that cost to zero. Actor, target, direct attack identity, sequence and prevention checks remain required. Its later recoil receipt does not own or repeat the move animation.

Return and Frustration use a detached nullable byte on the presentation invocation for source gAnimFriendship. Cmd_attackanimation passes the attacking battle body's friendship through controller buffer byte 10; the event-time body preserves the actor's original friendship under Transform. The renderer does not infer friendship from damage or current party state. Only an invoked friendship predicate requires this input. Return's exact source branch leaves friendship 60 in its weakest category.

Trace clones are raw, uncounted sprite owners whose creating task waits on its own child counter. They use the shared scene's real capacity, including natural failed allocations, instead of a per-move copy cap. The current copy adapter supports visible, non-affine translated battlers. Invocation validation checks reachable callback semantics and conservatively rejects affine, visibility and form-changing companions across participants; unused helpers do not widen that closure. Hidden-but-present sprite identity and affine cloning remain outside this adapter's claim.

Safeguard uses the existing owned five-turn field-setting receipt. Ingrain authenticates the adjacent PP → rooted marker before its attack animation; later root recovery/removal cannot replay the setup. The singles receipt adapter preserves the same root marker for the common matcher.

### Ordered selected status and all-stat secondary results

Protect and Endure authenticate the successful source attempt together with its adjacent owned volatile marker; the attempt remains the dispatch result boundary. Disable preserves the typed disabled-move receipt through the singles adapter. Primary confusion and Foresight use their effect metadata and adjacent owned results. Foresight's legitimate identified 1→1 result is retained, including the source substitute bypass. No selected type, confusion duration, disabled move or success roll is recomputed by presentation.

The all-stats-up secondary effect reuses the successful-hit prefix and authenticates the five ordered Attack/Defense/Speed/Special Attack/Special Defense outcomes. Only the first outcome owns the wave, even when that first stat is capped. Two or more raised stats select the shared all-stat wave; one raised stat selects its individual wave; an entirely capped group does not animate. Source ALLOW_PTR cap messages remain visible because ChangeStatBuffs returns WORKED on that branch. Held-item restoration groups cannot masquerade as this move-owned group.

Plain source PlaySE retains the song's own panning. Panned source wrappers still apply their explicit pan through the same token-owned audio adapter. The sound cue carries that distinction; a plain cue does not overwrite it with an invented zero-pan command.

### Prepared power and selected control results

Dynamic-power execution attaches its existing effective base-power override to the HP or substitute-damage receipt as optional `PreparedBasePower`. It adds neither an event nor an RNG draw. Singles export and normalization preserve the value. Flail's effect matcher accepts the six source power values and supplies the existing animation `MovePower` input; later healing, damage adjustments or displayed HP do not reconstruct that earlier command value. Return, Frustration, Low Kick and Eruption producer tests cover the same field independently of animation admission.

Taunt and Torment require the adjacent PP → owned volatile result. Attract requires the unique successful infatuation setup and retains it when a later Mental Herb clears the state. Follow Me accepts its real self-owned result, including the singles no-redirection marker and repeated doubles setup. All use effect metadata and the canonical script catalog. Follow Me is forbidden to Metronome in the source; called-leaf tests do not invent an impossible invocation.

Torment's raw thought bubbles use scene-wide ascending allocation and template scanning, with the source six-match limit and actual successful-child count. Their original scene owners remain attached while the task's finishing counter changes. Counted thought-bubble overlap is rejected in the invoked closure because the source raw destructor would otherwise leave an unmatched visual count. Authored Taunt and Torment both drain their bubbles before ending. Attract reuses the indexed BG1 overlay authority and preserves BG2 through its separate clear and blend-reset phases.

### Partner support, multi-target status timing and prepared hit counters

Helping Hand authenticates the user's self-targeted PP command and the partner's adjacent successful support marker. Only the visual target changes to that partner. Imprison separately authenticates its ordinary PP command and optional Pressure decrement of the first learned Imprison slot; the two PP commands share an RNG cursor. Neither route infers eligibility from the final battle state.

Teeter Dance's numeric target loop can expose failure messages before its first successful confusion. Its one dedicated root therefore uses `BeforeStatusResult`, at the first ordered `teeter-dance` confusion receipt. Later successful targets do not replay the root. Other targets' failures remain scoped to their targets, and result playback resumes the same receipt without acknowledging it. This reuses the existing result continuation and charge-visibility projection.

Fury Cutter attaches its post-increment counter to the hit receipt as optional `PreparedFuryCutterCounter`, including saturated 5→5 executions which emit no counter-change event. Export and normalization retain it; the visual input accepts only 1–5 and validates a preceding change marker when present. It does not recover the counter from damage or later combatant state. Brick Break's screen branch instead authenticates the ordered opposing-side screen-removal prefix. That branch runs even when the selected target is immune, because the source temporarily clears the immunity flag for attackanimation before restoring it for damage.

## Caller preludes, retained invocations and mutable sprite state

Metronome and Sleep Talk own their dedicated prelude independently of the selected leaf. The canonical caller-selection and invocation-transition receipts authenticate that prelude. Metronome has no PP command at this boundary; Sleep Talk retains its actual caller PP command. This does not admit called-leaf animations through ordinary direct-move matching.

Retained Thrash uses its real unpaired attack-string and opponent hit, without inventing PP. Bide setup uses its owned PP and setup volatile; release uses the released volatile and fixed-damage result, with no fabricated attack-string or PP. Nullable sequence fields preserve these distinctions, and visibility replay starts from the actual authenticated boundary. The battle kernel remains the sole owner of move selection, target choice, damage and RNG.

The renderer separates source battler presence, logical invisibility, sprite invisibility and the built-OAM visibility latch. `monbg` can hide an OBJ while retaining its background copy. Source visibility predicates use the original flag semantics; cancellation restores the invocation's captured flags. Raw battler x/y movement is separate from x2/y2, so Thrash's simultaneous affine and base-position tasks, Teeter Dance and Odor Sleuth clones share one position authority. Camouflage receives the committed native terrain enum rather than guessing terrain from palette or backdrop pixels.

## Charge phases, static backgrounds and detached battler copies

Razor Wind and Skull Bash setup authenticate their owned PP command followed by the charge marker, before charging text. They carry no invented attack-string. Release requires the actual released marker immediately before the attack string and permits the source zero-consumption PP command, including an exhausted final PP. A following actor's headerless charge setup ends the previous action window. Skull Bash's Defense wave is separately owned by its adjacent setup stat result. Psycho Boost reuses the metadata-selected self Special Attack drop, preserving the source two-stage wave even when stage one clamps the actual change.

Mimic authenticates the committed temporary copied-move receipt and replacement PP rather than selecting a move again. Forbidden source copy identities, foreign ownership and malformed replacement slots fail closed.

Static monbg retains the source OBJ while copying its battler background, then clears that background through the source delayed task. Double Team's two raw copies use the shared scene registry and the actor's body material with a detached uniform black blend. This is exact for the reviewed closure, which performs no later clone palette writes; unsupported companion palette mutations are rejected. Whole-plane background enable remains independent of OBJ visibility and alpha. Copies are resolved by the battler composer, never as effect sheets.

ChangeSpriteAffineAnim retains scale/rotation while restarting the shared source program. The common affine player uses source UBFIX's safe zero-axis result; nonzero division and native-width updates remain unchanged. No gameplay state or random stream is affected.

## Expulsion and contributor-owned hits

Roar/Whirlwind authenticate the owned PP command followed by the committed force-out result; Teleport authenticates a self-targeted ordinary escape result. Failure, foreign ownership and copied/called invocations do not authorize these roots. Original party species and global cry liveness remain shared; Roar adds the source forward presets 7 and 8 to the existing cry encoder, without another audio engine. Teleport's source affine duration literal -56 is the native u8 value 200, extracted by the common typed parser.

Beat Up is a contributor loop, not a planned multi-hit summary. Its normalized contributor/hit pairs own each dedicated animation; skipped party members do not increment the visual ordinal. The current hit retains its own Substitute/body route. Pending/orphan members, foreign pairs and fabricated multi-hit summaries fail closed.

Completed battler pose is detached from the finished script. The shared scene continues advancing residual sprites and sorting OAM with retained built offsets, visibility and affine state; it never freezes old sprites or materials. The view retains pose through silent PP and Natural Cure bookkeeping, releases the replaced slot at actual switch-entry presentation, and clears on a superseding animation, cancellation or battle exit. Party-member identity comes from the existing event-time projection. The same projection now supplies singles Substitute state, replacing reverse inference from the final committed frame.

The pose handoff only accepts restored background/material state. Teleport's existing skipped terminal `Ended` page still omits its source fled message; that text boundary remains separate from dedicated animation coverage.

## Normal palette snapshots and retained Rollout inputs

Normal and faded palette states remain separate. Same-bank backup tasks capture immutable normal-state transforms; copying faded to normal preserves every sequential RGB555 rounding step. Restoring normal does not overwrite faded until the next source palette operation. Actual palette/material reloads clear both states, while static battler-background copies and detached clones retain their captured transforms. Small Ember has one indexed owner in the elemental catalog; physical recipes reference that owner instead of declaring an incompatible RGB-only duplicate.

BLDALPHA coefficients preserve the authored five-bit register value, including Overheat's 12/18. The shared compositor saturates each coefficient at16 for pixel arithmetic. Values outside the admitted coefficient fields fail validation before playback. This is shared hardware behavior, not a move-specific clamp.

Rollout's postdecrement counter comes from its owned called-rollout receipt. Initial PP ownership and retained attack-string/no-PP ownership remain distinct. The damage recipe passes its existing calculated power into the existing PreparedBasePower receipt field; neither damage nor RNG is recomputed. Endure/Focus Band adjustments remain ordered between the counter and hit. Raw particles use the single scene registry, with one reviewed task owner and unchanged allocation-failure behavior.

Nightmare copies the target's existing body through that same scene and palette authority. Feather Dance uses the source callback's base coordinates, unsigned falling accumulator and directly written affine matrix, with the existing frame/composition players. None of these roots adds a separate support-ID table.

## Additional monster pictures

Role Play selects an ephemeral additional-mon material from authenticated event-time bodies and party-member identities. The input species owns the source coordinate-table Y offset; personality may independently select an Unown bitmap. Source opposite-actor-party identity selection is preserved even for same-side targets. The selector proves persistent species/PID/OT stability across the batch, rejects identity-changing or missing-party inputs, and never rebuilds a battle frame backwards.

The existing battle-art producer supplies explicit base-frame metadata where ordinary regional display differs (Deoxys). Form0 and ordinary art reuse their existing catalogs. Runtime invocation snapshots detach pixels/indices/palette, while the shared sprite scene preserves an explicitly owned material and binds its palette after OAM capture. Role Play's white silhouette uses the same indexed palette slots, affine matrices, cancellation and scene lifecycle as other sprites. No move-ID support list or separate compositor was added.

## Battler material handoff

Transform and Substitute use the existing replacement dictionary and shared sprite scene. Transform mode0 reuses the source mosaic task; Substitute owns one flatten/hide/load/drop/bounce task. The application matcher authenticates each effect's actual receipt order, including Substitute's PP before the attack text. The replacement body comes from the same forward-only display reducer, never from the final committed frame.

Completed battler state can hold a detached replacement material alongside independent terminal offsets/visibility/affine state. Normal idle composition uses that material until the matching result receipt is projected: current-inclusive in singles, after ACK in doubles. Material release does not discard a separately retained pose. Switch, cancellation and battle exit still clear the occupant-owned state. The former singles-only Transform sound/wait placeholder is removed; authored script timing owns both sounds.

The shared transformed-body selector uses the actor's persistent shiny identity, donor picture/form, and source white coefficient6. Existing base-frame metadata supplies Deoxys's source frame. Actor-already-behind-Substitute controller wrappers remain separate; that Transform invocation fails closed. Front Spinda requires unavailable indexed personality-spot inputs and is not admitted by this material path. These are explicit invocation/material boundaries, not claims of full species/context parity.

Doom Desire's dedicated script is setup only, sharing effect-based Future Sight scheduling receipt ownership and the committed initial animation turn. The later delayed-hit general script is a separate unfinished presentation. The source turn predicate retains argument7 for turns above2; it does not read a gameplay or final-frame counter.

Validation for this material/setup integration: default solution build passed with zero warnings/errors; affected Application72/72 and Presentation45/45 passed. The focused producer checks passed11/11 (material4, delayed setup7), with both maintained root-inventory regeneration checks passing. Native Substitute evidence covers408 callback and402 frame-driver rows; the delayed predicate covers1,024 register cases, and mode0 reuses the existing extracted mosaic traces. Both new material roots compose/replay all six battle layouts with normal idle handoff pixel equality. No full Presentation/all-root campaign, native UI or actual audio-output claim is added.

### Rectangular window color effects

Fake Out introduces one shared WIN0 snapshot and BLDCNT brightness mode, with source-exclusive on-screen bounds and source BLDY saturation. The compositor selects the actual frontmost layer before applying the effect: BG3 targeting cannot darken an equal-colored backdrop, an occluding object or a battler BG copy. Indexed terrain provenance is required. CLR window bits also gate the existing alpha authority; null-window rendering retains its previous path. Inverted/wrapping bounds, layer-disabling masks and concurrent unreviewed register owners fail closed. The source task owns its short transition, palette flash and cleanup; cancellation releases the same state.

Focus Punch's successful hit uses the shared affine/frame players and ordinary authenticated damage result. Its public turn preparation and interruption cases are tested through the existing queue; losing focus produces no hit invocation. The separate pre-turn general setup animation is not included.

## Party synchronization and command-owned item results

Trick and Recycle publish typed success receipts at the actual item command. Optional party writes cannot establish success: equal-item swaps and restored items may produce no such write. Singles and doubles normalize these receipts through the same invocation matcher and use the same source narration selectors; bookkeeping writes remain silent. A received berry's later consumption cannot replace the earlier command identity or replay its animation.

Active party synchronization carries its source battler and an explicit four-slot temporary-move mask derived from the existing Transform/Mimic persistence rules. The producer and event-routing validator authenticate that provenance. A persistent PP command must match its complete before/after chain even if a forged refund makes the party rows equal; temporary battle moves instead preserve the persistent party slots. Neither HP costs nor item changes excuse unrelated PP writes. Legacy event export retains the same evidence.

Destiny Bond, Perish Song and Curse consume their distinct committed result shapes. Perish Song's additional Pressure reduction retains the actual PP-owner slot; Curse selects its animation branch from the authenticated ghost-cost or ordered self-stat result. Its self-stat branch does not invent a generic stat-wave animation.

The renderer shares object-window battler copies, latched per-battler OBJ priority and partial background-palette loads. Partial loads update only their addressed unfaded/faded entries; later normal fades use that loaded baseline. Task-phase priority changes appear on the next OAM capture, while palette writes affect the current composed frame. All copies and temporary register owners use the existing scene cancellation and lifetime authority.

The six-root integration (Destiny Bond, Perish Song, Curse, Trick, Recycle and Silver Wind) brings the canonical resolver inventory to315 unique dedicated move roots. Validation: solution build0 warnings/errors; affected Application166/166, Presentation100/100, Battle70/70 and maintained ResourceImport2/2 (12 Python checks). The representative native comparators cover159 Ghost/Song and115 Item/Wind cases; the same Ghost/Song comparator also passed736 full cases. Existing309 script rows,314 material blobs and46 background material rows remain unchanged. Generated trajectories and game resources remain local ignored inputs. Native UI and actual audio output were not part of this focused gate.
