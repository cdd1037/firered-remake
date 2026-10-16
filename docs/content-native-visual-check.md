# First content batch: bounded native visual check

Checked 2026-10-16, 12:55–13:03 UTC, in the dot cloud Linux desktop using CUA/Sky input and screenshots of the running Godot 4.7.2 application. This is a native visual/interaction smoke check, not pixel parity, full campaign replay, or audible verification.

## Setup and safety

- Used the existing badge-house process, then `bash /workspace/shared/frui.sh m`, `k`, and `p` from the desktop Terminal. No game code changes or builds were performed by this check.
- The controlled `TEST-SYNTHETIC-HATCH-1000-*` checkpoints are test branches, not recovered player history. No save/load keys were used and original player data was not overwritten.
- Audio driver was Dummy throughout. Native screenshots are retained in the CUA tool transcript, outside the repository; logs and fixture manifest are under `/workspace/shared/firered-content-visual/`.
- Viewport was 960×640 at desktop (202,206), with letterboxing around the 240×160 game presentation.

## Observed results

- **Badge topics:** four visible rows render legibly. Five Down presses scroll from Boulderbadge to Marshbadge, with both scroll indicators visible. Enter opens its text. Down during the topic leaves the menu selection unchanged. The window remains drawn while its input is suspended, as intended. Advancing the topic returns to the same scrolled selection. Backspace from the ready choice removes the menu and shows goodbye; Enter dismisses it. No clipping or fault observed. The explicit Exit row and reopen/reset behavior were not separately exercised.
- **Aerodactyl:** Enter opens a complete framed fossil image above the matching `AERODACTYL Fossil / A primitive and rare POKéMON.` caption. Enter closes to the museum; another Enter reopens the correct image.
- **Kabutops:** its separate fixture opens a complete distinct framed fossil image with matching `KABUTOPS Fossil` caption. Enter closes cleanly. Both fossil frames and text fit without overlap.
- **Pokédex Search:** start-menu entry opens National list with Seen 155 / Owned 73. R opens all six order choices: Kanto, National, A to Z, Type, Lightest, Smallest. Down/Enter applies A to Z (Abra, Aerodactyl, Alakazam…); reopening Search and selecting National restores numerical order. Backspace cancels Search without losing the selected numerical row. Type/weight/height sorting were not separately exercised.
- **Pokédex detail:** Bulbasaur's seen-only entry renders its sprite and masked category/height/weight, without description. Tab CRY leaves the screen responsive; the native log records a second `CRY_BULBASAUR_NORMAL` start token. This confirms dispatch only, not audible output. Down selects Ivysaur, then skips unseen 003 to caught Charmander. Charmander shows its sprite, category, height, weight, footprint, and complete wrapped description. Backspace returns to the list. Enter on unseen 003 keeps the list open. Backspace from the list returns to the start menu.

## Limits and handoff

No visual defect was observed in these bounded paths. No claim is made about exact original-game pixel parity, real audio, all topic branches, all search modes, or diploma scenes. Escape closed the final fixture and released the desktop to Terminal (last command: `bash /workspace/shared/frui.sh p`).
