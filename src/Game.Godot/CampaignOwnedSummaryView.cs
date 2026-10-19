using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// Shared field/PC/service move-detail selection. Only a cursor and an uncommitted swap
// draft live here. The complete owned move slots are changed by Application.
public partial class CampaignView
{
    private bool _summaryDetails;
    private SummaryInteractionMode _summaryMode = SummaryInteractionMode.Reorder;
    private int _summaryMoveCursor, _summarySwapSource = -1;
    private long _summaryMoveRevision, _summaryMoveInstanceId;
    private bool _summaryMoveStorage;
    private string? _summaryMoveContext;
    private PokemonStorageMoveLocation? _summaryMoveLocation;
    private SummaryMoveSelection OwnedMoveSelection => new(_summaryMode, _summaryMoveCursor, _summarySwapSource);
    private const string SummaryChangedNotice = "The selected POKéMON has changed.\nPlease open its moves again.";

    private void ResetOwnedSummarySelection()
    {
        _summaryDetails = false; _summaryMoveCursor = 0; _summarySwapSource = -1;
        _summaryMoveInstanceId = 0; _summaryMoveLocation = null; _summaryMoveContext = null; _summaryMode = SummaryInteractionMode.Reorder;
    }
    private void OpenOwnedMoveDetails(PartyPokemon owned, SummaryInteractionMode mode = SummaryInteractionMode.Reorder)
    {
        if (owned.IsEgg || owned.Pokemon.Moves.IsDefaultOrEmpty) return;
        ResetOwnedSummarySelection(); _summaryDetails = true; _summaryMode = mode;
        CaptureOwnedSummarySelection(owned);
    }
    private void CaptureOwnedSummarySelection(PartyPokemon owned)
    {
        _summaryMoveRevision = _session.Revision; _summaryMoveInstanceId = owned.InstanceId;
        _summaryMoveContext = SummaryContext;
        _summaryMoveStorage = _session.State.Pc?.Summary is not null;
        _summaryMoveLocation = _session.State.Pc?.Summary?.Location;
    }
    private bool OwnedSummarySelectionIsCurrent(PartyPokemon owned)
    {
        if (_summaryMoveRevision == _session.Revision && _summaryMoveInstanceId == owned.InstanceId &&
            _summaryMoveContext == SummaryContext &&
            _summaryMoveStorage == (_session.State.Pc?.Summary is not null) &&
            _summaryMoveLocation == _session.State.Pc?.Summary?.Location) return true;
        ResetOwnedSummarySelection();
        if (_battlePartySelection is not null) CloseStaleBattlePartySelection();
        else if (_session.State.Pc is not null) _pcNotice = SummaryChangedNotice;
        else if (DayCareChoosing) _teachingSummaryNotice = SummaryChangedNotice;
        else _fieldMenuNotice = SummaryChangedNotice;
        return false;
    }
    private bool OwnedMoveDetailsInput(InputEventKey key, PartyPokemon owned, int partyIndex = -1)
    {
        if (!_summaryDetails) return false;
        if (!OwnedSummarySelectionIsCurrent(owned)) return true;
        bool back = key.Keycode is Key.Backspace or Key.Escape or Key.B;
        if (back)
        {
            if (_summarySwapSource >= 0) _summarySwapSource = -1;
            else ResetOwnedSummarySelection();
            return true;
        }
        int count = OwnedMoveSelection.RowCount(owned.Pokemon.Moves.Length);
        if (key.Keycode == Key.Up) _summaryMoveCursor = (_summaryMoveCursor + count - 1) % count;
        if (key.Keycode == Key.Down) _summaryMoveCursor = (_summaryMoveCursor + 1) % count;
        if (key.Keycode is not (Key.Enter or Key.KpEnter or Key.A)) return true;
        if (_summaryMoveCursor == owned.Pokemon.Moves.Length) { ResetOwnedSummarySelection(); return true; }
        if (!OwnedMoveSelection.CanReorder) return true;
        if (_summarySwapSource < 0) { _summarySwapSource = _summaryMoveCursor; return true; }
        int source = _summarySwapSource; _summarySwapSource = -1;
        if (source == _summaryMoveCursor) return true;
        if (_session.State.Pc?.Summary is { } summary)
        {
            if (StorageSummaryAction(BindStorageSummary(new("storage_summary_swap", Choice: _summaryMoveCursor, MoveSlot: source), summary)))
                CaptureOwnedSummarySelection(_session.StorageSummaryPokemon!);
        }
        else
        {
            bool accepted = Apply(new("party_summary_swap", PartyIndex: partyIndex, MoveSlot: source, Choice: _summaryMoveCursor)
                { TargetInstanceId = owned.InstanceId });
            if (accepted) CaptureOwnedSummarySelection(_session.State.Party[partyIndex]);
            else
            {
                ResetOwnedSummarySelection();
                const string notice = "The move order could not be\nchanged here.";
                if (DayCareChoosing) _teachingSummaryNotice = notice;
                else _fieldMenuNotice = notice;
            }
        }
        return true;
    }
    private bool FieldSummaryInput(InputEventKey key) => OwnedPartySummaryInput(key,
        ref _fieldPartyCursor, ref _fieldSummaryPage, () => _fieldMenuMode = "party");

    // Field and Day-Care PSS_MODE_NORMAL share page bounds, egg-aware party
    // browsing, move details, two-stage swap/cancel and the native exit fade.
    private bool OwnedPartySummaryInput(InputEventKey key, ref int partyIndex, ref int page, Action close)
    {
        if (SummaryExitActive) return true;
        var owned = _session.State.Party[partyIndex];
        if (OwnedMoveDetailsInput(key, owned, partyIndex)) return true;
        bool confirm = key.Keycode is Key.Enter or Key.KpEnter or Key.A;
        if (key.Keycode is Key.Backspace or Key.Escape or Key.B || confirm && page == 0)
        { BeginSummaryExit(() => { ResetOwnedSummarySelection(); close(); }); return true; }
        if (!owned.IsEgg)
        {
            if (key.Keycode == Key.Left) page = Math.Max(0, page - 1);
            if (key.Keycode == Key.Right) page = Math.Min(2, page + 1);
            if (confirm && page == 2) OpenOwnedMoveDetails(owned);
        }
        if (key.Keycode is Key.Up or Key.Down && PokemonSummaryRules.SeekPartyIndex(_session.State.Party,
            partyIndex, (PokemonSummaryPage)page, key.Keycode == Key.Up ? -1 : 1) is { } next)
            partyIndex = next;
        return true;
    }
    private bool MouseOwnedSummary(Vector2 point, PartyPokemon owned, int page)
    {
        if (SummaryExitActive) return true;
        if (_summaryDetails)
        {
            if (!OwnedSummarySelectionIsCurrent(owned)) return true;
            if (new Rect2(208, 0, 32, 16).HasPoint(point)) return MouseKey(Key.Backspace);
            if (new Rect2(152, 0, 56, 16).HasPoint(point)) return MouseKey(Key.Enter);
            int rows = OwnedMoveSelection.RowCount(owned.Pokemon.Moves.Length);
            return MouseSelect(point, ref _summaryMoveCursor, rows, 120, 18, 120, 28);
        }
        if (!owned.IsEgg && page > 0 && new Rect2(104, 0, 24, 16).HasPoint(point)) return MouseKey(Key.Left);
        if (!owned.IsEgg && new Rect2(128, 0, 80, 16).HasPoint(point)) return MouseKey(page == 2 ? Key.Enter : Key.Right);
        if (new Rect2(208, 0, 32, 16).HasPoint(point)) return MouseKey(Key.Backspace);
        return false;
    }
    private bool NavigateOwnedSummaryPointer(Vector2 point, int direction, PartyPokemon owned)
    {
        if (SummaryExitActive) return true;
        if (!_summaryDetails) return direction != 0 && new Rect2(8, 26, 224, 110).HasPoint(point) && MouseNavigationKey(direction);
        if (!OwnedSummarySelectionIsCurrent(owned)) return true;
        return PointerRows(point, direction, ref _summaryMoveCursor,
            OwnedMoveSelection.RowCount(owned.Pokemon.Moves.Length), 120, 18, 120, 28);
    }
    private void DrawOwnedMoveDetails(PartyPokemon owned) => DrawNativeSummary(owned, 2, true, OwnedMoveSelection);
}
