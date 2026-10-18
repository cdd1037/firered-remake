# Reaction callback source-C vectors

This fixture covers the newly translated mechanics used by move roots 161 (Tri Attack), 263 (Facade), 265 (Smelling Salt), and 348 (Leaf Blade). It does not claim an independent oracle for every older callback in those complete scripts, or native visual/audio acceptance.

## Regenerate explicitly

From the repository root:

```sh
python -B tests/Game.Presentation.Tests/Reference/generate_reaction_vectors.py --root . --output-dir /tmp/firered-reaction-vectors
```

The generator imports the maintained authored source-player extraction from `generate_orbit_spray_vectors.py`, verifies pinned input hashes, extracts source C verbatim, and compiles only that authored harness. It does not execute upstream scripts, invoke .NET, or consult the managed implementation to construct expected values. Review the candidate before replacing `Fixtures/reaction_vectors.json`. Normal tests only consume the checked-in fixture.

Observed toolchain: Python 3.12.14; cc (Debian 14.2.0-19) 14.2.0. Authored-C compile command: `cc -std=c11 -O0 -fwrapv reaction_source.c -o reaction_source`. The signed arithmetic shift assumption is asserted. The reuse layer fixes the source ARM affine union ABI to eight bytes and provides bounded pointer handles and BIOS affine calculations.

## Evidence and coverage

- 620 extracted-C scenarios run; 138 focused positive scenarios and 8,226 complete rows are selected for the maintained fixture.
- The focused JSON is about 6.5 MiB. Full matrix gzip, per-kind files, emitted C/binary, row stream, and explicit invalid/failed-init probes are diagnostic output only and should not be added to the product.
- Tick -1 is after synchronous initialization. Each ordinary row records sprite callback/frame/affine work before tasks, then post-task state. Source `CreateSprite` sets the sheet's first tile immediately: newly allocated Leaf Blade controllers/trails already use tile28, while `StartSpriteAnim` defers a different selected frame until the next ordinary sprite pass.
- Cases include all canonical Triangle actor/target pairs, both battle sides, single/double positions, picture widths/heights8..64, two valid Y-offset profiles (including an exclamation whose unbounded top is below8), both hands, repeat/delay boundaries, all24 Facade colors, every RGB555 entry, and unchanged unfaded/unrelated banks.
- Source tasks/children retain every relevant signed word. Task affine pointer words are stable host handles `(1,0)`; Leaf Blade words13/14 are real counters and remain exact.
- Allocator mode0 is ordinary first-free; mode1 holds the initial Triangle/Leaf controller at slot32 and then releases lower slots before ordinary stepping; mode2 leaves only slots61..63 free, covering partial sweat emission and leaf-trail allocation failure. Fake occupied slots have no ownership or callback behavior.
- Facade and Smelling Salt task affine initialization, repeated preparation, ordinary updates, foot anchoring and restoration use extracted source task/sprite helpers. Battlers use the actual `sAffineAnim_Battler_Normal` data. An initial harness attempt with a dummy END-only battler table asserted after restoration; it was rejected, and the correct source normal-affine table was extracted before producing these vectors.
- Source-C assertions verify raw-child/task counters at every row, no unrelated palette writes, preserved unfaded colors, terminal sprite/matrix release, bounded execution and failed allocator handling. The final fixture independently passed shape, duplicate-slot, uninterrupted-tick and terminal-ownership checks. Recompiling the same authored C with `cc -std=c11 -O2 -fwrapv reaction_source.c -o reaction_source_o2` produced a byte-identical row stream to the `-O0` build.

## Exact outcomes (ordinary ticks)

| Mechanic | Final tick | Source ownership result |
|---|---:|---|
| Triangle |81|One counted sprite and one effect matrix released|
| Facade, one squish |25|Eight droplets; at most four live|
| Facade, three squishes |75|24 droplets; at most four live|
| Facade color duration72 |72|All16 original entries restored after72 colored writes|
| Smelling hands, one/two squishes |45 /59|One counted sprite released|
| Exclamation, delay8/blinks3 |39|Third hidden transition destroys sprite|
| Smelling squish, one/two |12 /25|Target affine/offset restored; source direction word never increments, so every shake write is +2|
| Leaf Blade, ordinary/lower-slot |136|77 trails total; maximum11 trails plus one controller =12 owned sprites|
| Leaf Blade, only three free slots |126|14 trails plus controller allocated successfully; failed trails do not increase parent count|

The first ordinary Leaf Blade peak is tick10. The source root waits for its trails before creating Cross Impact. A broader admitted linear program may need one additional capacity cell for an overlapping allowed Cross Impact; that is a validator proof, not the leaf callback's intrinsic occupancy.

## Deliberate boundaries

The callbacks have no cancellation entrypoint. `ReactionSourceVectorTests` first establishes live state from source rows at eight cancellation boundaries, then separately tests the host cleanup contract: raw and counted sprites, tasks, matrices/offsets, palette state and idle-frame resurrection. This is not mislabeled as source-C cancellation behavior.

Zero-squish Facade destroys its task and then still prepares affine state. Exhausted initial Leaf Blade allocation destroys its task and then writes sprite index64. The C harness backs that sentinel with a nonanimated extra cell to observe this unsafe path; it does not treat it as valid sprite allocation. These remain separate diagnostics and runtime rejection tests, not clean parity cases. A zero exclamation delay is also excluded from admitted positive cases. Smelling attacker-selector squish is a valid immediate no-op and remains covered.

Battler palettes are bound to source OBJ banks0..3. Managed battler colors are retained as symbolic blend operations, so tests compare all16 final rendered RGB555 values against C while independently checking untouched unfaded input and unrelated banks. Dummy END-only sprite frame tables intentionally retain source `animBeginning`; their normalized managed dummy-frame bookkeeping is not asserted. Rendered image/flip/motion and lifetime remain asserted. Leaf and Triangle real frame/affine timing is compared explicitly.

## Provenance

Pinned source revision: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

`sourceSha256` in the fixture contains every whole-file source digest. `sourceSpans` contains inclusive line numbers, zero-based UTF-8 byte offsets, exact byte lengths, and full SHA-256 digests for each extracted callback/declaration, including the shared source player. These metadata belong to fixture/import tooling and do not enter runtime resources.

Focused fixture SHA-256: `20ca0b285a6f29a56fc4491c3bd5616359e64744532409a9b0ec640016ca2095`.

### New callback spans

| File and lines | Callback/declaration | SHA-256 |
|---|---|---|
|src/battle_anim_effects_1.c:3333-3357|`void AnimTask_LeafBlade(u8 taskId)`|`94c48ff1d2c19a2520695471456325b363dc44ec339773adec4f41380380a8a9`|
|src/battle_anim_effects_1.c:3359-3543|`void AnimTask_LeafBlade_Step(u8 taskId)`|`5623a3e01701b55c7a3e97f2ef08d4c5f5c74d047779c313b6150c64d36abb33`|
|src/battle_anim_effects_1.c:3545-3553|`s16 LeafBladeGetPosFactor(struct Sprite* sprite)`|`a78bfc6a467fc7c2afeb79d30a91c93a3ca1cbff3c2c0e693cd667c78cfe622e`|
|src/battle_anim_effects_1.c:3555-3579|`void AnimTask_LeafBlade_Step2(struct Task* task, u8 taskId)`|`1dfdc9a3772a62a2eb4bc93b71363fda97427cfafdb2f2f44fd08f1e98d15393`|
|src/battle_anim_effects_1.c:3581-3595|`void AnimTask_LeafBlade_Step2_Callback(struct Sprite* sprite)`|`a879d435dba1771909a3e6a08004cc2cc0b2a0137eef920e45fc175a28b5f5e4`|
|src/battle_anim_effects_1.c:3668-3697|`void AnimTask_CycleMagicalLeafPal(u8 taskId)`|`edcf55f0b2785374b112b356fcc97a239ba3d9203f72b2386e12666c82a23e8b`|
|src/battle_anim_effects_3.c:1988-2017|`void AnimTriAttackTriangle(struct Sprite *sprite)`|`67557732d46f7adfddab35c440974446c584f7cfe05da3c27512401be4cbc291`|
|src/battle_anim_effects_3.c:3669-3692|`void AnimTask_SquishAndSweatDroplets(u8 taskId)`|`f111279259c9ded95e856086aead42da347d01350082c123e1b398cce7f86522`|
|src/battle_anim_effects_3.c:3694-3729|`void AnimTask_SquishAndSweatDroplets_Step(u8 taskId)`|`5b4069263d228f401d4cf6a6370f6a999a75a1db7c7d00a9e030b5450079942c`|
|src/battle_anim_effects_3.c:3731-3771|`void CreateSweatDroplets(u8 taskId, bool8 lowerDroplets)`|`15ceeb8ddcaf992ee87caf423611ad3b6c89307597e3867c6aca8915ba613cfd`|
|src/battle_anim_effects_3.c:3773-3782|`void AnimFacadeSweatDrop(struct Sprite *sprite)`|`7625aa8497a2b5cc182b16fb737b2fbdc3a23aeaaf39eb89e2009192b2312fc7`|
|src/battle_anim_effects_3.c:3802-3811|`void AnimTask_FacadeColorBlend(u8 taskId)`|`840eaa98a0416831ba0b2db008d946e407c355092101ca42cc3c55f1f5624914`|
|src/battle_anim_effects_3.c:3813-3828|`void AnimTask_FacadeColorBlend_Step(u8 taskId)`|`de1d8053a574ecd5d33ba2d97b6b0d1b33442ca8c1e781f934519b3c9b61a264`|
|src/battle_anim_effects_3.c:4138-4162|`void AnimSmellingSaltsHand(struct Sprite *sprite)`|`aa95d386aeae32eb20ed3713b0049882e1991eddaa72c0f7fabf7ca65cb62ec6`|
|src/battle_anim_effects_3.c:4164-4208|`void AnimSmellingSaltsHand_Step(struct Sprite *sprite)`|`7595e20e2b7687d23acaae16d3193e4c316048a3dc0751fa31c7c3536c75b1d1`|
|src/battle_anim_effects_3.c:4213-4226|`void AnimTask_SmellingSaltsSquish(u8 taskId)`|`9e18408eaea49c10586d1dacf5eaa969e8ecb7cbdacfc367ed6d5160758b714c`|
|src/battle_anim_effects_3.c:4228-4255|`void AnimTask_SmellingSaltsSquish_Step(u8 taskId)`|`c54e55858447637d2db8215dd473f4488eacfdcfc02947ef94ea49eece1a6d06`|
|src/battle_anim_effects_3.c:4261-4282|`void AnimSmellingSaltExclamation(struct Sprite *sprite)`|`82bf0b2117e6205e673289dc033d1b177f42f5cdbe878876817ab8eb726d0032`|
|src/battle_anim_effects_3.c:4284-4294|`void AnimSmellingSaltExclamation_Step(struct Sprite *sprite)`|`929d4dd21dc81926b2a3451c0d2aeab7509575d17c2c804479c7432cd78d12bc`|
|src/battle_anim_effects_1.c:897-960|`const union AnimCmd sLeafBladeAnimCmds1[] =`|`4b27f5cca4e7b3a51f4a805cd66c9f5c7dee3eeab6db23022b646f1e54a001a3`|
|src/battle_anim_effects_3.c:431-466|`const union AnimCmd sTriAttackTriangleAnimCmds[] =`|`a0925cb8af018af00861708bebc8279dc5777156483da9ff4f6d9b2d9975127c`|
|src/battle_anim_effects_3.c:3646-3798|`#define IDX_ACTIVE_SPRITES 2  // Used by the sprite callback to modify the number of active sprites`|`cd9a1b3e7021eae3e5de7eaf7ce9dcf7c05e2cc63b3fcfd44656c2f4637d488a`|

Managed parity/cancellation tests were authored and statically reviewed in the staging task; no managed build or test execution was authorized there. Run the repository’s normal serial managed validation after integration before reporting these tests passed.
