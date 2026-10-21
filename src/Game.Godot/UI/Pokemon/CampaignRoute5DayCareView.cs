using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

/// <summary>Route 5's source party callback uses the existing party rows and bounded summary.
/// The field script owns all deposit refusals, withdrawal text, quotes and Yes/No choices.</summary>
public partial class CampaignView
{
    private int _dayCareActionCursor = -1, _dayCareSummaryPage = -1;
    private const int DayCareActionTop = 72;
    private static bool IsDayCareChoosing(CampaignState? state) =>
        state is not null && state.DayCareInteraction?.Stage is CampaignRoute5DayCareStage.Party or CampaignRoute5DayCareStage.Residents &&
        state.Script.Wait?.Kind == StoryWaitKind.PartySelection;
    private bool DayCareChoosing => IsDayCareChoosing(_session?.State);
    private bool DayCareResidentsChoosing => DayCareChoosing && _session.State.DayCareInteraction!.Stage == CampaignRoute5DayCareStage.Residents;
    private void ResetDayCarePartyPresentation()
    {
        _dayCareActionCursor = _dayCareSummaryPage = -1; _partyLastRightSlot = 1;
        ResetOwnedSummarySelection();
    }

    // pret 037335f: party_menu.c:2990,3109,3391,3840,6315; data/party_menu.h:1096,1099.
    // Eggs retain SUMMARY/CANCEL. STORE submits the live identity; the source script,
    // not this menu, decides whether the remaining party can battle.
    private SummaryPartyMenuEntry[] DayCarePartyActions() => SummaryInteractionPresentationAssets.DayCarePartyActions(
        _session.State.Party[_tutorCursor].IsEgg, _summaryPartyText.Label);
    private void DayCarePartyTransition()
    {
        _held = null; ReleaseFieldTextInput();
        _tutorTransitionFrame = Engine.GetProcessFrames();
    }

    private bool DayCarePartyInput(InputEventKey key)
    {
        if (SummaryExitActive) return true;
        if (DayCareResidentsChoosing) return DayCareResidentsInput(key);
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (confirm || cancel || key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right) SourceSound("SE_SELECT");
        if (_teachingSummaryNotice is not null)
        { if (confirm || cancel) _teachingSummaryNotice = null; return true; }
        if (_dayCareSummaryPage >= 0)
            return OwnedPartySummaryInput(key, ref _tutorCursor, ref _dayCareSummaryPage,
                () => { _dayCareSummaryPage = -1; _dayCareActionCursor = 0; DayCarePartyTransition(); });
        if (_dayCareActionCursor >= 0)
        {
            var actions = DayCarePartyActions();
            // The original two/three-entry action popup does not wrap.
            if (key.Keycode == Key.Up) _dayCareActionCursor = Math.Max(0, _dayCareActionCursor - 1);
            if (key.Keycode == Key.Down) _dayCareActionCursor = Math.Min(actions.Length - 1, _dayCareActionCursor + 1);
            var action = SummaryInteractionPresentationAssets.SelectedPartyAction(actions, _dayCareActionCursor, confirm, cancel);
            if (action == SummaryPartyAction.Cancel)
            {
                _dayCareActionCursor = -1; DayCarePartyTransition();
            }
            else if (action == SummaryPartyAction.Summary)
            {
                _dayCareSummaryPage = 0; ResetOwnedSummarySelection(); DayCarePartyTransition();
            }
            else if (action == SummaryPartyAction.Store)
            {
                var mon = _session.State.Party[_tutorCursor];
                Apply(new("daycare_deposit", PartyIndex: _tutorCursor) { TargetInstanceId = mon.InstanceId });
            }
            return true;
        }
        int count = _session.State.Party.Length + 1;
        NativePartyMove(key.Keycode, ref _tutorCursor, count - 1);
        if (cancel || confirm && _tutorCursor == count - 1) Apply(new("daycare_cancel"));
        else if (confirm) { _dayCareActionCursor = 0; DayCarePartyTransition(); }
        return true;
    }

    private bool NavigateDayCarePartyPointer(Vector2 point, int direction)
    {
        if (DayCareResidentsChoosing) return PointerRows(point, direction, ref _tutorCursor,
            _session.State.DayCareInteraction!.Residents.Length + 1, 96, 8, 136, 14);
        if (_teachingSummaryNotice is not null) return false;
        if (_dayCareSummaryPage >= 0)
            return NavigateOwnedSummaryPointer(point, direction, _session.State.Party[_tutorCursor]);
        if (_dayCareActionCursor >= 0)
            return PointerRows(point, direction, ref _dayCareActionCursor, DayCarePartyActions().Length, 160, DayCareActionTop, 72);
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_tutorCursor == row) return false;
        _tutorCursor = row; return true;
    }

    private bool MouseDayCareParty(Vector2 point)
    {
        if (DayCareResidentsChoosing) return MouseSelect(point, ref _tutorCursor,
            _session.State.DayCareInteraction!.Residents.Length + 1, 96, 8, 136, 14);
        if (_teachingSummaryNotice is not null) return MouseConfirm(point, MouseMessage);
        if (_dayCareSummaryPage >= 0)
            return MouseOwnedSummary(point, _session.State.Party[_tutorCursor], _dayCareSummaryPage);
        if (_dayCareActionCursor >= 0)
            return MouseSelect(point, ref _dayCareActionCursor, DayCarePartyActions().Length, 160, DayCareActionTop, 72, 16);
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _tutorCursor = row; return MouseKey(Key.Enter);
    }

    private void DrawDayCareParty()
    {
        if (_dayCareSummaryPage >= 0)
        {
            DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
            DrawOwnedSummary(_session.State.Party[_tutorCursor], _dayCareSummaryPage, true);
            if (_teachingSummaryNotice is not null) ItemMessageBox(_teachingSummaryNotice);
            return;
        }
        // Source strings.c:323 and 314. PKMN's compact glyph is expanded by this
        // existing functional surface; original party/summary pixels remain separate.
        DrawServicePartyChooser(_tutorCursor, _dayCareActionCursor < 0 ? _summaryPartyText.ChoosePrompt : _summaryPartyText.ActionPrompt);
        if (_dayCareActionCursor < 0) return;
        var actions = DayCarePartyActions();
        _battleSkin.DrawStandardFrame(this, new(152, DayCareActionTop - 8, 88, actions.Length * 16 + 16));
        for (int i = 0; i < actions.Length; i++) SourceText(_sourceNormal, new(170, DayCareActionTop + i * 16), actions[i].Label);
        SourceText(_sourceNormal, new(160, DayCareActionTop + _dayCareActionCursor * 16), "▶");
    }

    private bool DayCareResidentsInput(InputEventKey key)
    {
        var rows = _session.State.DayCareInteraction!.Residents;
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (key.Keycode == Key.Up) _tutorCursor = Math.Max(0, _tutorCursor - 1);
        if (key.Keycode == Key.Down) _tutorCursor = Math.Min(rows.Length, _tutorCursor + 1);
        if (confirm || cancel || key.Keycode is Key.Up or Key.Down) SourceSound("SE_SELECT");
        if (cancel || confirm && _tutorCursor == rows.Length) Apply(new("daycare_cancel"));
        else if (confirm)
        {
            var row = rows[_tutorCursor];
            Apply(new("daycare_resident", Choice: row.Slot) { TargetInstanceId = row.InstanceId });
        }
        return true;
    }

    // Original daycare.c level window: interior tile(12,1),17x5; two live levels and EXIT.
    // Script callbacks retain fee, refusal, pending-Egg and Yes/No dialogue ownership.
    private void DrawDayCareResidents()
    {
        if (!DayCareResidentsChoosing) return;
        var rows = _session.State.DayCareInteraction!.Residents;
        _battleSkin.DrawUserFrame(this, new(88, 0, 152, 56));
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; int y = 8 + i * 14;
            var mon = (row.Slot == 0 ? _session.State.FourIslandDayCare?.First : _session.State.FourIslandDayCare?.Second)?.Pokemon;
            if (mon is null || mon.InstanceId != row.InstanceId) throw new InvalidDataException("Day-Care resident presentation identity differs.");
            string name = _catalog.PokemonDisplayName(mon);
            SourceText(_sourceNormal, new(104, y), name);
            if (mon.Pokemon.Gender != PokemonGender.Genderless)
            {
                float nameWidth = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(name), fontSize: _sourceNormal.Size).X;
                SourceText(mon.Pokemon.Gender == PokemonGender.Female ? _healthFemale : _healthMale,
                    new(104 + nameWidth, y), mon.Pokemon.Gender == PokemonGender.Female ? "♀" : "♂");
            }
            string level = "Lv" + row.Level;
            float width = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(level), fontSize: _sourceNormal.Size).X;
            SourceText(_sourceNormal, new(228 - width, y), level);
        }
        SourceText(_sourceNormal, new(104, 8 + rows.Length * 14), "EXIT");
        SourceText(_sourceNormal, new(96, 8 + _tutorCursor * 14), "▶");
    }
}
