using Game.Application;
using Game.Domain.Events;
using Game.Domain.Battle;
using Game.Domain.FirstBattle;
using Godot;

public partial class CampaignView
{
    // Each battle owner retains its own presentation identity. These surfaces own
    // only visual pages, never semantic page indices, clocks or gameplay ACKs.
    private sealed class BattleDialogSurfaceState
    {
        public SourceDialogSurface Surface { get; } = new();
        public object? Identity { get; set; }
        public string? Source { get; set; }
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
    private bool NativeWildMessageEnabled => _wildMenu is not ("party" or "medicine-target" or "medicine-move") &&
        !ForestTrainerShiftPending && PresentedWildBattle is { Capture: null, Replacement: null };
    private bool NativeDoubleMessageEnabled => _doubleMenu is not ("party" or "medicine-target" or "medicine-move" or "bag" or "bag-actions") &&
        _session.State.DoubleBattle?.CurrentEvent is not SourceMedicineAppliedEvent;

    private SourceMessageSample NativeBattleSample(BattleDialogSurfaceState state, object identity,
        string source, SourceGameFont font, int width, int lineStep)
    {
        var binding = (identity, font, width, lineStep);
        var text = state.Surface.Text;
        if (!Equals(state.Identity, binding) || state.Source != source)
        {
            text.Visible = true;
            string prepared = font.PrepareText(source);
            text.Bind(prepared, font.Font, font.Size, lineStep, width,
                wrap: SourceHelpTextLayout.ContainsCjk(prepared));
            text.RevealAll();
            state.Identity = binding;
            state.Source = source;
        }
        return new(text.CurrentVisualText, text.VisualPage, text.PageCount);
    }

    private static bool AdvanceNativeBattleVisual(BattleDialogSurfaceState state)
    {
        if (!state.Surface.Text.TryAdvanceVisual(true)) return false;
        state.Surface.Text.RevealAll();
        return true;
    }

    private void DrawNativeBattleText(BattleDialogSurfaceState state, Vector2 origin,
        SourceGameFont font, bool cursor, double elapsed, bool blue = true, bool drawText = true)
    {
        if (drawText) state.Surface.Draw(this, origin, font);
        if (cursor)
            _textCursor.Draw(this, (origin + state.Surface.Text.ContinuationPosition).Floor(),
                (long)Math.Max(0, elapsed * 60), blue);
    }
}
