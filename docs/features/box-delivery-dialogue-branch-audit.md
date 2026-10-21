# PC delivery dialogue branch audit

2026-10-17. Bounded ordinary single-player audit; not complete-world acceptance.

## Concrete defect and change

Renamed PC boxes were already supported and saved, but three delivery adapters reconstructed
`BOX1`, `BOX2`, etc. instead of reading that metadata:

- shared script gifts (Eevee, Lapras, fossils and the other shared gift paths)
- ordinary wild capture delivery
- Safari capture delivery

All three now use the existing `PokemonStorageCustomizationRules.BoxName` lookup. Both the
actual destination and the previous/full box use the saved name; unnamed boxes retain the
existing default. This changes dialogue only, not destination selection, RNG, reward ownership,
capacity rules or one-time completion flags.

Pinned source: `src/scrcmd.c` `ScrCmd_bufferboxname` (line 1725) calls `GetBoxNamePtr`;
`src/battle_script_commands.c` `Cmd_givecaughtmon` (line 9617) does likewise for destination
and full-box buffers. `data/scripts/pc_transfer.inc` shares those buffers across scripted gifts.

## Verification boundary

`BoxDeliveryDialogueTests` supplies controlled delivery-boundary states, including renamed
and default boxes, full-party storage, current-box rollover and duplicate-delivery rejection.
The fixture's ordinary battle is entered by public commands, but its final capture/delivery
state is deliberately prepared. These tests must not be described as a natural gift/capture
playthrough, whole-save validation, or native UI acceptance. Compiler/test execution belongs
to the serial build coordinator; results are pending when this document is first written.

## Additional inspected seams

- `GiveScriptGift` keeps a full-storage failure at result 2, without a receipt or allocator
  increment; it intentionally retains source RNG consumption before failed delivery.
- Eevee source checks result 2 before hiding the actor or setting the received flag.
- Fossil collection only appends a revival receipt when a gift is actually received.
- Togepi gift source/application separates no-party-room handling and receipt creation;
  the egg service is implemented, not a newly discovered missing service.

These are static findings, not exhaustive executable closure for full storage, cancellation,
revisit or Continue. Existing gift ownership tests cover retained completion receipts after
recipient departure; they do not by themselves prove all public acquisition branches.

Final focused gate: all 10 cases passed within the 203-case Quest/capture/Safari/gift cohort (`/tmp/nonmove-quest-box-corrected.log`). Four initial rollover fixtures incorrectly copied party stat caches into storage; rebuilding them through `PokemonStorageRules.Receive` and validating storage fixed the fixtures without changing dialogue assertions or product rules. Full solution and test refresh both compiled with zero warnings/errors.
