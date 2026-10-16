using Game.Application;
using Godot;

public partial class CampaignView
{
    private BattleMoveOrderSelection? _battleMoveOrderSelection;
    private CampaignBattleMoveOrderContext? _battleMoveOrderContext;

    private bool BattleMoveOrderInput(InputEventKey key, string menu, ref int cursor, out string? notice)
    {
        notice = null;
        if (menu != "moves") { ResetBattleMoveOrder(); return false; }
        var current = _session.BattleMoveOrder;
        if (_battleMoveOrderSelection is { } selected)
        {
            if (current is null || !selected.Matches(_session.Revision, current.BattleId, current.BattleRevision,
                current.ActorSlot, current.InstanceId) || current.Actor != _battleMoveOrderContext!.Actor)
            {
                cursor = Math.Clamp(selected.SourceSlot, 0, Math.Max(0, (current?.Moves.Length ?? 1) - 1));
                ResetBattleMoveOrder(); notice = "The battle selection changed.\nChoose a move again."; return true;
            }
            if (key.Keycode is Key.Backspace or Key.B)
            { cursor = selected.CancelCursor; ResetBattleMoveOrder(); SourceSound("SE_SELECT"); return true; }
            if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
            { cursor = BattleMoveOrderSelection.Navigate(cursor, current.Moves.Length, key.Keycode is Key.Left or Key.Right); SourceSound("SE_SELECT"); return true; }
            if (key.Keycode is Key.Enter or Key.R)
            {
                var command = new CampaignCommand("battle_move_swap") {
                    TargetInstanceId = selected.InstanceId, BattleMoveSwap = _battleMoveOrderContext.Swap(selected.SourceSlot, cursor) };
                ResetBattleMoveOrder();
                if (selected.SourceSlot != cursor && !Apply(command))
                    notice = _lastCommandFault ? "The move order was not committed." : "The moves could not be switched.\nChoose a move again.";
                SourceSound("SE_SELECT"); return true;
            }
            return true;
        }
        if (key.Keycode != Key.R) return false; // Normalized physical SELECT (Space / joypad Back).
        if (current is null || current.Moves.Length < 2) return true;
        _battleMoveOrderContext = current;
        _battleMoveOrderSelection = new(_session.Revision, current.BattleId, current.BattleRevision,
            current.ActorSlot, current.InstanceId, Math.Clamp(cursor, 0, current.Moves.Length - 1));
        cursor = _battleMoveOrderSelection.InitialDestination;
        SourceSound("SE_SELECT"); return true;
    }

    private void ResetBattleMoveOrder() { _battleMoveOrderSelection = null; _battleMoveOrderContext = null; }

    private bool DrawBattleMoveOrder(int destination)
    {
        if (_battleMoveOrderSelection is not { } selection || _session.BattleMoveOrder is not { } current ||
            !selection.Matches(_session.Revision, current.BattleId, current.BattleRevision, current.ActorSlot, current.InstanceId)) return false;
        _uiCorrections.DrawMoveSwapCursor(this, selection.SourceSlot, false);
        if (destination != selection.SourceSlot) _uiCorrections.DrawMoveSwapCursor(this, destination, true);
        SourceText(_battleMenuFont, new(168,122), "Switch");
        SourceText(_battleMenuFont, new(168,138), "which?");
        return true;
    }
}
