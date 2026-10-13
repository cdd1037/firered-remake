using Game.Application;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Functional tutor menu using authenticated source text and existing fonts/frames.
/// All callback state, target identity, learning and cancellation belong to Application.</summary>
public partial class CampaignView
{
    private int _tutorCursor;
    private ulong _tutorTransitionFrame = ulong.MaxValue;
    private bool MoveTutorActive => _session?.State.MoveTutor is not null;
    private static Rect2 TutorPartyRow(int index) => new(8, 6 + index * 18, 224, 18);
    private static Rect2 TutorMoveRow(int index) => new(8, 12 + index * 18, 224, 18);
    private static Rect2 TutorCancelRow => new(8, 116, 224, 18);
    private static Rect2 TutorChoiceRow(int index) => new(182, 73 + index * 16, 50, 16);
    private static Rect2 TutorMessageRect => new(0, 112, 240, 48);

    private void SynchronizeMoveTutorPresentation(CampaignState? before, CampaignState after)
    {
        var old = before?.MoveTutor; var next = after.MoveTutor;
        if (old?.Stage != next?.Stage || old?.Page != next?.Page)
        {
            _held = null; _repeat = 0; ReleaseFieldTextInput(); _tutorCursor = 0;
            _tutorTransitionFrame = Engine.GetProcessFrames();
        }
        if ((old is null) != (next is null)) _fade = 16.0 / 60;
    }

    private bool ConsumeMoveTutorTransitionInput(InputEvent input)
    {
        if (_tutorTransitionFrame != Engine.GetProcessFrames() || input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 }) return false;
        GetViewport().SetInputAsHandled(); return true;
    }

    private bool TutorHasChoice => _session.State.MoveTutor is { Stage: CampaignMoveTutorStage.ReplaceQuestion or CampaignMoveTutorStage.StopQuestion } s &&
        s.Page + 1 == s.Pages.Length;
    private int TutorRowCount => _session.State.MoveTutor!.Stage == CampaignMoveTutorStage.Party ? _session.State.Party.Length + 1 :
        _session.State.MoveTutor.Stage == CampaignMoveTutorStage.ChooseMove ? 5 : TutorHasChoice ? 2 : 0;

    private bool MoveTutorInput(InputEventKey key)
    {
        if (!MoveTutorActive || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        var scene = _session.State.MoveTutor!;
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        int count = TutorRowCount;
        if (count > 0)
        {
            if (key.Keycode is Key.Up or Key.Left) _tutorCursor = (_tutorCursor + count - 1) % count;
            else if (key.Keycode is Key.Down or Key.Right) _tutorCursor = (_tutorCursor + 1) % count;
        }
        if (!confirm && !cancel) return true;
        if (scene.Stage == CampaignMoveTutorStage.Party)
        {
            if (cancel || _tutorCursor == _session.State.Party.Length) Apply(new("move_tutor_cancel"));
            else
            {
                var mon = _session.State.Party[_tutorCursor];
                if (mon.IsEgg) { SourceSound("SE_FAILURE"); return true; }
                Apply(new("move_tutor_choose", PartyIndex: _tutorCursor) { TargetInstanceId = mon.InstanceId });
            }
        }
        else if (scene.Stage == CampaignMoveTutorStage.ChooseMove)
        {
            if (cancel || _tutorCursor == 4) Apply(new("move_tutor_cancel"));
            else Apply(new("move_tutor_replace", MoveSlot: _tutorCursor) { TargetInstanceId = scene.InstanceId });
        }
        else if (TutorHasChoice) Apply(new("move_tutor_choice", Choice: cancel ? 0 : _tutorCursor == 0 ? 1 : 0));
        else if (scene.Stage != CampaignMoveTutorStage.Learned || _fanfareRemaining <= 0) Apply(new("move_tutor_ack"));
        return true;
    }

    private Rect2 TutorRow(int index) => _session.State.MoveTutor!.Stage == CampaignMoveTutorStage.Party
        ? index == _session.State.Party.Length ? TutorCancelRow : TutorPartyRow(index)
        : _session.State.MoveTutor.Stage == CampaignMoveTutorStage.ChooseMove ? TutorMoveRow(index) : TutorChoiceRow(index);
    private int TutorPointerRow(Vector2 point)
    { for (int i = 0; i < TutorRowCount; i++) if (TutorRow(i).HasPoint(point)) return i; return -1; }
    private bool NavigateMoveTutorPointer(Vector2 point, int direction)
    {
        if (!MoveTutorActive) return false;
        int row = TutorPointerRow(point); if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_tutorCursor == row) return false;
        _tutorCursor = row; return true;
    }
    private bool MouseMoveTutor(Vector2 point)
    {
        int row = TutorPointerRow(point);
        if (row >= 0) { _tutorCursor = row; MouseKey(Key.Enter); return true; }
        if (TutorRowCount == 0 && TutorMessageRect.HasPoint(point)) { MouseKey(Key.Enter); return true; }
        return false;
    }

    private bool DrawMoveTutor()
    {
        if (!MoveTutorActive) return false;
        var state = _session.State; var scene = state.MoveTutor!; var catalog = _catalog.MoveTutors!;
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        if (scene.Stage == CampaignMoveTutorStage.Party)
        {
            var definition = catalog.Definitions[scene.TutorIndex];
            for (int i = 0; i < state.Party.Length; i++)
            {
                var mon = state.Party[i]; var row = TutorPartyRow(i); float y = row.Position.Y;
                if (_tutorCursor == i) DrawRect(row, new Color("e4e4c8"));
                DrawTextureRectRegion(PresentedPokemonIcon(mon.Pokemon), new(18, y, 16, 16), new(0, 0, 32, 32));
                SourceText(_sourceSmall, new(36, y), mon.Nickname);
                SourceText(_sourceSmall, new(113, y), "Lv" + mon.Pokemon.Level);
                string label = MoveTutorRules.Eligibility(definition, mon) switch
                { MoveTutorEligibility.Able => "gText_Able2", MoveTutorEligibility.AlreadyKnown => "gText_Learned", _ => "gText_NotAble" };
                SourceText(_sourceSmall, new(157, y), catalog.Texts[label]);
            }
            SourceText(_sourceNormal, new(20, 116), "CANCEL");
            SourceText(_sourceNormal, new(8, TutorRow(_tutorCursor).Position.Y), "▶");
            SourceText(_sourceSmall, new(8, 140), catalog.Texts["gText_TeachWhichPokemon"]);
        }
        else
        {
            var mon = state.Party[scene.PartyIndex];
            SourceText(_sourceNormal, new(12, 6), mon.Nickname);
            if (scene.Stage == CampaignMoveTutorStage.ChooseMove)
            {
                _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 110));
                for (int i = 0; i < 5; i++)
                {
                    var row = TutorMoveRow(i);
                    SourceText(_sourceNormal, new(24, row.Position.Y), i == 4 ? "CANCEL" : MoveName(mon.Pokemon.Moves[i].Move));
                    if (i < 4) SourceText(_sourceSmall, new(178, row.Position.Y), $"{mon.Pokemon.Moves[i].CurrentPp}/{mon.Pokemon.Moves[i].MaximumPp}");
                    if (i == _tutorCursor) SourceText(_sourceNormal, new(10, row.Position.Y), "▶");
                }
            }
            else DrawTextureRectRegion(PresentedPokemonIcon(mon.Pokemon), new(108, 36, 32, 32), new(0, 0, 32, 32));
            _battleSkin.DrawStandardFrame(this, TutorMessageRect);
            var lines = scene.Pages[scene.Page].Split('\n');
            for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 118 + i * 16), lines[i]);
            if (TutorHasChoice)
            {
                _battleSkin.DrawStandardFrame(this, new(176, 68, 64, 44));
                SourceText(_sourceNormal, new(198, 73), "YES"); SourceText(_sourceNormal, new(198, 89), "NO");
                SourceText(_sourceNormal, new(184, TutorChoiceRow(_tutorCursor).Position.Y), "▶");
            }
        }
        if (_fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_fade / (16.0 / 60), 0, 1)));
        return true;
    }
}
