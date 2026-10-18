# Shared Easy Chat: Mail and local profile service

The woman in Pewter City's Pokémon Center is an ordinary local profile editor. The source script's `mystery_event_club` filename does not make this a network or Mystery Gift feature. Its existing actor and script were reachable, but the YES/NO/INFO menu and the two Easy Chat native operations were not implemented.

## Authority and ownership

The canonical story menu importer now includes `MULTICHOICE_YES_NO_INFO_2`. The existing Easy Chat catalog imports the profile and Mail templates, source text symbols, the four initial profile words and the special phrase. The shared editor rule owns only the transient word buffer and Done/Cancel/Delete confirmations. Mail still owns its stationery/party/item transaction; the profile adapter authenticates the suspended `ShowEasyChatScreen` instruction, stack, revision and native wait before resuming it. Input and saves cannot bypass an active editor.

One Godot presenter consumes the template's dimensions and text, with the same word/group/A–Z selection and keyboard/mouse handling for both contexts. Source-derived settled backgrounds use the existing Easy Chat assets. There is no second word catalog, profile-only field screen, or network unlock. The normal Trainer Card does not acquire link-only profile text.

## Source behavior retained

Pinned source `037335f4` supplies `data/scripts/mystery_event_club.inc`, `src/easy_chat.c` and `src/easy_chat_2.c`:

- New-game words are I AM / A / POKéMON / FRIEND.
- An unchanged confirmed draft returns zero; an all-empty Done asks to cancel.
- Every normal exit, including confirmed cancellation, sets the profile flag. The special-phrase comparison reads the transient draft even on cancellation, while the displayed profile reads saved words. This source quirk is intentional.
- The source's four-word message keeps its spacing and two rows, remains visible through its 80-frame delay, then yields to the next script message.
- The local conversation flag prevents another edit until map re-entry. Persistent profile state controls the returning greeting.
- Entry and return retain the script's 16-frame fades. Editing owns the native wait while the underlying field is black.

Old saves lacking profile words can use the known initial words only when the profile-set flag is absent. A missing profile with that flag set is ambiguous and rejects. Explicit invalid, unknown or all-empty saved words reject. The first cancellation persists the known original words before setting the flag. No rules-version change, guessed custom phrase or extra history is needed.

## Evidence and limits

Focused import checks independently reconstruct all six Mail/profile settled surfaces. All 47 old Mail RGBA assets and the 1,813-word catalog remain unchanged. The new title palette is from the same pinned source. Generated images/contracts remain local and ignored.

Managed checks cover the actual mounted actor/script via controlled valid field placement and real entry callbacks, source menu branches, editing and confirmations, stale inputs, active-save rejection, special-phrase cancellation, delayed message ownership, re-entry and save round-trip. These are not a complete physical Kanto traversal. The shared cursor tests cover the two-by-two profile and Mail's partial final row.

The coherent solution build passed with zero warnings/errors (26.08 seconds). Focused Application checks passed 163/163 (34 seconds), covering profile, Mail, composition and save boundaries; Domain passed 5/5, Content 8/8, and cursor Presentation 2/2. The 47 focused Python checks and both importer parity commands passed. Story regeneration compiled 4,237 programs and 4,284 text labels.

Failed checks are retained: the first build found a missing namespace import after relocating the existing Mail icon loader; the first Application run passed 22/33 and exposed preflight trying generic acknowledgement through the new editor. The corrected preflight recognizes only the authenticated native transaction and has an explicit forged-callback regression. No gameplay or expected-data fallback was added.

Window-level interaction and actual speaker output remain unverified in the cloud environment without a graphical display.

The final Godot-only build passed with zero warnings/errors (22.82 seconds). Official Godot 4.7.2/.NET 10.0.12 headless startup completed 180 frames and exited zero without runtime errors (MVID `7f3bafdb-fcbd-41a2-acb0-97cd937e28e6`). This verifies composition/startup, not the unseen editor's window pixels or real audio.
