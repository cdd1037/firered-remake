# Razor Wind and Skull Bash native callback evidence

Source is local pret/pokefirered commit 037335f4c725d7c9aecdac87066f2002b4bd7e14. No upstream script, ROM, external download or managed product build is executed by this generator.

The generator uses the maintained orbital/spray C harness for the original sprite allocator, callback scheduler, signed-16-bit state, frame/affine player, rot-scale preparation/reset and BIOS integer matrix adapter. New callback bodies are extracted verbatim, with line ranges and SHA-256 in the numerical fixture:

- battle_anim_effects_2.c: AnimRazorWindTornado and its actual affine command array.
- battle_anim_mons.c: TranslateSpriteInCircle, InitSpritePosToAnimAttacker, SetAnimSpriteInitialXOffset, InitSpriteDataForLinearTranslation, TranslateSpriteLinearByIdFixedPoint and SetBattlerSpriteYOffsetFromRotation.
- battle_anim_mon_movement.c: SlideMonToOffsetAndBack and its final cleanup callback.
- battle_anim_effects_1.c: AnimTask_SkullBashPosition, PositionSet and PositionReset.

The native fixture has 104 cases and 2,432 rows, including synchronous creation, each callback/player step and the final destruction row. It covers both singles positions, all four doubles positions, same-side initial-X direction for tornadoes, all three authored tornado phases, negative phase speed, duration-one handoff, signed-16-bit offsets/amplitudes/accumulator wrap, both slide participants, side-sensitive Y inversion, retained x2/y2 and explicit cleanup, both Skull Bash task selectors, hidden-but-present battlers, and full task data/matrix transitions.

Source tick -1 is after CreateSpriteAndAnimate or task initialization. Later rows preserve AnimateSprites before RunTasks. Tornado destruction occurs one callback after installing the stored destructor. Slides likewise install their stored callback on the tick after the final translation, then destroy or zero x2/y2 on the following callback. Skull Bash positioning retains its rotation between its set and reset tasks; it does not manufacture base-position movement. All runtime callbacks use the shared battler state, rot-scale helpers, sprite scene, source frame player and affine player.

The root tests run both original two-turn branches through the canonical resolver, with all source side/slot configurations, deterministic replay, pure composition, exact dependency sets and cancellation at every tick. Existing air-wave, impact, palette, shake and RotateMonSpriteToSide adapters remain shared. No timing is used to choose a gameplay result. Dispatch owns committed setup/release selection separately.

Regeneration, from repository root:

python tests/Game.Presentation.Tests/Reference/generate_charge_pair_vectors.py --root . --output-dir /tmp/charge-pair-review

The generator refuses to overwrite an existing candidate. It uses tests/Reference/reference_c.py for installed portable compiler discovery and compares -O0 and -O2 outputs. Two cold generations produced identical fixture SHA-256 2373e13aa4205413c72cc29d63084afbe7f56a7c802e5f171e504dc167b6ee70.

An explicit Linux AddressSanitizer/UndefinedBehaviorSanitizer run matched all 104 cases and 2,432 rows with -fwrapv. LeakSanitizer itself failed under the execution environment's ptrace wrapper, so it was disabled for the successful address/undefined run (ASAN_OPTIONS=detect_leaks=0). This static-state callback harness does not allocate heap memory. No leak-check pass is claimed.

Managed tests are normal maintained test sources; the worker staged them without running a managed build. Native callback and composition evidence do not constitute a complete gameplay-route, Godot-window or audible-output acceptance.
