# Substitute move-controller presentation

The source move controller reveals an attacker's body before its move script, then normally restores its Substitute doll. This is separate from the move that creates a Substitute and from general result animations. The shared controller holds one original receipt and one acknowledgement across the reveal, move and restore stages; none of these stages executes a gameplay command.

## Source contract

Pinned `battle_controller_player.c:2331–2375` and the corresponding opponent controller set transient `flag_x8` before `Special_SubstituteToMon`. Once that special ends, the actual move runs with its original actor and target. When the move script ends, `Special_MonToSubstitute` starts only when the controller's remaining multi-hit counter is below two. The flag therefore survives intermediate hit pages and even an early target KO. It belongs to the current occupant and is discarded on replacement, loss of the doll, cancellation or battle exit.

The counter is zero on normal action entry (`battle_main.c:3978`), including called leaves and Beat Up. Authenticated multi-hit summaries supply planned hits minus the current zero-based ordinal, rather than eventual actual hits. Pursuit during a switch uses the switch script's counter: two for the right doubles interceptor and one for the left interceptor; normal Pursuit remains zero.

`battle_anim_special.c:1936–1990` moves x2 by five each callback until unsigned `(rawX + x2 + 32) > 304`. A separate callback loads the replacement and resets raw Y to its default while retaining raw X, y2 and invisibility. The return phase clamps x2 to zero. The shared handoff must preserve a preceding move's terminal pose, including Teleport's hidden offscreen raw X and y2.

`Cmd_attackanimation` (`battle_script_commands.c:1667`) exempts Transform and Substitute from Battle Scene Off. Their material transitions still run; other moves retain the option's ordinary behavior. A successful Transform behind a Baton-Passed doll uses authenticated pre/post bodies and restores the doll afterward. A transformed actor using Substitute selects a doll explicitly, rather than mistaking the existing Transform state for the requested material change.

## Implementation boundary

The scene remains the sole sprite allocator and per-frame sprite/OAM owner. Selected runtimes validate before the first visible stage; only the current stage attaches. The wrapper uses the existing named-program catalog and material replacement seam, not fabricated move IDs or per-move draw branches. Original action target, visual RNG and residual sprite ownership are retained. Transient reveal ownership is not serialized.

This sequence begins at `PlayerDoMoveAnimation` case 0, after the move command is accepted; the earlier command dispatch and sound-busy wait remain outside its scope. The controller launch frame is distinct from the first move-script callback. A reveal table runs in its source task phase; once it finishes, the next controller callback launches the actual move, whose script begins on the following callback. Already-revealed multi-hit invocations retain both the initial controller-only pass and the launch-only pass. The final restore begins before that frame's sprite/OAM/task pass, and acknowledgement remains a separate next-frame controller action. Background/palette engines and existing uncounted task instances continue across handoff; source launch resets still apply. A background fade task captures its immutable palette input when created rather than consulting a later phase's catalog.

## Verification

The extracted original callback reproduces twenty explicit cases and 851 rows, including both directions, all singles/doubles positions, threshold equality, nonzero offsets, hidden sprites and the post-Teleport pose. Numeric output is generated into the ignored local fixture directory. The importer preserves all ten prior named programs and material metadata and adds the two Special programs; targeted importer checks passed 11/11 plus 7/7 named-program checks.

Application receipt/lifetime checks passed 142/142 in six seconds, including natural Baton Pass into Ditto, planned versus actual hit counts, early KO, switch interception, called leaves and Scene Off material changes. An initial affected Presentation run passed 95/101: one synthetic Teleport expectation omitted its affine foot offset, and five tests reported four missing ignored source-vector files after workspace recovery. The maintained generators restored those local files; the corrected run passed 101/101 in twelve seconds. A subsequent source review added a direct controller launch-only cadence and visible-priority reset regression. The final solution build passed with zero warnings/errors in 35.79 seconds, and affected Presentation passed 102/102 in twelve seconds, including fifteen sequence tests and five source-swap tests. Actual resolved Tackle, Teleport and Transform roots are composed through the wrapper for all canonical positions; background/palette continuation and foreign residual ownership are checked separately.

Headless startup passed on Godot 4.7.2/.NET 10.0.12. This checks loading and composition startup only. No new graphical interaction or real-speaker observation is claimed. Generated numeric data remains ignored and reproducible, not tracked in Git.
