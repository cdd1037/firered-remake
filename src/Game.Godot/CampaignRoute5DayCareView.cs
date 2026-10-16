using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
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
    private void ResetDayCarePartyPresentation() { _dayCareActionCursor = _dayCareSummaryPage = -1; _partyLastRightSlot = 1; }

    // pret 037335f: party_menu.c:2990,3109,3391,3840,6315; data/party_menu.h:1096,1099.
    // Eggs retain SUMMARY/CANCEL. STORE submits the live identity; the source script,
    // not this menu, decides whether the remaining party can battle.
    private string[] DayCarePartyActions() => _session.State.Party[_tutorCursor].IsEgg
        ? ["SUMMARY", "CANCEL"] : ["STORE", "SUMMARY", "CANCEL"];
    private void DayCarePartyTransition()
    {
        _held = null; _repeat = 0; ReleaseFieldTextInput();
        _tutorTransitionFrame = Engine.GetProcessFrames();
    }

    private bool DayCarePartyInput(InputEventKey key)
    {
        if (SummaryExitActive) return true;
        if (DayCareResidentsChoosing) return DayCareResidentsInput(key);
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (confirm || cancel || key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right) SourceSound("SE_SELECT");
        if (_dayCareSummaryPage >= 0)
        {
            if (cancel)
            {
                BeginSummaryExit(() => { _dayCareSummaryPage = -1; _dayCareActionCursor = 0; DayCarePartyTransition(); });
            }
            else
            {
                if (!_session.State.Party[_tutorCursor].IsEgg)
                {
                    if (key.Keycode == Key.Left) _dayCareSummaryPage = (_dayCareSummaryPage + 2) % 3;
                    if (key.Keycode == Key.Right) _dayCareSummaryPage = (_dayCareSummaryPage + 1) % 3;
                }
                if (key.Keycode == Key.Up) _tutorCursor = (_tutorCursor + _session.State.Party.Length - 1) % _session.State.Party.Length;
                if (key.Keycode == Key.Down) _tutorCursor = (_tutorCursor + 1) % _session.State.Party.Length;
                if (_session.State.Party[_tutorCursor].IsEgg) _dayCareSummaryPage = 0;
            }
            return true;
        }
        if (_dayCareActionCursor >= 0)
        {
            var actions = DayCarePartyActions();
            // The original two/three-entry action popup does not wrap.
            if (key.Keycode == Key.Up) _dayCareActionCursor = Math.Max(0, _dayCareActionCursor - 1);
            if (key.Keycode == Key.Down) _dayCareActionCursor = Math.Min(actions.Length - 1, _dayCareActionCursor + 1);
            if (cancel || confirm && actions[_dayCareActionCursor] == "CANCEL")
            {
                _dayCareActionCursor = -1; DayCarePartyTransition();
            }
            else if (confirm && actions[_dayCareActionCursor] == "SUMMARY")
            {
                _dayCareSummaryPage = 0; DayCarePartyTransition();
            }
            else if (confirm)
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
        if (_dayCareSummaryPage >= 0) return false;
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
        if (_dayCareSummaryPage >= 0)
        {
            if (new Rect2(104, 0, 24, 16).HasPoint(point)) return MouseKey(Key.Left);
            if (new Rect2(128, 0, 80, 16).HasPoint(point)) return MouseKey(Key.Right);
            if (new Rect2(208, 0, 32, 16).HasPoint(point)) return MouseKey(Key.Backspace);
            return false;
        }
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
            DrawPartySummary(_tutorCursor, _dayCareSummaryPage);
            return;
        }
        // Source strings.c:323 and 314. PKMN's compact glyph is expanded by this
        // existing functional surface; original party/summary pixels remain separate.
        DrawServicePartyChooser(_tutorCursor, _dayCareActionCursor < 0 ? "Choose a POKéMON." : "Do what with this POKéMON?");
        if (_dayCareActionCursor < 0) return;
        var actions = DayCarePartyActions();
        _battleSkin.DrawStandardFrame(this, new(152, DayCareActionTop - 8, 88, actions.Length * 16 + 16));
        for (int i = 0; i < actions.Length; i++) SourceText(_sourceNormal, new(170, DayCareActionTop + i * 16), actions[i]);
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
            SourceText(_sourceNormal, new(104, y), row.Nickname);
            var mon = (row.Slot == 0 ? _session.State.FourIslandDayCare?.First : _session.State.FourIslandDayCare?.Second)?.Pokemon;
            if (mon is null || mon.InstanceId != row.InstanceId) throw new InvalidDataException("Day-Care resident presentation identity differs.");
            if (mon.Pokemon.Gender != PokemonGender.Genderless)
            {
                float nameWidth = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(row.Nickname), fontSize: _sourceNormal.Size).X;
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
