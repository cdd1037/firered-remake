# Static horizontal fog: source phases and local oracle

Reference: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14, retained local inputs. No downloads and no upstream scripts executed.

## Source model

- field_weather_effects.c:1308–1355: FogHorizontal_InitVars resets scroll only when fog sprites do not already exist. InitAll calls InitVars and loops Main synchronously until graphics are loaded. Main computes the 8-bit X position from the previous scroll offset, increments the counter, increments the u16 scroll offset every fourth call, creates sprites once, and targets12/8 for WEATHER_FOG_HORIZONTAL.
- field_weather.c:967–1023: Weather_UpdateBlend changes one coefficient every4 calls, alternating EVA and EVB. Starting0/16, the final12/8 arrives on synchronous Main call93. This is not93 visible frames. Exact signature: initStep2,loaded1,offset23,counter1,posX233 at cameraX0,EVA12,EVB8,blendCounter0,blendUpdateCounter23.
- Subsequent stationary callback clock f=0 at the end of fresh InitAll: offset=(23+(f+1)//4)&65535; counter=(f+1)%4; posX=(cameraX−23−f//4)&255. The resulting position repeats after1024 callbacks. Position uses preincrement offset, so at f3 offset24 but posX233; at f4 posX232.
- Repeating InitAll with existing fog sprites uses2 synchronous Main calls, preserves scroll and12/8, and resets the blend target counters. It does not describe a full same-map warp: ResumeMap resets tasks/sprites, and StartWeather sets fogHSpritesCreated false (overworld.c:2103–2122; field_weather.c:144–170). A full same-map warp therefore takes the fresh path.
- field_weather.c:351–395,429–438,690–758: Fog arrival uses FadeScreen's weather branch, with no normal software palette fade. Each FogHorizontal fade callback applies15…0. Callback16 still returns TRUE at coefficient0; callback17 returns FALSE and switches weather state to idle. Repeating WarpFadeInScreen resets this fade counter and black fill but preserves fog scroll and12/8.
- field_weather.c:208–240: the first weather task callback runs InitAll and changes the task function without processing the fade that callback. Task priority80 is after arrival priority10 (field_fadetransition.c:242–279; task.c:50–85,118–131). Thus fresh plain and stair arrival callback1 initializes fog; callbacks2–17 apply15…0;18 marks weather idle;19 releases the arrival task. Task_ExitStairs's duplicate WarpFadeInScreen is before first weather init and does not shift this bound.
- Arrival f1 is steady fog elapsed0. Arrival f19 retains elapsed18,offset27,counter3,pos229 at cameraX0. Ordinary field continuation must preserve that state rather than restart fog after release.
- field_fadetransition.c:441–460,690–710,794–970: plain departure remains24 callbacks, delayed music ready65 releases66, stairs remain36. Fog departure first copies faded into unfaded palettes once, then uses the ordinary split-bank software fade (field_weather.c:734–741; palette.c:151–187,393–465). Plain departure BG/OBJ coefficients at16 are16/14; at17 they are16/16; software-active clears22; task advances23 and transfers24.
- field_weather_effects.c:1389–1397: sprite y2=(u8)cameraY. Sprite x=posX+32+column*64; if x>=272 then x=(480+posX−(4−column)*64)&511. The five column positions at posX233 are265,9,73,137,201.
-12/8 hardware alpha means fog contribution12/16 plus destination contribution8/16, with normal hardware channel clipping. It is not standard foreground-opacity.75 compositing. This phase oracle does not verify composited pixels.

## Camera ordering

OverworldBasic (overworld.c:1460–1471) calls RunTasks, AnimateSprites, CameraUpdate, UpdateCameraPanning, BuildOamBuffer and UpdatePaletteFade in that order. Main consumes the previous cameraX; fog SpriteCallback consumes the previous cameraY. UpdateCameraPanning is extracted in the staged camera trace. Its movement inputs are explicit host fixtures; full map/camera object movement is not reimplemented.

Fresh loading nuance: ResumeMap calls ResetSpriteData, which resets camera offsets to0/0 (sprite.c:300–301). Map load then initializes total offsets to0 and vertical camera pan32 without calling UpdateCameraPanning before first OverworldBasic (overworld.c:1848–1932,2103–2142; field_camera.c:513–527). The first black init frame's fog sprite y2 is therefore0. That frame's end updates camera to0/-40. The first post-init callback reads0/-40 and stores y2=216. Subsequent fog positions lag the new movement/panning by one callback, as source ordering requires.

## Artifacts and commands

- generate_field_fog_vectors.py: authored generator, importing maintained reference_c/generate_field_warp_vectors/generate_field_stair_vectors helpers. If copied next to those helpers, --helpers may be omitted.
- field-fog-cases.json:19 small explicit hand-authored semantic cases. The generator requires all19 distinct traces, checks decisive frames, all emitted steady-scroll formulas and every arrival coefficient/readiness row. It fails on missing/duplicate traces or missing sprite/camera evidence.
- field-fog-source-vectors.json: ignored/local generated evidence,19 traces and627 phase rows; additionally12 sprite-position rows and5 camera-order rows. columns/spriteColumns/cameraLagColumns describe their schemas.
- field_fog.c: local retained extracted host for inspection/recompilation. Source callbacks are copied verbatim except two function-symbol renames for counting/wrapping. Hardware, audio, sprite allocation, unused weather functions and map preview are bounded stubs. Palette coefficients are recorded at the original BlendPalette/ApplyFogBlend seam; this does not verify RGB555 palette values, sprite allocation success, walking gait or hardware-cycle timing.
- Main phase trace cameraX/Y columns describe the supplied camera fixture. Most are fixed0/0; signedCameraScroll uses-17/-9. offsetX/Y are player stair offsets. Actual changing camera ordering is represented separately by cameraLag. The seeded u16ScrollWrap and signed-camera frame0 positions are intentionally stale before the next Main computes a position.

From the project root:

    python tests/Reference/generate_field_fog_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-fog-source-vectors.json --retain-c /tmp/field_fog.c

Validation performed: generator succeeds; independent rerun output is byte-identical; C output at-O0 and-O2 matches for every emitted row (cc Debian14.2.0). The generator pins all required source bytes and checks priority/frame ordering. These source-oracle checks are separate from the managed phase/composition and public-route tests. Native rendering and audible output remain unverified.

Static map identity was checked directly from weather metadata, yielding19 source maps: Pokémon Tower3F–7F and Lost Cave Rooms1–14. The harness has no map-name allowlist and no runtime metadata change.
