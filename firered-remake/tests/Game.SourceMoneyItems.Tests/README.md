# Amulet Coin entry and settlement proof

Run `bash tests/Game.SourceMoneyItems.Tests/run.sh PINNED_SOURCE [CATALOG_ROOT] [ITEM_PACK_ROOT]` with official `scripts/env.sh`. The default mechanics and C15 held-item packs come from the project content; a separate authenticated item-pack root is optional. Generated output stays under `generated/` and uses the shared build lock only for compilation.

The six focused groups exercise source singles/four-position initial entries, both sides, actual reserve switches, departure/faint/replacement, repeated holders, actual Pay Day actions, JSON continuation, multiplier validation, omitted defaults, and 48 locally compiled original-C money vectors. The source probe requires the exact pinned commit and compares each source file's Git blob with its worktree bytes before extraction. It executes no script from the reference repository.

Source arithmetic includes double-battle prize factor exactly once, capped wallet deltas, full u32 Pay Day grants and source u16 text-buffer wrap. Campaign settlement/idempotence and presentation are verified by the integrating campaign owner; this suite does not claim native UI or full campaign verification.
