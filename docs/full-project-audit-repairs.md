# Full-project audit: first repair batch

Scope: reachable single-player behavior, shared runtime boundaries and repeated work. The first repair batch left the 354 move identities and ten named general animation programs unchanged. The subsequent shared Substitute controller repair adds two Special programs, described separately below. Static audit, managed checks and native acceptance are distinct evidence; this document does not claim an exhaustive campaign or real-speaker pass.

## Findings and changes

- **Teleport result text:** the producer previously emitted an undifferentiated escaped terminal result, and the view skipped every terminal result. Optional typed move-escape evidence on that same event now preserves the original actor and the fled-message page until acknowledgement. Ordinary Run retains its existing terminal behavior. No added event, gameplay or RNG change. See [move escape presentation](move-escape-presentation.md).
- **Runtime import provenance:** four icon-only consumers depended on historical item counts and predecessor import contracts. They now consume the existing icon schema directly. Import tooling validates fixed source inputs; all 2,304 pixels and atlas rectangles are preserved. Tanoby completion retains its ordered switch evidence and gameplay validation without a source-file hash. Legacy save envelopes still authenticate their original bytes before decoding; retired metadata is naturally ignored and omitted on the next save, without a rules-version change.
- **Static shop ownership:** six previously separate Celadon/Indigo shelves move into the existing canonical item/shop recipe, actor/instruction binding and transaction path. Machine purchases retain the shared TM CASE insertion and capacity rules. Story-dependent Two Island selection remains explicit.
- **Movement projection:** ordinary movement repeatedly copied the 395-map catalog (414 resource rows include alternate layouts) and rebuilt offscreen regional cells. Command-local map views now compose only requested maps and memoize within that view, retaining adapter order and full enumeration semantics. Four immutable Trainer Tower course projections bind once with the catalog; dynamic occupancy remains current/neighbor-specific.
- **Animation resource resolution:** canonical move/general requests now reuse lazily resolved dependency/material objects for the lifetime of the scene catalog. Custom script resolution still revalidates references and dependencies. Mutable animation palette state remains owned by each invocation; cached source palette data is not a writable playback bank.

## Remaining concrete follow-ups

- The shared Substitute controller repair is described in [Substitute move controller](substitute-move-controller.md). It addresses body reveal/restoration and the reachable Baton Pass into Ditto Transform path through a single scene-owned sequence; its own validation record is separate from this first batch.
- Trainer Tower settlement history grows without bound and is repeatedly validated. A bounded current/last receipt plus explicit serial requires a carefully tested save representation transition; it is not truncated by this batch.
- Quest transition/weather details and actual speaker acceptance remain separate recorded boundaries. Missing native observations do not establish a missing game feature.

## Verification

Initial coherent solution build passed with zero warnings/errors (91.58 seconds). Content passed 483/483 (33 seconds); affected Presentation passed 22/22 (49 seconds), including canonical cache reuse/custom validation and palette ownership. Application passed 3,577/3,582 (3 minutes 5 seconds); the five failures identified one shared escape-producer alias needing separation and two controlled-fixture assumptions (silent party synchronization/history retention and earned HM ownership). The corrected solution again passed with zero warnings/errors (23.35 seconds). Focused Application refresh passed 71/71 (18 seconds), Content 23/23 (3 seconds), and text Presentation 5/5 (305 milliseconds). The intervening fourth-floor icon migration build exposed one wrong texture overload, corrected to the existing shared ItemIcon overload before these final gates.

Baseline Application: 3,555 passed; two failures explicitly reported missing locally generated jump/Quest-exit reference data after environment recovery. Both maintained generators restored those inputs. A subsequent old-DLL refresh overlapped publication of the intentionally retired resource contract and therefore could not load the old catalog; it is retained as a failed check and will be replaced by a coherent rebuilt test run.


Four icon importers passed source parity; source-caller checks passed 6/6, current-item import checks 10/10, and script layout 28/28. No generated resource or source-vector output is tracked.

The external before/after probe used matching original/current product assemblies and separate coherent resource roots. It measured only warmed `OccupiedFieldMaps(snapshot)[PalletTown]`, seven batches of 100 calls, with tiered compilation disabled on .NET 10.0.12. Median allocation fell from 660,280 to 10,736 bytes per query (98.4%); median elapsed time was 1,395.996 versus 3.714 microseconds. Returned map cells and connections compared exactly. This excludes cold catalog loading, rendering and command/save work, and is not a whole-game frame-rate claim. Probe output remains external rather than becoming generated Git evidence.

Headless startup exited zero using official Godot 4.7.2 and .NET 10.0.12, with no runtime errors. The cloud workspace has no graphical display server, so movement/shop/Teleport visual interaction was not newly observed. Dummy audio proves no actual-speaker acceptance.
