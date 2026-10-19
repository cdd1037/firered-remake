using System.Collections.Immutable;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private int _inventoryMoveSource = -1;
    private string? _inventoryMoveItem, _inventoryMoveContext;
    private InventoryState? _inventoryMoveInventory;
    private void ResetInventoryMove()
    { _inventoryMoveSource = -1; _inventoryMoveItem = null; _inventoryMoveContext = null; _inventoryMoveInventory = null; }

    private bool InventoryMoveInput(InputEventKey key, ImmutableArray<InventoryStack> stacks, ref int cursor)
    {
        var menu = _session.State.ItemMenu;
        bool enabled = menu is { Origin: "bag", View: "bag", TargetInstanceId: null, Toss: null }
            or { Origin: "pc", View: "withdraw", Toss: null };
        string context = $"{menu?.Origin}:{menu?.View}:{menu?.Pocket}";
        if (_inventoryMoveSource >= 0 && (!enabled || _inventoryMoveContext != context || !ReferenceEquals(_inventoryMoveInventory, _session.State.Inventory)))
            ResetInventoryMove();
        if (!enabled || !key.Pressed || key.Echo) return false;
        if (_inventoryMoveSource < 0)
        {
            if (key.Keycode != Key.Space || cursor >= stacks.Length) return false;
            _inventoryMoveSource = cursor; _inventoryMoveItem = stacks[cursor].Item.Value;
            _inventoryMoveContext = context; _inventoryMoveInventory = _session.State.Inventory;
            return true;
        }
        if (key.Keycode == Key.Backspace)
        { if (_inventoryMoveSource < cursor) cursor--; ResetInventoryMove(); return true; }
        if (key.Keycode == Key.Up) cursor = Math.Max(0, cursor - 1);
        if (key.Keycode == Key.Down) cursor = Math.Min(stacks.Length, cursor + 1);
        if (key.Keycode is Key.Enter or Key.Space)
        {
            int destination = cursor, source = _inventoryMoveSource;
            string item = _inventoryMoveItem!;
            ResetInventoryMove();
            if (Apply(new("item_move", Text: item, Choice: source, Quantity: destination)))
                cursor = source < destination ? destination - 1 : destination;
            else _itemNotice = _inventoryText.Get("notice.order-failed");
        }
        return true;
    }

    private void DrawInventoryMove()
    {
        if (_inventoryMoveSource < 0 || _inventoryMoveItem is null) return;
        // item_pc.c and item_menu.c share gOtherText_WhereShouldTheStrVar1BePlaced.
        ItemMessageBox(InventoryMenuPresentation.BindSource(InventorySourceText("gOtherText_WhereShouldTheStrVar1BePlaced"), InventoryName(new(_inventoryMoveItem))));
    }
}
