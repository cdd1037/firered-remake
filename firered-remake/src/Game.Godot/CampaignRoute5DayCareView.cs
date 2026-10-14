using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Route 5's source party callback uses the existing party rows and bounded summary.
/// The field script owns all deposit refusals, withdrawal text, quotes and Yes/No choices.</summary>
public partial class CampaignView
{
    private int _dayCareActionCursor = -1, _dayCareSummaryPage = -1;
    private const int DayCareActionTop = 72;
    private static bool IsDayCareChoosing(CampaignState? state) =>
        state is not null && state.DayCareInteraction?.Stage == CampaignRoute5DayCareStage.Party &&
        state.Script.Wait?.Kind == StoryWaitKind.PartySelection;
    private bool DayCareChoosing => IsDayCareChoosing(_session?.State);
    private void ResetDayCarePartyPresentation() => _dayCareActionCursor = _dayCareSummaryPage = -1;

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
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (confirm || cancel || key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right) SourceSound("SE_SELECT");
        if (_dayCareSummaryPage >= 0)
        {
            if (cancel)
            {
                _dayCareSummaryPage = -1; _dayCareActionCursor = 0;
                DayCarePartyTransition();
            }
            else
            {
                if (key.Keycode == Key.Left) _dayCareSummaryPage = (_dayCareSummaryPage + 2) % 3;
                if (key.Keycode == Key.Right) _dayCareSummaryPage = (_dayCareSummaryPage + 1) % 3;
                if (key.Keycode == Key.Up) _tutorCursor = (_tutorCursor + _session.State.Party.Length - 1) % _session.State.Party.Length;
                if (key.Keycode == Key.Down) _tutorCursor = (_tutorCursor + 1) % _session.State.Party.Length;
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
        if (key.Keycode is Key.Up or Key.Left) _tutorCursor = (_tutorCursor + count - 1) % count;
        if (key.Keycode is Key.Down or Key.Right) _tutorCursor = (_tutorCursor + 1) % count;
        if (cancel || confirm && _tutorCursor == count - 1) Apply(new("daycare_cancel"));
        else if (confirm) { _dayCareActionCursor = 0; DayCarePartyTransition(); }
        return true;
    }

    private bool NavigateDayCarePartyPointer(Vector2 point, int direction)
    {
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
        if (_dayCareSummaryPage >= 0)
        {
            if (new Rect2(8, 140, 60, 16).HasPoint(point)) return MouseKey(Key.Left);
            if (new Rect2(84, 140, 68, 16).HasPoint(point)) return MouseKey(Key.Right);
            if (new Rect2(172, 140, 60, 16).HasPoint(point)) return MouseKey(Key.Backspace);
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
}
