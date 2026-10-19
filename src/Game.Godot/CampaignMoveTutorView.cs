using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
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
    private static Rect2 TutorPartyRow(int index) => NativePartySlot(index);
    private static Rect2 TutorMoveRow(int index) => new(120, 18 + index * 28, 120, 28);
    private static Rect2 TutorChoiceRow(int index) => new(182, 73 + index * 16, 50, 16);
    private static Rect2 TutorPaymentRow(int index) => new(8, 32 + index * 24, 224, 22);
    private static Rect2 TutorMessageRect => new(0, 112, 240, 48);

    private void SynchronizeMoveTutorPresentation(CampaignState? before, CampaignState after)
    {
        if (before?.MoveTutor?.Stage != after.MoveTutor?.Stage || before?.MoveTutor?.Page != after.MoveTutor?.Page ||
            before?.MoveReminder?.Stage != after.MoveReminder?.Stage || before?.MoveReminder?.Page != after.MoveReminder?.Page ||
            before?.MoveDeleter?.Stage != after.MoveDeleter?.Stage || before?.DayCareInteraction?.Stage != after.DayCareInteraction?.Stage || IsDayCareChoosing(before) != IsDayCareChoosing(after))
        {
            _moveSelectionStamp = null; _teachingSummaryNotice = null;
            _held = null; _repeat = 0; ReleaseFieldTextInput(); _tutorCursor = 0; _tutorScroll = 0;
            ResetDayCarePartyPresentation();
            _tutorTransitionFrame = Engine.GetProcessFrames();
        }
        bool oldActive = before?.MoveTutor is not null || before?.MoveReminder is { Stage: not CampaignMoveReminderStage.FieldScript } ||
            before?.MoveDeleter is { Stage: not CampaignMoveDeleterStage.FieldScript } || IsDayCareChoosing(before);
        if (oldActive != MoveTutorActive) _fade = 16.0 / 60;
    }

    private bool ConsumeMoveTutorTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _tutorTransitionFrame);

    private int TutorRowCount => Teaching is not { } scene ? 0 : scene.Party ? _session.State.Party.Length + 1 :
        scene.Replace ? _session.State.Party[scene.PartyIndex].Pokemon.Moves.Length + 1 : scene.Moves ? _session.State.MoveReminder!.Moves.Length + 1 : scene.Choice || scene.Payment ? 2 : 0;
    private void TutorKeepCursorVisible()
    {
        if (Teaching?.Moves != true) return;
        _tutorScroll = SourceVerticalMenuPresentation.KeepSelectionVisible(_tutorCursor, _tutorScroll, TutorRowCount, 5);
    }

    private bool MoveTutorInput(InputEventKey key)
    {
        if (!MoveTutorActive || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        if (DayCareChoosing) return DayCarePartyInput(key);
        var scene = Teaching!;
        if (_teachingSummaryNotice is not null)
        { if (key.Keycode is Key.Enter or Key.A or Key.Backspace or Key.B) _teachingSummaryNotice = null; return true; }
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        int count = TutorRowCount;
        if (count > 0)
        {
            if(scene.Party)NativePartyMove(key.Keycode,ref _tutorCursor,count-1);
            else if (key.Keycode is Key.Up or Key.Left) _tutorCursor = (_tutorCursor + count - 1) % count;
            else if (key.Keycode is Key.Down or Key.Right) _tutorCursor = (_tutorCursor + 1) % count;
            TutorKeepCursorVisible();
        }
        if (!confirm && !cancel) return true;
        if ((scene.Replace || scene.Moves) && !MoveSelectionCurrent(scene.Prefix,
            _session.State.Party[scene.PartyIndex], TeachingNewMove(scene)))
        { _tutorCursor = 0; _tutorScroll = 0; _teachingSummaryNotice = SummaryChangedNotice; return true; }
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
        return scene.Party ? index == _session.State.Party.Length ? NativePartyCancel : TutorPartyRow(index)
            : scene.Replace ? TutorMoveRow(index) : scene.Payment ? TutorPaymentRow(index)
            : scene.Moves ? TutorMoveRow(index - _tutorScroll) : TutorChoiceRow(index);
    }
    private int TutorPointerRow(Vector2 point)
    {
        int start = Teaching?.Moves == true ? _tutorScroll : 0;
        int end = Teaching?.Moves == true ? Math.Min(TutorRowCount, start + 5) : TutorRowCount;
        for (int i = start; i < end; i++) if (TutorRow(i).HasPoint(point)) return i;
        return -1;
    }
    private bool NavigateMoveTutorPointer(Vector2 point, int direction)
    {
        if (DayCareChoosing) return NavigateDayCarePartyPointer(point, direction);
        if (!MoveTutorActive || _teachingSummaryNotice is not null) return false;
        int row = TutorPointerRow(point); if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_tutorCursor == row) return false;
        _tutorCursor = row; return true;
    }
    private bool MouseMoveTutor(Vector2 point)
    {
        if (DayCareChoosing) return MouseDayCareParty(point);
        if (_teachingSummaryNotice is not null) return MouseConfirm(point, MouseMessage);
        if (Teaching is { } screen && (screen.Replace || screen.Moves))
        {
            if (new Rect2(208, 0, 32, 16).HasPoint(point)) return MouseKey(Key.Backspace);
            if (new Rect2(152, 0, 56, 16).HasPoint(point)) return MouseKey();
        }
        int row = TutorPointerRow(point);
        if (row >= 0) { _tutorCursor = row; MouseKey(Key.Enter); return true; }
        if (TutorRowCount == 0 && TutorMessageRect.HasPoint(point)) { MouseKey(Key.Enter); return true; }
        return false;
    }

    private string? _teachingSummaryNotice;
    private MoveId? TeachingNewMove(TeachingPresentation scene) => scene.Delete || scene.Moves ? null
        : _session.State.MoveTutor is { } tutor ? _catalog.MoveTutors!.Definitions[tutor.TutorIndex].Move
        : _session.State.MoveReminder!.Move;

    private bool DrawMoveTutor()
    {
        if (DayCareResidentsChoosing) return false;
        if (DayCareChoosing) { DrawDayCareParty(); return true; }
        if (Teaching is not { } scene) return false;
        var state = _session.State;
        if (scene.Replace || scene.Moves)
        {
            var owned = state.Party[scene.PartyIndex];
            if (scene.Replace) DrawMoveSelectionSummary(scene.Prefix, owned, TeachingNewMove(scene), _tutorCursor);
            else
            {
                if (_moveSelectionStamp is null) CaptureMoveSelection(scene.Prefix, owned, null);
                DrawNativeSummary(owned, 2, true, new(SummaryInteractionMode.SelectNew, _tutorCursor,
                    Candidates: state.MoveReminder!.Moves, FirstRow: _tutorScroll));
            }
            if (_teachingSummaryNotice is not null) ItemMessageBox(_teachingSummaryNotice);
            return true;
        }
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        if (scene.Party)
        {
            string? Description(int i)
            {
                if(state.MoveTutor is not {} tutor)return null;
                var catalog=_catalog.MoveTutors!;
                string symbol=MoveTutorRules.Eligibility(catalog.Definitions[tutor.TutorIndex],state.Party[i]) switch
                { MoveTutorEligibility.Able=>"gText_Able2",MoveTutorEligibility.AlreadyKnown=>"gText_Learned",_=>"gText_NotAble" };
                return catalog.Texts[symbol];
            }
            DrawNativeParty(state.Party,_tutorCursor,state.MoveDeleter is not null ? DeleterCatalog.Texts["gText_ChoosePokemon"]
                : (state.MoveTutor is null ? ReminderCatalog.Texts : _catalog.MoveTutors!.Texts)["gText_TeachWhichPokemon"],description:Description);
        }
        else
        {
            var mon = state.Party[scene.PartyIndex];
            if (scene.Payment)
            {
                SourceText(_sourceNormal, new(12, 6), _catalog.PokemonDisplayName(mon));
                for (int i = 0; i < 2; i++)
                {
                    var row = TutorPaymentRow(i);
                    SourceText(_sourceNormal, new(24, row.Position.Y), ReminderCatalog.Texts[i == 0 ? "gText_2Tinymushrooms" : "gText_1BigMushroom"]);
                    if (i == _tutorCursor) SourceText(_sourceNormal, new(10, row.Position.Y), "▶");
                }
            }
            else
            {
                SourceText(_sourceNormal, new(12, 6), _catalog.PokemonDisplayName(mon));
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
