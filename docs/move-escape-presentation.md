# Move escape result and controller boundaries

## Successful Teleport result

Pinned `data/battle_scripts_1.s:1921–1934` runs the move animation, prints `STRINGID_PKMNFLEDFROMBATTLE`, waits, then records the escaped outcome. Previously the successful producer published an ordinary terminal event and `CampaignSession.PrepareWildPresentation` skipped it, omitting the text after the dedicated Teleport animation.

The existing terminal event now carries an optional typed move-escape receipt containing the actual invocation actor and move. The singles adapter preserves it; animation admission checks that same actor/move ownership. Application pauses this terminal receipt and the shared source-text model selects `sText_PkmnFledFromBattle`. Ordinary Run, escape items, forced expulsion and other outcomes do not acquire this page. No event, sequence, battle outcome, PP rule or RNG draw is added. This uses the existing acknowledgeable battle-page pacing; it does not claim source scheduler or 64-frame wait parity.

Added checks cover genuine player/opponent Teleport through public campaign commands, announcement/result order, exactly one result page before settlement, no repeated party/PP or RNG changes on dismissal, ordinary Run separation, source/singles serialization, and missing/foreign/malformed escape metadata. These receipt/text changes were integrated and verified in the first audit-repair checkpoint; the original pending-integration wording is historical. Native-window and real-audio evidence remain separate.

## Shared Substitute controller gap: historical finding, now closed

The following records the original audit finding. The shared wrapper is now implemented and verified; see [Substitute move-controller presentation](substitute-move-controller.md) for current behavior and exact acceptance boundaries. This was a reachable presentation gap, not a missing Transform rule. Pinned `src/battle_controller_player.c:2338–2363` and the opponent equivalent at `1266–1290` wrap moves used behind a Substitute with `Special_SubstituteToMon` and `Special_MonToSubstitute`. The two programs in `data/battle_anim_scripts.s:11097–11103` call `AnimTask_SwapMonSpriteToFromSubstitute`.

At the time of that audit, the source-animation scene supplied Substitute pixels as the acting battler and had no implementation of that shared callback/wrapper. `MaterialChangeAtInvocation` explicitly rejected an actor already behind a Substitute; consequently its Transform root was skipped. A legal ordinary single-player setup is a Substitute-tutored Eevee with level-36 Baton Pass passing the doll to caught Ditto, whose level-1 move is Transform. `SourceSwitchRules.PrepareBody` already retains Substitute HP on Baton Pass.

The coherent fix belongs to the shared controller-level body/doll transition and invocation lifecycle, including multi-hit and cancellation ownership. Merely allowing Transform's material request would animate the wrong body. Front Spinda's missing personality spot material is independent; no ordinary single-player acquisition or opponent route was established, so it is not a demonstrated blocker and grants no scope to add the species.
