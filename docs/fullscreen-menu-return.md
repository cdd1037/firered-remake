# Full-screen menu return

Bag, Party, Options, Trainer Card and Pokédex return through one presentation controller. Save dialogs and Exit remain field-owned and do not use it.

1. Retain the outgoing logical 240×160 page and run that screen's outgoing palette profile. Bag also masks the page from top to bottom in 16-pixel steps.
2. Keep the display black while reconstructing the field and Start menu. The setup duration depends on the number of Start entries, printed two per original callback.
3. Reveal the field, actors and complete Start menu together through the existing weather-aware BG/OBJ arrival palettes.
4. Unlock menu input only after the weather and Start task finish. Start still owns input; field movement does not resume underneath it.

Commands commit before presentation begins. Rejected commands do not start a transition. Rendering snapshots do not own or replace campaign state.

## Callback boundaries

One model advance is one original main callback, not a claim about measured GPU frames. F0 is the callback initiating the outgoing fade, after that callback's palette calls. Input-to-F0 lead-ins are Bag 1, Party 0, Options 1, Trainer Card 0 and Pokédex 2 callbacks.

Bag and Party use palette delay −2 and select the return callback at F14. Options uses delay 0 and selects it at F23. Trainer Card advances the palette in both its closing task and outer callback and selects return at F11. Pokédex has no outer palette advance in its close callback and selects return at F24. Selecting a callback is not invoking it: the following callback is R0.

R0 invokes the return callback and selects the local field loader. R1 resets/reconstructs the field. The Start fade request is R(9 + ceil(entry count / 2)); the next callback installs VBlank/Overworld, and only the following callback advances weather. There are no weather ticks during black setup. Five/six entries request fade at R12; seven/eight at R13. Normal-weather Start input unlock is respectively R38/R39.

Pinned source: `content/raw/pokefirered/037335f4`. Relevant code is `item_menu.c` (Bag close and Win0 animation), `party_menu.c` (HandleChooseMonCancel and Task_ClosePartyMenu), `option_menu.c` (close state machine), `trainer_card.c` (closing task and CB2 palette calls), `pokedex_screen.c` (CB2_ClosePokedex), `overworld.c` (CB2_ReturnToFieldWithOpenMenu/ReturnToFieldLocal), and `start_menu.c` (FieldCB2_DrawStartMenu/DoDrawStartMenu).

## Rendering boundary

Outgoing pixels are transformed using RGB555 palette arithmetic rather than an alpha overlay. Bag's Win0 mask is represented by a top-down black rectangle: input callback height 0, F0 height 16, through F9 height 160. Incoming field and actors retain separate palette banks. Start glyphs are preloaded before drawing any labels so lazy atlas replacement cannot invalidate earlier label draw commands.

The outgoing page is a composed snapshot, not a reconstruction of all original BG/OBJ layers. It uses the BG coefficient for the whole image, so it does not reproduce the original one-callback outgoing sprite-bank offset. Outgoing animations remain frozen. Pokédex's pre-close cleanup delay is modeled, but its separately removed windows are not removed from the frozen snapshot. These are explicit limits, not full hardware pixel-parity acceptance.

## Verification

`FullMenuReturnPresentationTests` contains 14 cases: five independently traced outgoing profiles, eight entry-count/weather setup and input-unlock cases, and a field-owned Save/Start exclusion case. The existing arrival-only source fixture remains independent and does not establish the outgoing half. Native checks must separately verify page capture, repeated exits/resource retirement, absence of field flashes, intact glyphs and input lock. Source callback tests do not replace native visual evidence.
