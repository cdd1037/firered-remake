# Fame Checker and slot presentation

Pinned reference: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/fame_checker.c`, `src/text.c`, `src/text_printer.c`, `src/new_menu_helpers.c`, `src/slot_machine.c`, `src/palette.c`, `src/blend_palette.c`, `src/sprite.c` and `src/trig.c`.

## Fame Checker

`FameCheckerTextPages` retains the admitted COLOR, SHADOW and FONT commands as zero-width printer steps, including consecutive commands at the same text offset. Expanded PLAYER/RIVAL names are literal text; they are not reparsed as controls. Existing styled runs and the retained row are preserved.

`SourceTextProgress` continues to own ordinary glyph/LF cadence and A/B speed-up. Its optional formatting offsets follow the same original RENDER_REPEAT path. `FameCheckerTextPresentation` adds only clear/scroll prompt states: a fresh press accepts a prompt, clears or scrolls, and preserves the printer delay and speed-up latch. Scroll moves the source 15-pixel line distance by 1/2/4 pixels per frame for SLOW/MID/FAST, followed by the source state-transition frame. Selection changes create a new printer.

The message draws at source window y+1, clips partly scrolled glyphs to the 208×32 message window, and uses the last line's currentX/currentY for the continuation arrow. Font atlas textures are cached by style, rather than rebuilt while scrolling. CANCEL uses the source immediate printer so accepting it does not wait for the configured text speed. Closing/reopening clears the transient printer. Collected facts, application commands and persistence are unchanged.

## Slots

Source comparison confirmed that the existing 24/28/12-frame Clefairy animation durations are correct: sprite.c decrements duration on the initial frame. They are retained. Dance/faint selection now follows source task activation and cleanup, including the transition frame before the new task runs.

Winning-line palette 6 now follows the exact pinned integer sine coefficients, preserving palette entries 2 and 4. Payout lights follow 0,1,2,1 at nine-frame intervals; the initial task frame only binds winning lines and does not pulse. The offline importer bakes only the distinct required palette strips into the existing atlas. Every variant shares its original line geometry. Runtime reads generated images/layout only.

Bet lights stay visible until the source resets them, including the exit fade after an unplayed wager is refunded. Flash cleanup restores winning lines before the later all-line reset. Normal palette fades honor alternating BG/OBJ transfers; the next Y counter is not mistaken for an already-transferred palette. Software palette fading precedes hardware scanline darkening, preserving five-bit rounding order. The two static message windows use the original standard border, palette, font and two-line spacing, baked into the same atlas. They remain visible during the exit fade and pass through the same BG palette blend. The YES/NO choices use the source user-selected border, 8-pixel cursor inset and separate 14-pixel text rows. The compositor reuses output/background buffers and the Godot Image/ImageTexture.

Wallet changes, wager rules, RNG, reel stopping, payout, fanfare and existing approved rule differences are unchanged.

## Verification boundary

- Offline regeneration passed with the retained local pinned source.
- The independent palette check reconstructs 54,272 winning-line pixels from original tile indices, map flips and palette entries across every distinct pulse coefficient. It also checks 3,724 pixels across both complete message glyph layouts against the raw font, the original integer sine vectors and exact regeneration.
- On 2026-10-16, the Godot build with normal project dependencies passed with zero warnings/errors. The refreshed Presentation assembly passed 24/24 focused ordinary text, Fame and slot cases; the ResourceImport wrapper passed 1/1. These cover control-step timing, scroll speeds, prompt delay/latch continuity, immediate CANCEL text, all imported fact/pick body decoding, palette-half transfer vectors, and Clefairy loop boundaries.
- These source-vector and build checks do not establish every native UI state, pixel, frame or audible output. Representative native Fame scrolling and slot payout/cleanup samples remain the visual verification gate.

Commands:

```sh
python scripts/imports/items/import-slot-machine-presentation.py . --dependency . --check
python scripts/checks/test-slot-machine-presentation.py
source scripts/env.sh
dotnet build src/Game.Godot/Game.Godot.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~TextProgressTests|FullyQualifiedName~FameCheckerTextPresentationTests|FullyQualifiedName~SlotMachineVisualPresentationTests' -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~SlotPresentation -m:1 -nr:false -p:UseSharedCompilation=false
```
