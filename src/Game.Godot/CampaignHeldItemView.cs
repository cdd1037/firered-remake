using Game.Application;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private ItemId? _heldGiveItem;
    private bool _heldGivePc, _heldExchange;
    private int _heldTarget, _heldChoice;
    private long[] _heldTargetIds = [];
    private bool HeldItemScreen => _session.State.ItemMenu is { Origin: "party", View: "held" } || _heldGiveItem is not null;
    private int ItemActionCount => ItemActionLabels().Length;
    private int ItemActionY => _session.State.ItemMenu?.Origin == "bag" ? 154 - ItemActionCount * 16 : ItemActionCount == 3 ? 106 : 130;
    private string[] ItemActionLabels()
    {
        var menu = _session.State.ItemMenu!;
        if (menu.View == "withdraw") return ["WITHDRAW", "GIVE", "CANCEL"];
        if (menu.View == "deposit") return ["DEPOSIT", "CANCEL"];
        bool holdable = _itemCursor < FieldItemStacks.Length && _session.CanGiveHeldItem(FieldItemStacks[_itemCursor].Item);
        if (menu.TargetInstanceId is not null) return holdable ? ["GIVE", "CANCEL"] : ["CANCEL"];
        // Field action tables belong to the pocket, not an item's CanToss flag.
        // Key Items and TM CASE use their own menus and never enter this branch.
        if (menu.Origin == "bag" && menu.View is "bag" or "berries")
            return [menu.View == "bag" && _itemCursor < FieldItemStacks.Length && _session.CanGiveMail(FieldItemStacks[_itemCursor].Item) ? "CHECK" : "USE",
                "GIVE", "TOSS", menu.View == "berries" ? "EXIT" : "CANCEL"];
        return holdable ? ["USE", "GIVE", "CANCEL"] : ["USE", "CANCEL"];
    }
    private void OpenPartyHeldItems()
    {
        int target = _fieldPartyCursor;
        var id = _session.State.Party[target].InstanceId;
        bool accepted = Apply(new("party_item_open", PartyIndex: target) { TargetInstanceId = id });
        if (accepted) ResetFieldMenu();
        else _fieldMenuNotice = HeldItemNotice(_status);
    }
    private void StartGiveItem(ItemId item, bool pc = false)
    {
        _itemPopup = false; _questPocketPopup = false;
        if (!_session.CanGiveHeldItem(item)) { _itemNotice = "This item can't be held."; return; }
        if (_session.State.Party.IsEmpty) { _itemNotice = "There is no POKéMON."; return; }
        _heldGiveItem = item; _heldGivePc = pc; _heldExchange = false; _heldChoice = 0; _heldTarget = 0;
        _heldTargetIds = _session.State.Party.Select(p => p.InstanceId).ToArray();
        if (_session.State.ItemMenu?.TargetPartyIndex is {} target)
        { _heldTarget = target; ConfirmHeldTarget(); }
    }
    private bool HeldItemInput(InputEventKey key)
    {
        if (!HeldItemScreen) return false;
        if (!key.Pressed || key.Echo) return true;
        if (_heldGiveItem is not null)
        {
            if (key.Keycode == Key.Backspace) { _heldGiveItem = null; _heldExchange = false; return true; }
            if (_heldExchange)
            {
                FieldMenuMoveCursor(key, ref _heldChoice, 2);
                if (key.Keycode == Key.Enter)
                { if (_heldChoice == 0) CompleteGiveItem(true); else { _heldGiveItem = null; _heldExchange = false; } }
                return true;
            }
            FieldMenuMoveCursor(key, ref _heldTarget, _heldTargetIds.Length + 1);
            if (key.Keycode == Key.Enter)
            { if (_heldTarget == _heldTargetIds.Length) _heldGiveItem = null; else ConfirmHeldTarget(); }
            return true;
        }
        if (key.Keycode == Key.Backspace) { Apply(new("menu_back")); return true; }
        FieldMenuMoveCursor(key, ref _heldChoice, 3);
        if (key.Keycode != Key.Enter) return true;
        var menu = _session.State.ItemMenu!;
        if (_heldChoice == 2) { Apply(new("menu_back")); return true; }
        int target = menu.TargetPartyIndex!.Value;
        string previous = _session.State.Party[target].HeldItem;
        bool accepted = Apply(new(_heldChoice == 0 ? "held_bag_open" : "field_take", PartyIndex: target) { TargetInstanceId = menu.TargetInstanceId });
        if (!accepted) _itemNotice = HeldItemNotice(_status);
        else if (_heldChoice == 1) _itemNotice = "Received " + InventoryName(new("pret:" + previous)) + ".";
        return true;
    }
    private void ConfirmHeldTarget()
    {
        if (_heldTarget >= _session.State.Party.Length || _session.State.Party[_heldTarget].InstanceId != _heldTargetIds[_heldTarget])
        { _heldGiveItem = null; _itemNotice = "The selected POKéMON changed."; return; }
        if (_session.State.Party[_heldTarget].HeldItem != "ITEM_NONE") { _heldExchange = true; _heldChoice = 0; }
        else CompleteGiveItem(false);
    }
    private void CompleteGiveItem(bool exchange)
    {
        var item = _heldGiveItem!.Value;
        var command = new CampaignCommand(_heldGivePc ? "pc_give" : "field_give", Text: item.Value,
            Choice: exchange ? 1 : null, PartyIndex: _heldTarget) { TargetInstanceId = _heldTargetIds[_heldTarget] };
        _heldGiveItem = null; _heldExchange = false;
        bool accepted = Apply(command);
        if (MachineActive) _machineCursor = Math.Min(_machineCursor, MachineStacks.Length);
        _itemCursor = Math.Min(_itemCursor, (_heldGivePc ? _session.State.Inventory!.PcItems : FieldItemStacks).Length);
        _questPocketCursor = Math.Min(_questPocketCursor, QuestPocketStacks.Length);
        if (_session.State.MailComposer is not null) { _itemNotice = null; return; }
        _itemNotice = !accepted ? HeldItemNotice(_status) : "Gave " + InventoryName(item) + "\nto " + _session.State.Party[_heldTarget].Nickname + ".";
    }
    private static string HeldItemNotice(string status) => status switch {
        "held_item_none" => "It isn't holding anything.",
        "inventory_destination_full" or "inventory_stack_full" or "inventory_key_items_full" => "The BAG is full. The item\ncouldn't be taken.",
        "held_item_target_stale" or "item_party_index_invalid" => "The selected POKéMON changed.",
        "held_item_not_holdable" => "This item can't be held.",
        "held_item_mail_or_egg_unsupported" => "Mail and Eggs aren't supported\nfor item transfers here.",
        _ => "The item couldn't be transferred." };
    private bool DrawHeldItemScreen()
    {
        if (!HeldItemScreen) return false;
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        if (_heldGiveItem is not null && !_heldExchange)
        {
            for (int i = 0; i < _session.State.Party.Length; i++)
            { SourceText(_sourceNormal, new(24, 8 + i * 17), PresentedOwnedName(_session.State.Party[i])); }
            SourceText(_sourceNormal, new(24, 8 + _session.State.Party.Length * 17), "CANCEL");
            SourceText(_sourceNormal, new(12, 8 + _heldTarget * 17), "▶");
            ItemMessageBox("Give to which POKéMON?");
        }
        else
        {
            int index = _heldGiveItem is not null ? _heldTarget : _session.State.ItemMenu!.TargetPartyIndex!.Value;
            var owned = _session.State.Party[index];
            SourceText(_sourceNormal, new(12, 10), owned.Nickname);
            SourceText(_sourceSmall, new(12, 32), "HELD ITEM / " + (owned.HeldItem == "ITEM_NONE" ? "NONE" : InventoryName(new("pret:" + owned.HeldItem))));
            string[] options = _heldExchange ? ["YES", "NO"] : ["GIVE", "TAKE", "CANCEL"];
            for (int i = 0; i < options.Length; i++) SourceText(_sourceNormal, new(176, 58 + i * 17), options[i]);
            SourceText(_sourceNormal, new(164, 58 + _heldChoice * 17), "▶");
            if (_heldExchange) ItemMessageBox("Switch its held item for\n" + InventoryName(_heldGiveItem!.Value) + "?");
        }
        if (_itemNotice is not null) ItemMessageBox(_itemNotice);
        return true;
    }
    private bool HeldItemMouse(Vector2 point, int? direction = null)
    {
        if (_itemNotice is not null) return direction is null && MouseConfirm(point, MouseMessage);
        bool targets = _heldGiveItem is not null && !_heldExchange;
        int count = targets ? _heldTargetIds.Length + 1 : _heldExchange ? 2 : 3;
        if (targets) return direction is {} d ? PointerRows(point, d, ref _heldTarget, count, 12, 8, 216, 17)
            : MouseSelect(point, ref _heldTarget, count, 12, 8, 216, 17);
        return direction is {} move ? PointerRows(point, move, ref _heldChoice, count, 164, 58, 68, 17)
            : MouseSelect(point, ref _heldChoice, count, 164, 58, 68, 17);
    }
}
