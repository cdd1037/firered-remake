# Shared four-position hit items

The frame adapter now reaches the existing canonical `ApplyHitItems` command for
King's Rock and Shell Bell. No item arithmetic, comparison, eligibility metadata,
or canonical singles implementation is duplicated or replaced. In particular,
King's Rock uses the actual holder's parameter 10, following the existing approved
original-bug correction. The strict modulo comparison, draw-before-flag/HP guards,
ordinary multihit extra final visit, and Gen III first-nonzero Shell Bell receipt
remain intact.

Run from the product checkout:

```sh
bash tests/Game.SharedHitItems.Tests/run.sh COMPLETE_CONTENT_ROOT PINNED_PRET_ROOT NEW_EVIDENCE_DIRECTORY
```

The runner uses `scripts/env.sh`, locked offline restore, the common build lock,
`-m:1 -nr:false -p:UseSharedCompilation=false`, and the official .NET 10 toolchain.
It authenticates pinned pret commit `037335f4c725d7c9aecdac87066f2002b4bd7e14` before
executing authored host C harnesses. The existing original 160 command/sequence
vectors and approved 44-parameter-change vectors are reused. Another 72 vectors
exercise all four actual positions under optimized and UBSan builds; the product's
shared item formulas consume these exact expected outputs. Source checks also
prove changed-item/berry/item/substitute/next-target ordering and that ordinary
move-value cleanup does not clear damage or receipts. The inherited header and
host driver are `../Game.SourceHitItems.Tests/source-prefix.h` and `source-main.c`;
generated C and input paths are recorded in `source/source-report.json`.

`singles-before.json` contains 96 immutable complete public canonical result hashes
captured before any product edits in this isolated snapshot (main commit
`242275ce63a8c1241e5a1ffd068b651141890fc1`, with the applied shared dynamic-power,
freeze and recharge work). Every serialized state, event and RNG field is covered.
Capture refuses both an already migrated frame and an existing output file.
The existing historical 64-phase singles golden is a separate unchanged check.

The focused suite executes real ordered four-action turns, selected allies,
independent BOTH passes, numeric Explosion targets including the ally, ordinary
multihit and its final callback, intact/broken Substitute, first/nonzero/sentinel
receipts, zero-loss boundaries, KO/reward/replacement, contact damage, berry order,
self costs/confusion, delayed MOVE_NONE, serialization and stale continuation
rejection. HP, stages, status, Substitute and retained-register arrangements are
explicit controlled fixtures; positive species/move combinations use authenticated
level-up or TM/tutor entitlements. Negative caller/ability fixtures are intentionally
unsupported. The output retains actual action events, complete RNG traces and full
result hashes in `actions.json`.

This remains a bounded four-slot source capability. Triple Kick, Beat Up, item
transfer/change callers, Sleep Talk, Metronome and other unmigrated effects remain
atomically gated; their original source vectors do not admit their frame recipes.
Serene Grace remains outside the existing ability gate. There is no campaign item
inventory, AI, UI/audio or full-engine/ROM acceptance claim. No public assets are
uploaded and no GUI or campaign replay runs.


## Fixed two-hit moves

`./dev test-fixed-two /absolute/new-output` verifies Double Kick, Bonemerang and Twineedle through the existing multi-hit recipe. `fixed-two-singles-before.json` preserves 24 complete canonical result hashes captured before this admission change. Each move executes from all four actor positions with actual four-action completion; bounded cases cover per-hit Shell Bell, Substitute break, Endure, contact recoil KO, immunity and atomic rejection. The source's one final Twineedle secondary command and approved strict comparison remain unchanged. A doll-only first hit has no physical HP-loss receipt and therefore does not make an extra King's Rock draw.
