# Field and Oak text input

Field dialogue and Oak's opening speech share `SourceTextProgress` and the
one-shot `SourceTextInput` adapter. The host samples combined logical A/B held
state each printer frame, including L=A, multiple devices and pointer owners.
Releasing one owner does not release the others. A pending new press survives a
subframe update and is consumed once, not repeated across a multi-frame batch.

The pinned source (`src/text.c:RenderText`, `src/text_printer.c:AddTextPrinter`)
activates its speed-up latch only when a fresh A/B press reaches a character-delay
frame. Holding after activation prints one ordinary text element per frame;
releasing restores the selected SLOW/MID/FAST cadence (8/4/1 frames). The latch
remains set, but is not itself a held button. A key already held when a new
message starts, or pressed on its first ordinary glyph frame, does not silently
activate it. `SourceTextProgress` retains that behavior unchanged.

Visual continuations and consecutive pages of the same source message retain
the printer latch and delay. Their acknowledgement edge is consumed by the page
change, while physical held state remains available. New messages reset the
printer. Field continuation identity requires the same source symbol and page
array plus the next page index; Oak retains the original message identity across
its prepared pages. Holding alone never supplies a new page acknowledgement.

In Oak speech, both A and B speed printing or advance completed text. B no longer
navigates backward through speech; the earlier controls/Pikachu guide pages keep
their separate backward navigation. Releasing a tap no longer leaves Oak in
permanent accelerated printing. Right-click uses the same guarded B route.
The field printer's egg-hatch consumer retains its existing keyboard aliases
and pointer controls.

## Deliberate remaining boundaries

This input correction does not change final-character/EOS readiness, script
acknowledgement scheduling, source clear/scroll animation timing, or Oak's
existing manual versus automatic EOS choreography. Printer completion is not a
promise about the exact host input-dispatch frame: input callbacks and `_Process`
have separate ordering, and scene/cry/animation owners may still block progress.
It is not a claim of original frame-for-frame message completion. Specialized
menus/notices retain their own owners; battle scope is documented separately in
[battle text progress](battle-text-progress.md).

## Verification

`SourceTextInputTests` exercises taps and releases, subframe/batched advancement,
first-frame activation boundaries, A/B and L=A mixed owners, continuation versus
new-message latch state, and clearing pending input. Narrow source-wiring guards
check that the actual field/Oak consumers use the shared sampler and that Oak's
right-click route does not pre-consume its same-frame guard. These guards are not
an executed native keyboard/mouse walkthrough.

This patch was reconstructed after workspace recovery. Earlier reported test
results do not verify the reconstructed tree; rebuild and rerun before acceptance.
No new native-input or audio-output acceptance is claimed. Input-focused command:

```sh
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SourceTextInputTests|FullyQualifiedName~TextProgressTests|FullyQualifiedName~SourceDialogPageCursorTests|FullyQualifiedName~ButtonModeHelpTests'
```
