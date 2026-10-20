using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Game.Domain.FirstBattle;
using Godot;

public partial class CampaignView
{
    // Each battle owner retains its own presentation identity. These surfaces own
    // only visual pages, never semantic page indices, clocks or gameplay ACKs.
    private sealed class BattleDialogSurfaceState
    {
        public SourceDialogSurface Surface { get; } = new();
        public SourceBattleTextPrinter Printer { get; } = new();
        public object? Identity { get; set; }
        public BattleMessageIdentity? Message { get; set; }
        public string? Source { get; set; }
    }
    private sealed record BattleMessageIdentity(string? Key, ImmutableArray<string> Pages, int Page);
    private readonly HashSet<int> _battleSourceContinuations = [], _wildSourceContinuations = [], _doubleSourceContinuations = [];

    // Record boundaries while expanding each source string, before flattening.
    // Index zero of each call starts a new printer; subsequent pages continue it.
    private static void AppendBattleSourcePages(ImmutableArray<string>.Builder target,
        IEnumerable<string> sourcePages, HashSet<int> continuations)
    {
        bool first = true;
        foreach (string page in sourcePages)
        {
            if (!first) continuations.Add(target.Count);
            target.Add(page);
            first = false;
        }
    }

    private static ImmutableArray<string> JoinBattleSourcePages(HashSet<int> continuations,
        params ImmutableArray<string>[] messages)
    {
        continuations.Clear();
        var pages = ImmutableArray.CreateBuilder<string>();
        foreach (var message in messages) AppendBattleSourcePages(pages, message, continuations);
        return pages.ToImmutable();
    }

    private readonly BattleDialogSurfaceState _nativeBattleMessage = new(),
        _nativeWildMessage = new(), _nativeDoubleMessage = new();

    private void InitializeBattleDialogText()
    {
        AddChild(_nativeBattleMessage.Surface);
        AddChild(_nativeWildMessage.Surface);
        AddChild(_nativeDoubleMessage.Surface);
    }

    // Party/medicine, capture and trainer-choice consumers retain the existing
    // renderer until their distinct layout and input paths migrate together.
    private bool NativeBattleMessageEnabled => _battleMenu is not ("party" or "medicine-target") &&
        !(_session.State.Battle is { Phase: CampaignBattlePhase.Events } battle &&
          battle.Events[battle.EventIndex] is FirstBattlePotionRecoveredEvent or
              FirstBattleTutorialEvent { Flag: FirstBattleTutorialFlags.PartyMenu });
    private bool NativeWildMessageEnabled => TeachyBattle is null && _wildMenu is not ("party" or "medicine-target" or "medicine-move") &&
        !ForestTrainerShiftPending && PresentedWildBattle is { Capture: null, Replacement: null };
    private bool NativeDoubleMessageEnabled => _doubleMenu is not ("party" or "medicine-target" or "medicine-move" or "bag" or "bag-actions") &&
        _session.State.DoubleBattle?.CurrentEvent is not SourceMedicineAppliedEvent;

    private SourceMessageSample NativeBattleSample(BattleDialogSurfaceState state, BattleMessageIdentity identity,
        string source, SourceGameFont font, int width, int lineStep, HashSet<int> continuations)
    {
        var binding = (identity, font, width, lineStep);
        var text = state.Surface.Text;
        if (!Equals(state.Identity, binding) || state.Source != source)
        {
            text.Visible = true;
            string prepared = font.PrepareText(source);
            text.Bind(prepared, font.Font, font.Size, lineStep, width,
                wrap: SourceHelpTextLayout.ContainsCjk(prepared));
            bool continuation = continuations.Contains(identity.Page) && state.Printer.Complete &&
                state.Message is {} previous && previous.Key == identity.Key &&
                previous.Pages == identity.Pages && previous.Page + 1 == identity.Page;
            if (continuation) state.Printer.ContinuePage(text.CurrentVisualText);
            else state.Printer.Reset(text.CurrentVisualText, _session.EffectiveOptions.TextSpeed switch
            { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 });
            state.Message = identity;
            state.Identity = binding;
            state.Source = source;
        }
        return new(text.CurrentVisualText, text.VisualPage, text.PageCount);
    }

    private static bool AdvanceNativeBattleVisual(BattleDialogSurfaceState state)
    {
        // Returning true while printing consumes the request without changing either
        // the visual cursor or the owning semantic event/page.
        if (!state.Printer.Complete) return true;
        if (!state.Surface.Text.TryAdvanceVisual(state.Printer.Complete)) return false;
        state.Printer.ContinuePage(state.Surface.Text.CurrentVisualText);
        return true;
    }

    // Select only the native message actually owned by the visible battle. Legacy
    // notices, stats panels, capture/party/medicine and choice screens are not
    // progressive consumers yet. A retained page array alone is not visibility.
    private BattleDialogSurfaceState? ActiveBattleText()
    {
        if (_session.State.Evolution is not null || _session.State.RareCandy is not null ||
            _session.State.OldManTutorial is not null || _session.State.SafariBattle is not null ||
            _sourceResultContinuation is not null && !_sourceResultCompleted)
            return null;
        if (DoubleBattleVisible)
        {
            if (!NativeDoubleMessageEnabled || _doubleNotice is not null || _doublePages.IsEmpty ||
                _doubleMenu is "learn-moves" or "learn-choice" || _doubleRewardStats.ContainsKey(_doublePage)) return null;
            _ = DoubleMessageSample;
            return _nativeDoubleMessage;
        }
        if (ForestTrainerCombatVisible || _session.State.WildBattle is not null)
        {
            var battle = PresentedWildBattle!;
            if (!NativeWildMessageEnabled || _wildNotice is not null || _wildPages.IsEmpty ||
                WildStatsPage(battle) || _wildMenu.StartsWith("learn-", StringComparison.Ordinal) && _wildMenu != "learn-intro" ||
                WildCaptureBallBlocking(battle) || WildCurrentEvent(battle) is WildBattleTowerHpEvent { Effect: "curse-cost" } && _wildElapsed < .4)
                return null;
            _ = WildMessageSample;
            return _nativeWildMessage;
        }
        if (_session.State.Battle is {} tutorial && NativeBattleMessageEnabled &&
            _battleNotice is null && !_battlePages.IsEmpty &&
            !(tutorial.Phase == CampaignBattlePhase.Results && tutorial.Pages[tutorial.PageIndex].Kind == "stats"))
        {
            _ = BattleMessageSample;
            return _nativeBattleMessage;
        }
        return null;
    }

    private void ProcessBattleText(double delta)
    {
        var state = ActiveBattleText();
        if (state is null) return;
        state.Printer.Advance(delta, TextSpeedButtonHeld);
        state.Surface.Text.RevealCodepoints(state.Printer.VisibleCodepoints);
    }

    private bool BattleTextInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo || key.Keycode is not (Key.Enter or Key.Backspace)) return false;
        var state = ActiveBattleText();
        if (state is null || !state.Printer.Press()) return false;
        _held = null;
        return true;
    }

    private void ReleaseBattleTextInput()
    {
        _nativeBattleMessage.Printer.ReleaseInput();
        _nativeWildMessage.Printer.ReleaseInput();
        _nativeDoubleMessage.Printer.ReleaseInput();
    }

    private void DrawNativeBattleText(BattleDialogSurfaceState state, Vector2 origin,
        SourceGameFont font, bool cursor, double elapsed, bool blue = true, bool drawText = true)
    {
        if (drawText) state.Surface.Draw(this, origin, font);
        if (cursor && state.Printer.Complete)
            _textCursor.Draw(this, (origin + state.Surface.Text.ContinuationPosition).Floor(),
                (long)Math.Max(0, elapsed * 60), blue);
    }
}
