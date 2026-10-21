# Itemfinder presentation

The common overworld importer now emits one 16×80 native sprite sheet alongside the existing overworld atlases. It uses the original player OBJ palette (palette zero), not Unicode arrows or a new font. Runtime reads only the generated sheet.

The read-only presentation follows `src/itemfinder.c`: response pulses every 25 frames, two or four increasing-strength arrow frames for ordinary pickups, three star pulses for underfoot items, center (120,76), signed 8.8 movement of ±100 per frame, and the original destruction boundary. The direction comes from the existing authoritative scan; an ordinary item on the player's tile follows the current facing, while underfoot items use the upward star. Each pulse emits `SE_ITEMFINDER` once. Message confirmation is available after the source pulse sequence; no key held during it is queued as a confirmation.

Item ownership, bag capacity, flag changes and digging remain the existing Application transactions. This change adds no save fields or history. Scene texture ownership releases the generated sprite with the other scene resources.

Verification: source-pixel check in `scripts/checks/test-itemfinder-presentation.py`; timing, signed-motion, boundary, no-response and underfoot cases in `ItemfinderPresentationTests`. Native-window and actual audio output checks are separate; their completion is not implied by these checks.

Actual window, 2026-10-16, MVID `5518e114-c267-4db6-91cb-3a5359964505`: from a labelled Route24 test fixture, registered use showed the native upward arrow pulses and both source response text pages, then returned to field. Source sound events were emitted four times; driver was Dummy, so no audible verification is implied. Underfoot timing remains covered by pure tests, not this window sample.
