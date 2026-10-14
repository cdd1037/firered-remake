using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

/// <summary>Shared functional party/teaching surface. The source callbacks own all rules;
/// these existing fonts and frames also present Reminder and Deleter selection.</summary>
public partial class CampaignView
{
    private int _tutorCursor;
    private int _tutorScroll;
    private ulong _tutorTransitionFrame = ulong.MaxValue;
    private sealed record TeachingPresentation(string Prefix, string Stage, int PartyIndex, long? InstanceId,
        bool Party, bool Replace, bool Moves, bool Payment, bool Choice, bool Learned,
        ImmutableArray<string> Pages, int Page)
    { public bool Delete => Prefix == "move_deleter_"; }
    private TeachingPresentation? Teaching
    {
        get
        {
            if (_session?.State.MoveTutor is { } tutor)
                return new("move_tutor_", tutor.Stage.ToString(), tutor.PartyIndex, tutor.InstanceId,
                    tutor.Stage == CampaignMoveTutorStage.Party, tutor.Stage == CampaignMoveTutorStage.ChooseMove, false, false,
                    tutor.Stage is CampaignMoveTutorStage.ReplaceQuestion or CampaignMoveTutorStage.StopQuestion && tutor.Page + 1 == tutor.Pages.Length,
                    tutor.Stage == CampaignMoveTutorStage.Learned, tutor.Pages, tutor.Page);
            if (_session?.State.MoveReminder is { Stage: not CampaignMoveReminderStage.FieldScript } reminder)
                return new("move_reminder_", reminder.Stage.ToString(), reminder.PartyIndex, reminder.InstanceId,
                    reminder.Stage == CampaignMoveReminderStage.Party, reminder.Stage == CampaignMoveReminderStage.ChooseMove,
                    reminder.Stage == CampaignMoveReminderStage.Moves, reminder.Stage == CampaignMoveReminderStage.Payment,
                    reminder.Stage is CampaignMoveReminderStage.TeachQuestion or CampaignMoveReminderStage.GiveUpQuestion or
                        CampaignMoveReminderStage.ReplaceQuestion or CampaignMoveReminderStage.StopQuestion && reminder.Page + 1 == reminder.Pages.Length,
                    reminder.Stage == CampaignMoveReminderStage.Learned,
                    reminder.Stage == CampaignMoveReminderStage.Payment ? _session.State.Dialogue?.Pages ?? [] : reminder.Pages,
                    reminder.Stage == CampaignMoveReminderStage.Payment ? _session.State.Dialogue?.Page ?? 0 : reminder.Page);
            if (_session?.State.MoveDeleter is { Stage: not CampaignMoveDeleterStage.FieldScript } deleter)
                return new("move_deleter_", deleter.Stage.ToString(), deleter.PartyIndex, deleter.InstanceId,
                    deleter.Stage == CampaignMoveDeleterStage.Party, deleter.Stage == CampaignMoveDeleterStage.Moves,
                    false, false, false, false,
                    [DeleterCatalog.Texts["gText_PokeSum_PageName_KnownMoves"] + "\n" +
                        DeleterCatalog.Texts["gText_PokeSum_Controls_PickDelete"].Replace("{DPAD_UPDOWN}", "↑↓ ", StringComparison.Ordinal)
                            .Replace("{A_BUTTON}", "A ", StringComparison.Ordinal)], 0);
            return null;
        }
    }
    private bool MoveTutorActive => Teaching is not null || DayCareChoosing;
    private MoveReminderCatalog ReminderCatalog => ((ICampaignCatalog)_catalog).MoveReminder!;
    private MoveDeleterCatalog DeleterCatalog => ((ICampaignCatalog)_catalog).MoveDeleter!;
    private static Rect2 TutorPartyRow(int index) => new(8, 6 + index * 18, 224, 18);
    private static Rect2 TutorMoveRow(int index) => new(8, 12 + index * 18, 224, 18);
    private static Rect2 TutorCancelRow => new(8, 116, 224, 18);
    private static Rect2 TutorChoiceRow(int index) => new(182, 73 + index * 16, 50, 16);
    private static Rect2 TutorPaymentRow(int index) => new(8, 32 + index * 24, 224, 22);
    private static Rect2 TutorMessageRect => new(0, 112, 240, 48);

    private void SynchronizeMoveTutorPresentation(CampaignState? before, CampaignState after)
    {
        if (before?.MoveTutor?.Stage != after.MoveTutor?.Stage || before?.MoveTutor?.Page != after.MoveTutor?.Page ||
            before?.MoveReminder?.Stage != after.MoveReminder?.Stage || before?.MoveReminder?.Page != after.MoveReminder?.Page ||
            before?.MoveDeleter?.Stage != after.MoveDeleter?.Stage || before?.DayCareInteraction?.Stage != after.DayCareInteraction?.Stage || IsDayCareChoosing(before) != IsDayCareChoosing(after))
        {
            _held = null; _repeat = 0; ReleaseFieldTextInput(); _tutorCursor = 0; _tutorScroll = 0;
            ResetDayCarePartyPresentation();
            _tutorTransitionFrame = Engine.GetProcessFrames();
        }
        bool oldActive = before?.MoveTutor is not null || before?.MoveReminder is { Stage: not CampaignMoveReminderStage.FieldScript } ||
            before?.MoveDeleter is { Stage: not CampaignMoveDeleterStage.FieldScript } || IsDayCareChoosing(before);
        if (oldActive != MoveTutorActive) _fade = 16.0 / 60;
    }

    private bool ConsumeMoveTutorTransitionInput(InputEvent input)
    {
        if (_tutorTransitionFrame != Engine.GetProcessFrames() || input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 }) return false;
        GetViewport().SetInputAsHandled(); return true;
    }

    private int TutorRowCount => Teaching is not { } scene ? 0 : scene.Party ? _session.State.Party.Length + 1 :
        scene.Replace ? _session.State.Party[scene.PartyIndex].Pokemon.Moves.Length + 1 : scene.Moves ? _session.State.MoveReminder!.Moves.Length + 1 : scene.Choice || scene.Payment ? 2 : 0;
    private void TutorKeepCursorVisible()
    {
        if (Teaching?.Moves != true) return;
        if (_tutorCursor < _tutorScroll) _tutorScroll = _tutorCursor;
        if (_tutorCursor >= _tutorScroll + 6) _tutorScroll = _tutorCursor - 5;
    }

    private bool MoveTutorInput(InputEventKey key)
    {
        if (!MoveTutorActive || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        if (DayCareChoosing) return DayCarePartyInput(key);
        var scene = Teaching!;
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        int count = TutorRowCount;
        if (count > 0)
        {
            if (key.Keycode is Key.Up or Key.Left) _tutorCursor = (_tutorCursor + count - 1) % count;
            else if (key.Keycode is Key.Down or Key.Right) _tutorCursor = (_tutorCursor + 1) % count;
            TutorKeepCursorVisible();
        }
        if (!confirm && !cancel) return true;
        if (scene.Party)
        {
            if (cancel || _tutorCursor == _session.State.Party.Length) Apply(new(scene.Prefix + "cancel"));
            else
            {
                var mon = _session.State.Party[_tutorCursor];
                if (mon.IsEgg && scene.Prefix == "move_tutor_") { SourceSound("SE_FAILURE"); return true; }
                Apply(new(scene.Prefix + "choose", PartyIndex: _tutorCursor) { TargetInstanceId = mon.InstanceId });
            }
        }
        else if (scene.Replace || scene.Moves)
        {
            if (cancel || _tutorCursor == count - 1) Apply(new(scene.Prefix + "cancel"));
            else Apply(new(scene.Prefix + (scene.Replace && !scene.Delete ? "replace" : "move"), MoveSlot: _tutorCursor) { TargetInstanceId = scene.InstanceId });
        }
        else if (scene.Payment)
        {
            if (confirm) Apply(new("move_reminder_payment", Choice: _tutorCursor));
            else SourceSound("SE_FAILURE");
        }
        else if (scene.Choice) Apply(new(scene.Prefix + "choice", Choice: cancel ? 0 : _tutorCursor == 0 ? 1 : 0));
        else if (!scene.Learned || _fanfareRemaining <= 0) Apply(new(scene.Prefix + "ack"));
        return true;
    }

    private Rect2 TutorRow(int index)
    {
        var scene = Teaching!;
        return scene.Party ? index == _session.State.Party.Length ? TutorCancelRow : TutorPartyRow(index)
            : scene.Replace ? TutorMoveRow(index) : scene.Payment ? TutorPaymentRow(index)
            : scene.Moves ? new(8, 6 + (index - _tutorScroll) * 16, 224, 16) : TutorChoiceRow(index);
    }
    private int TutorPointerRow(Vector2 point)
    {
        int start = Teaching?.Moves == true ? _tutorScroll : 0;
        int end = Teaching?.Moves == true ? Math.Min(TutorRowCount, start + 6) : TutorRowCount;
        for (int i = start; i < end; i++) if (TutorRow(i).HasPoint(point)) return i;
        return -1;
    }
    private bool NavigateMoveTutorPointer(Vector2 point, int direction)
    {
        if (DayCareChoosing) return NavigateDayCarePartyPointer(point, direction);
        if (!MoveTutorActive) return false;
        int row = TutorPointerRow(point); if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_tutorCursor == row) return false;
        _tutorCursor = row; return true;
    }
    private bool MouseMoveTutor(Vector2 point)
    {
        if (DayCareChoosing) return MouseDayCareParty(point);
        int row = TutorPointerRow(point);
        if (row >= 0) { _tutorCursor = row; MouseKey(Key.Enter); return true; }
        if (TutorRowCount == 0 && TutorMessageRect.HasPoint(point)) { MouseKey(Key.Enter); return true; }
        return false;
    }

    private bool DrawMoveTutor()
    {
        if (DayCareResidentsChoosing) return false;
        if (DayCareChoosing) { DrawDayCareParty(); return true; }
        if (Teaching is not { } scene) return false;
        var state = _session.State;
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        if (scene.Party)
        {
            for (int i = 0; i < state.Party.Length; i++)
            {
                var mon = state.Party[i]; var row = TutorPartyRow(i); float y = row.Position.Y;
                if (_tutorCursor == i) DrawRect(row, new Color("e4e4c8"));
                DrawTextureRectRegion(PresentedOwnedIcon(mon), new(18, y, 16, 16), new(0, 0, 32, 32));
                SourceText(_sourceSmall, new(36, y), PresentedOwnedName(mon));
                if (!mon.IsEgg) SourceText(_sourceSmall, new(113, y), "Lv" + mon.Pokemon.Level);
                if (state.MoveTutor is { } tutor)
                {
                    var catalog = _catalog.MoveTutors!;
                    string label = MoveTutorRules.Eligibility(catalog.Definitions[tutor.TutorIndex], mon) switch
                    { MoveTutorEligibility.Able => "gText_Able2", MoveTutorEligibility.AlreadyKnown => "gText_Learned", _ => "gText_NotAble" };
                    SourceText(_sourceSmall, new(157, y), catalog.Texts[label]);
                }
                else if (state.MoveReminder is not null && !mon.IsEgg)
                    SourceText(_sourceSmall, new(158, y), MoveReminderRules.Candidates(ReminderCatalog, mon).Moves.Length + " MOVES");
            }
            SourceText(_sourceNormal, new(20, 116), "CANCEL");
            SourceText(_sourceNormal, new(8, TutorRow(_tutorCursor).Position.Y), "▶");
            SourceText(_sourceSmall, new(8, 140), state.MoveDeleter is not null ? DeleterCatalog.Texts["gText_ChoosePokemon"]
                : (state.MoveTutor is null ? ReminderCatalog.Texts : _catalog.MoveTutors!.Texts)["gText_TeachWhichPokemon"]);
        }
        else
        {
            var mon = state.Party[scene.PartyIndex];
            if (scene.Replace || scene.Moves)
            {
                _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
                int start = scene.Moves ? _tutorScroll : 0, end = scene.Moves ? Math.Min(TutorRowCount, start + 6) : TutorRowCount;
                for (int i = start; i < end; i++)
                {
                    var row = TutorRow(i); bool back = i == TutorRowCount - 1;
                    SourceText(_sourceNormal, new(24, row.Position.Y), back ? "CANCEL" : MoveName(scene.Moves ? state.MoveReminder!.Moves[i] : mon.Pokemon.Moves[i].Move));
                    if (!back)
                    {
                        string pp = scene.Moves ? "PP " + _catalog.PokemonContent!.Catalog.Moves[state.MoveReminder!.Moves[i]].Pp
                            : $"{mon.Pokemon.Moves[i].CurrentPp}/{mon.Pokemon.Moves[i].MaximumPp}";
                        SourceText(_sourceSmall, new(178, row.Position.Y), pp);
                    }
                    if (i == _tutorCursor) SourceText(_sourceNormal, new(10, row.Position.Y), "▶");
                }
                if (scene.Moves && TutorRowCount > 6)
                    SourceText(_sourceSmall, new(166, 98), $"{_tutorCursor + 1}/{TutorRowCount}");
            }
            else if (scene.Payment)
            {
                SourceText(_sourceNormal, new(12, 6), mon.Nickname);
                for (int i = 0; i < 2; i++)
                {
                    var row = TutorPaymentRow(i);
                    SourceText(_sourceNormal, new(24, row.Position.Y), ReminderCatalog.Texts[i == 0 ? "gText_2Tinymushrooms" : "gText_1BigMushroom"]);
                    if (i == _tutorCursor) SourceText(_sourceNormal, new(10, row.Position.Y), "▶");
                }
            }
            else
            {
                SourceText(_sourceNormal, new(12, 6), mon.Nickname);
                DrawTextureRectRegion(PresentedPokemonIcon(mon.Pokemon), new(108, 36, 32, 32), new(0, 0, 32, 32));
            }
            _battleSkin.DrawStandardFrame(this, TutorMessageRect);
            var lines = scene.Pages.IsEmpty ? Array.Empty<string>() : scene.Pages[scene.Page].Split('\n');
            for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 118 + i * 16), lines[i]);
            if (scene.Choice)
            {
                _battleSkin.DrawStandardFrame(this, new(176, 64, 64, 48));
                SourceText(_sourceNormal, new(198, 73), "YES"); SourceText(_sourceNormal, new(198, 89), "NO");
                SourceText(_sourceNormal, new(184, TutorChoiceRow(_tutorCursor).Position.Y), "▶");
            }
        }
        if (_fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_fade / (16.0 / 60), 0, 1)));
        return true;
    }
}
