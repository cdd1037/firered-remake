# Intro movie presentation

The opening movie uses locally retained pret/pokefirered revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/intro.c` and `graphics/intro`. The offline importer does not execute reference code. Regenerate and check:

```
python3 scripts/imports/presentation/import-intro-movie.py
python3 scripts/imports/presentation/import-intro-movie.py --check
```

Runtime reads only `content/generated/ui/intro-movie`. The package holds independent tilemap layers and sprite strips, including the four-part Gengar OBJ assembled once. It does not bake full-screen animation frames, store source hashes, or read raw source. Palette-bank selection and horizontal/vertical tile flips are resolved offline. Runtime uses wrapping layer offsets, sprite frame regions, transforms, fades and theatrical masks.

Coverage: unskippable copyright; opening theatrical window; star and sparkle trail; Game Freak name/logo/presents reveal; animated close-up grass and forest zoom; opposing parallax forest/plants with the wide-shot Pokémon; vertical opposing close-up pan; battle entrance and foreground grass; Nidorino cry; Gengar wind-up/swipe; Nidorino recoil/dust/return hop; two preparation hops; shake, charge and jump; white background fade, Pokémon zoom, black fade and title handoff. Enter/A, Tab/Start and Space/Select skip after copyright. Backspace/B does not skip. R is accepted as the current shared input adapter's normalized Select alias. Escape remains the host's quit control.

The BCL presentation clock advances at 60 Hz and is independent of gameplay/session/RNG/save state. Sparkle randomness has a private fixed source seed. Source callback waits and sprite animation values are represented; DMA/setup waits are collapsed, and palette timing plus some particle/scanline behavior is approximated rather than claiming frame-exact hardware emulation. The source's title restart path can restart the movie; ordinary Continue/new-game/naming flows are owned by opening integration. Controlled native checks are scoped in [validation](validation.md); they do not establish a natural full movie playthrough or real-speaker output.

Fight positions are retained across presentation-stage boundaries. Background scrolling accumulates the source's 4-pixel steps, then 1/8-pixel steps after the foreground grass reaches x <= 52; changing speed never recalculates the already visible scroll phase. Drawing uses the integer pixel offset exposed by the source BG register, not fractional texture positions. The same grass keeps its position when the entrance finishes and exits at x <= -32. Gengar's swipe returns to sine index 64 and the normal pose, stays there while Nidorino recoils, and resumes its retained idle clock afterward. The zoomable Gengar sprite replaces the background only in the normal pose. `IntroMovieContinuityTests` covers these position, pose and frame-partition invariants; it is not a full native-frame acceptance claim.

Tests: `IntroMoviePresentationTests` exercises stage coverage, delta partition invariance, source skip rules, invalid clock input and generated-only load. `IntroMovieImportTests` checks byte-exact regeneration of the compact generated package.

Music cues use the shared synthesized MUS_GAME_FREAK and MUS_INTRO_FIGHT playback. Nidorino now uses the source CRY_MODE_DOUBLES (1) preset: length 20, release 225, pitch 15360, chorus 0, volume 120 and pan 63, without BGM ducking. `SpriteCB_NidorinoCry` waits nine crouch callbacks and plays the cry on the next callback; the presentation clock queues it at Cry frame 10. Consuming that cue is once-only even when a delta crosses the whole Cry stage. Skipping clears a pending cue, a new movie arms it again, and a completed movie does not play a late catch-up cry.

The host resolves `CRY_<species>_DOUBLES` from that species' existing normal binding on demand. It does not duplicate species metadata or mutate the stored catalog. Shared normal (0) and encounter (2) entrypoints retain their parameter and mode contracts; unsupported species/modes have no normal-cry fallback.

## Import and continuity verification

The L-mode Game Freak name PNG follows pinned gbagfx's inverted nibble convention: white source pixels become transparent index 0, black glyph pixels become palette 15. `python3 scripts/checks/test-opening-grayscale-import.py` independently reconstructs these values from raw grayscale pixels; the same helper covers the title border and NPC wireless signal.

`IntroMovieContinuityTests` guards background phase, grass position, Gengar's settled pose and clock partitioning. Independent native captures compare actual `CampaignView` output against source textures at entrance/wait, slowdown, swipe/recoil and BG-to-OBJ handoff boundaries. The current acceptance scope and retained counterexamples are in [validation](validation.md), not inferred from a passing build or importer. Full source DMA/palette/particle/scanline equivalence, every natural movie frame and audible output remain unverified; controlled runs used disabled/Dummy audio.
