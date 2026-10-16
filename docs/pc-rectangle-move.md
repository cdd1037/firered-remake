# PC rectangle MOVE

The shared rectangle transaction follows pinned pret pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14:

- `pokemon_storage_system_misc.c`, `MultiMove_GetMonsFromSelection` normalizes inclusive corners and copies row-major cells, including holes. `MultiMove_RemoveMonsFromBox` clears the whole source rectangle. `MultiMove_CanPlaceSelection` and `MultiMove_SetPlacedMonData` test/write occupied payload cells only. A hole never erases a destination creature.
- `pokemon_storage_system_data.c`, `HandleInput_InBox_GrabbingMultiple` extends while A is held and grabs on release. Returning the selection cursor to its origin is a single grab. `HandleInput_InBox_MovingMultiple` refuses B; it does not restore the source or discard the payload. Horizontal edge movement switches boxes.
- `pokemon_storage_system_tasks.c` routes the selection/grab/place state machine. No repeated single-creature commands implement a rectangle.

Desktop MOVE controls: Space toggles Select/multiple mode; hold Enter on an occupied slot and use arrows to extend, then release Enter to grab. Move the held rectangle with arrows; Page Up/Down changes boxes. Enter places. In multiple mode mouse clicks can choose the first and second corners, then a destination. B/close/party and unrelated creature actions are refused while carrying a group. Complete placement to exit. Selection outlines and cursor position are local; creatures and hole layout are authoritative application state.

`PokemonStorageRectangle` bounds both original corners before normalization. Commands bind every row-major cell's expected identity, including null holes. Grab/place checks the complete rectangle before replacing an immutable box. Rejections retain the input object and cursor. Group ownership participates in the shared current-ownership checks; held creatures must satisfy the same box projection/mail/allocator constraints as stored creatures. Player saves reject the open PC.

Focused tests: `StorageRectangleTests` covers reversed corners, holes overlapping occupants, cross-box placement, item/identity preservation, bounds, occupied/full boxes, full-box source return through explicit placement, stale commands, ownership, B/exit refusal and save refusal. These synthetic current-content cases do not require a gameplay replay. Visual validation remains a separate gate.

The accepted grab/place receipts now drive original hand frames and the whole group's source lift/drop, sharing the single-creature presentation. Only visible slots and selected icons are retained during the animation; the transaction rules above are unchanged. See [inventory presentation](inventory-presentation.md) for source functions, reset behavior and bounded checks.
