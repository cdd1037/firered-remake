using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// Shared field/PC move-detail selection. Only a cursor and an uncommitted swap
// draft live here. The complete owned move slots are changed by Application.
public partial class CampaignView
{
    private bool _summaryDetails;
    private int _summaryMoveCursor, _summarySwapSource = -1;
    private long _summaryMoveRevision, _summaryMoveInstanceId;
    private bool _summaryMoveStorage;
    private PokemonStorageMoveLocation? _summaryMoveLocation;
    private const string SummaryChangedNotice = "The selected POKéMON has changed.\nPlease open its moves again.";

    private void ResetOwnedSummarySelection()
    {
        _summaryDetails = false; _summaryMoveCursor = 0; _summarySwapSource = -1;
        _summaryMoveInstanceId = 0; _summaryMoveLocation = null;
    }
    private void OpenOwnedMoveDetails(PartyPokemon owned)
    {
        if (owned.IsEgg || owned.Pokemon.Moves.IsDefaultOrEmpty) return;
        ResetOwnedSummarySelection(); _summaryDetails = true;
        CaptureOwnedSummarySelection(owned);
    }
    private void CaptureOwnedSummarySelection(PartyPokemon owned)
    {
        _summaryMoveRevision = _session.Revision; _summaryMoveInstanceId = owned.InstanceId;
        _summaryMoveStorage = _session.State.Pc?.Summary is not null;
        _summaryMoveLocation = _session.State.Pc?.Summary?.Location;
    }
    private bool OwnedSummarySelectionIsCurrent(PartyPokemon owned)
    {
        if (_summaryMoveRevision == _session.Revision && _summaryMoveInstanceId == owned.InstanceId &&
            _summaryMoveStorage == (_session.State.Pc?.Summary is not null) &&
            _summaryMoveLocation == _session.State.Pc?.Summary?.Location) return true;
        ResetOwnedSummarySelection();
        if (_session.State.Pc is not null) _pcNotice = SummaryChangedNotice;
        else _fieldMenuNotice = SummaryChangedNotice;
        return false;
    }
    private bool OwnedMoveDetailsInput(InputEventKey key, PartyPokemon owned)
    {
        if (!_summaryDetails) return false;
        if (!OwnedSummarySelectionIsCurrent(owned)) return true;
        bool back = key.Keycode is Key.Backspace or Key.B;
        if (back)
        {
            if (_summarySwapSource >= 0) _summarySwapSource = -1;
            else ResetOwnedSummarySelection();
            return true;
        }
        int count = owned.Pokemon.Moves.Length + (_summarySwapSource < 0 ? 1 : 0);
        if (key.Keycode == Key.Up) _summaryMoveCursor = (_summaryMoveCursor + count - 1) % count;
        if (key.Keycode == Key.Down) _summaryMoveCursor = (_summaryMoveCursor + 1) % count;
        if (key.Keycode != Key.Enter) return true;
        if (_summaryMoveCursor == owned.Pokemon.Moves.Length) { ResetOwnedSummarySelection(); return true; }
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
               bool accepted = Apply(new("party_summary_swap", PartyIndex: _fieldPartyCursor, MoveSlot: source, Choice: _summaryMoveCursor)
                { TargetInstanceId = owned.InstanceId });
            if (accepted) CaptureOwnedSummarySelection(_session.State.Party[_fieldPartyCursor]);
            else { ResetOwnedSummarySelection(); _fieldMenuNotice = "The move order could not be\nchanged here."; }
        }
        return true;
    }
    private bool FieldSummaryInput(InputEventKey key)
    {
        if (SummaryExitActive) return true;
        var owned = _session.State.Party[_fieldPartyCursor];
        if (OwnedMoveDetailsInput(key, owned)) return true;
        if (key.Keycode is Key.Backspace or Key.B || key.Keycode == Key.Enter && _fieldSummaryPage == 0)
        { BeginSummaryExit(() => { ResetOwnedSummarySelection(); _fieldMenuMode = "party"; }); return true; }
        if (!owned.IsEgg)
        {
            if (key.Keycode == Key.Left) _fieldSummaryPage = Math.Max(0, _fieldSummaryPage - 1);
            if (key.Keycode == Key.Right) _fieldSummaryPage = Math.Min(2, _fieldSummaryPage + 1);
            if (key.Keycode == Key.Enter && _fieldSummaryPage == 2) OpenOwnedMoveDetails(owned);
        }
        if (key.Keycode is Key.Up or Key.Down && PokemonSummaryRules.SeekPartyIndex(_session.State.Party,
            _fieldPartyCursor, (PokemonSummaryPage)_fieldSummaryPage, key.Keycode == Key.Up ? -1 : 1) is { } next)
            _fieldPartyCursor = next;
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
            int rows = owned.Pokemon.Moves.Length + (_summarySwapSource < 0 ? 1 : 0);
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
            owned.Pokemon.Moves.Length + (_summarySwapSource < 0 ? 1 : 0), 120, 18, 120, 28);
    }
    private void DrawOwnedMoveDetails(PartyPokemon owned) => DrawNativeSummary(owned, 2, true, true);
}
