using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Godot;

// MOVE ITEMS holds only an Application-owned item. Pokemon never leave their
// cells. Local state is limited to menus, display cursors and identity snapshots.
public partial class CampaignView
{
    private string[] _storageItemActions = [];
    private int _storageItemChoice, _storageItemBagCursor, _storageItemPromptChoice;
    private CampaignStorageItemBagTarget? _storageItemBagTarget;
    private bool StorageItemBagOpen => _session.State.Pc?.ItemBagTarget is not null;
    private ItemId? _storageItemInfo;
    private PokemonStorageMoveLocation? _storageItemLocation;
    private long? _storageItemTargetId;
    private string? _storageItemTargetHeld, _storageItemExpectedCursor;
    private CampaignStorageItemPrompt? _storageItemPrompt;
    private InventoryStack[] _storageItemBagStacks = [];
    private int StorageItemBagStart => Math.Max(0, _storageItemBagCursor - 4);
    private PokemonStorageMoveLocation StorageItemLocation => _storageMovePartyOpen
        ? PokemonStorageMoveLocation.Party(_storageMovePartyCursor)
        : PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, _boxCursor);

    private void ResetStorageItemPresentation()
    {
        _storageItemActions = []; _storageItemChoice = _storageItemBagCursor = _storageItemPromptChoice = 0;
        _storageItemBagTarget = null; _storageItemInfo = null; _storageItemLocation = null;
        _storageItemTargetId = null; _storageItemTargetHeld = _storageItemExpectedCursor = null;
        _storageItemPrompt = null; _storageItemBagStacks = [];
    }

    private void SynchronizeStorageItemPrompt()
    {
        var prompt = _session.State.Pc?.ItemPrompt;
        var bag = _session.State.Pc?.ItemBagTarget;
        if (_storageItemBagTarget != bag)
        {
            _storageItemBagTarget = bag; _storageItemBagCursor = 0;
            if (bag is not null)
            {
                var inventory = _session.State.Inventory!;
                _storageItemBagStacks = inventory.BagItems.Concat(inventory.Balls ?? []).Concat(inventory.Berries ?? [])
                    .Concat(inventory.Machines ?? []).Where(s => _session.CanGiveHeldItem(s.Item) && !_session.CanGiveMail(s.Item)).ToArray();
                _storageItemLocation = bag.Location; _storageItemTargetId = bag.InstanceId;
                _storageItemTargetHeld = bag.HeldItemId["pret:".Length..]; _storageItemExpectedCursor = null;
            }
            else _storageItemBagStacks = [];
        }
        if (_storageItemPrompt == prompt) return;
        _storageItemPrompt = prompt; _storageItemPromptChoice = 0;
        if (prompt is not null) { _storageItemActions = []; _storageItemInfo = null; }
    }

    private bool StorageItemAction(CampaignCommand command, string? success = null)
    {
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return false; }
        bool accepted = Apply(command);
        if (accepted) { if (success is not null) _pcNotice = success; return true; }
        _pcNotice = _status.Contains("full", StringComparison.Ordinal) ? PcText("gText_BagIsFull2")
            : _status.Contains("identity", StringComparison.Ordinal) || _status.Contains("stale", StringComparison.Ordinal)
                ? "The selected POKéMON or item changed.\nPlease choose again."
            : _status.Contains("mail", StringComparison.Ordinal) ? PcText("gText_MailCantBeStored")
            : _status.Contains("egg", StringComparison.Ordinal) ? "An EGG can't hold an item."
            : _status.Contains("not_holdable", StringComparison.Ordinal) ? "This item can't be held."
            : "The item couldn't be transferred.\nPlease choose again.";
        return false;
    }

    private void ToggleStorageItemParty()
    {
        _storageTitleSelected = false;
        _storageMovePartyOpen = !_storageMovePartyOpen;
        if (_storageMovePartyOpen) { _storageMovePartyCursor = 0; _storageMovePreviousPartyCursor = 1; }
    }

    private void OpenStorageItemActions()
    {
        if (_storageMovePartyOpen && _storageMovePartyCursor == 6) { ToggleStorageItemParty(); return; }
        var target = StorageMoveTarget;
        if (target is null || target.IsEgg) return;
        bool mail = target.Mail != 255 || SourceMailRules.IsMailItem(target.HeldItem);
        var cursor = _session.State.Pc!.ItemCursor;
        // Original SetMenuTexts: no transfer options for Mail, no menu on Eggs.
        _storageItemActions = cursor is not null
            ? mail ? [] : target.HeldItem == "ITEM_NONE" ? ["Give", "Cancel"] : ["Switch", "Cancel"]
            : target.HeldItem == "ITEM_NONE" ? ["Give", "Cancel"]
            : mail ? ["Info", "Cancel"] : ["Take", "Bag", "Info", "Cancel"];
        _storageItemChoice = 0; _storageItemLocation = StorageItemLocation;
        _storageItemTargetId = target.InstanceId; _storageItemTargetHeld = target.HeldItem;
        _storageItemExpectedCursor = cursor?.Item.Value;
    }

    private bool StorageItemSelectionIsCurrent()
    {
        var target = StorageMoveTarget;
        if (_storageItemLocation == StorageItemLocation && target?.InstanceId == _storageItemTargetId &&
            target?.HeldItem == _storageItemTargetHeld && _session.State.Pc!.ItemCursor?.Item.Value == _storageItemExpectedCursor) return true;
        _storageItemActions = [];
        _pcNotice = "The selected POKéMON or item changed.\nPlease choose again."; return false;
    }

    private CampaignCommand StorageItemCommand(string kind, string? text = null) => new(kind, Text: text)
    {
        StorageLocation = _storageItemLocation, TargetInstanceId = _storageItemTargetId,
        HeldItemId = _storageItemExpectedCursor, TargetHeldItemId = "pret:" + _storageItemTargetHeld
    };

    private void ConfirmStorageItemAction()
    {
        string action = _storageItemActions[_storageItemChoice]; _storageItemActions = [];
        if (action == "Cancel" || !StorageItemSelectionIsCurrent()) return;
        if (action == "Info") { _storageItemInfo = new("pret:" + _storageItemTargetHeld); return; }
        if (action == "Give" && _storageItemExpectedCursor is null)
        {
            StorageItemAction(StorageItemCommand("storage_item_bag_open")); return;
        }
        string? notice = action == "Bag" ? PcText("gText_PlacedItemInBag")
            : action == "Give" ? PcText("gText_ItemIsNowHeld", StorageItemName(new(_storageItemExpectedCursor!)))
            : action == "Switch" ? PcText("gText_ChangedToNewItem", StorageItemName(new(_storageItemExpectedCursor!))) : null;
        StorageItemAction(StorageItemCommand(action switch
        { "Take" => "storage_item_take", "Bag" => "storage_item_to_bag", "Give" => "storage_item_give", _ => "storage_item_switch" }), notice);
    }

    private bool StorageItemInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return true;
        _held = null; _repeat = 0;
        bool back = key.Keycode is Key.Backspace or Key.B;
        if (_pcNotice is not null) { if (back || key.Keycode == Key.Enter) _pcNotice = null; return true; }
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return true; }
        if (_storageItemInfo is not null)
        { if (back || key.Keycode is Key.Enter or Key.Up or Key.Down or Key.Left or Key.Right) _storageItemInfo = null; return true; }
        if (_session.State.Pc!.ItemPrompt is not null)
        {
            if (key.Keycode is Key.Up or Key.Down) _storageItemPromptChoice ^= 1;
            if (back || key.Keycode == Key.Enter)
                StorageItemAction(new("storage_item_prompt", Choice: back ? 127 : _storageItemPromptChoice)
                    { HeldItemId = _session.State.Pc.ItemCursor?.Item.Value });
            return true;
        }
        if (_storageItemActions.Length != 0)
        {
            if (back) _storageItemActions = [];
            else { FieldMenuMoveCursor(key, ref _storageItemChoice, _storageItemActions.Length); if (key.Keycode == Key.Enter) ConfirmStorageItemAction(); }
            return true;
        }
        if (StorageItemBagOpen)
        {
            if (back) { StorageItemAction(new("storage_item_bag_cancel")); return true; }
            FieldMenuMoveCursor(key, ref _storageItemBagCursor, _storageItemBagStacks.Length + 1);
            if (key.Keycode == Key.Enter)
            {
                if (_storageItemBagCursor == _storageItemBagStacks.Length) { StorageItemAction(new("storage_item_bag_cancel")); return true; }
                if (StorageItemSelectionIsCurrent())
                {
                    var item = _storageItemBagStacks[_storageItemBagCursor].Item;
                    StorageItemAction(StorageItemCommand("storage_item_from_bag", item.Value), PcText("gText_ItemIsNowHeld", StorageItemName(item)));
                }
            }
            return true;
        }
        if (back) { if (_storageMovePartyOpen) ToggleStorageItemParty(); else StorageItemAction(new("pc_back")); return true; }
        if (key.Keycode == Key.Tab) { ToggleStorageItemParty(); return true; }
        if (key.Keycode is Key.Pageup or Key.Pagedown)
        { StorageItemAction(new("storage_box", Choice: (_session.State.Storage!.CurrentBox + (key.Keycode == Key.Pagedown ? 1 : 13)) % 14)); return true; }
        NavigateStorageWorkspace(key, ToggleStorageItemParty);
        if (key.Keycode == Key.Enter) OpenStorageItemActions();
        return true;
    }

    private bool MouseStorageItems(Vector2 point)
    {
        if (_pcNotice is not null) return MouseConfirm(point, MouseMessage);
        if (_storageItemInfo is not null) return MouseKey();
        if (_session.State.Pc!.ItemPrompt is not null) return MouseSelect(point, ref _storageItemPromptChoice, 2, 176, 72, 64);
        if (_storageItemActions.Length != 0) return MouseSelect(point, ref _storageItemChoice, _storageItemActions.Length, 168, 40, 72);
        if (StorageItemBagOpen)
            return MouseSelect(point, ref _storageItemBagCursor, Math.Min(5, _storageItemBagStacks.Length + 1 - StorageItemBagStart), 8, 32, 224, 16, StorageItemBagStart);
        if (!_storageMovePartyOpen && MouseStorageBoxTitle(point)) return true;
        if (StorageMoveCloseButton.HasPoint(point))
        { _menuMouseActionFrame = Engine.GetProcessFrames(); StorageItemAction(new("storage_item_close")); return true; }
        // Shared source cell geometry; synthetic keys return through MOVE ITEMS.
        if (_storageMovePartyOpen)
        {
            if (StorageMovePartyCancel.HasPoint(point)) { _storageMovePartyCursor = 6; return MouseKey(); }
            for (int i = 0; i < 6; i++) if (StorageMovePartyRect(i).HasPoint(point)) { _storageMovePartyCursor = i; return MouseKey(); }
            return false;
        }
        if (StorageMovePartyButton.HasPoint(point) || new Rect2(0, 140, 80, 20).HasPoint(point)) return MouseKey(Key.Tab);
        if (new Rect2(84, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pageup);
        if (new Rect2(216, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pagedown);
        if (!new Rect2(88, 32, 144, 120).HasPoint(point)) return false;
        _storageTitleSelected = false; _boxCursor = (int)((point.X - 88) / 24) + 6 * (int)((point.Y - 32) / 24); return MouseKey();
    }

    private bool NavigateStorageItemPointer(Vector2 point, int direction)
    {
        if (_pcNotice is not null || _storageItemInfo is not null) return false;
        if (_session.State.Pc!.ItemPrompt is not null) return PointerRows(point, direction, ref _storageItemPromptChoice, 2, 176, 72, 64);
        if (_storageItemActions.Length != 0) return PointerRows(point, direction, ref _storageItemChoice, _storageItemActions.Length, 168, 40, 72);
        if (StorageItemBagOpen) return PointerRows(point, direction, ref _storageItemBagCursor,
            Math.Min(5, _storageItemBagStacks.Length + 1 - StorageItemBagStart), 8, 32, 224, 16, StorageItemBagStart);
        return NavigateStorageMovePointer(point, direction);
    }

    private void DrawStorageItemIcon(ItemId item, Rect2 rect)
    {
        Texture2D? icon = _earlyItemIcons.GetValueOrDefault(item);
        if (icon is null && _catalog.PewterMachines is { } machines && PewterMachineRules.Definition(machines, item) is not null)
            icon = _questPocketTextures.GetValueOrDefault(MachineIcon);
        if (icon is not null) DrawTextureRect(icon, rect, false);
    }

    private string StorageItemName(ItemId item) =>
        (_catalog.PewterMachines is { } machines ? PewterMachineRules.Definition(machines, item)?.Machine.Item.Name : null)
        ?? InventoryName(item);

    private string StorageItemDescription(ItemId item) =>
        (_catalog.PewterMachines is { } machines ? PewterMachineRules.Definition(machines, item)?.Machine.Description : null)
        ?? EarlyItem(item)?.Description.Replace("\\n", "\n", StringComparison.Ordinal).TrimEnd('$')
        ?? _questPocketUi?.Items.SingleOrDefault(i => i.Id == item.Value)?.Description
        ?? "No item information is available.";

    private void DrawStorageItems()
    {
        var held = _session.State.Pc!.ItemCursor?.Item;
        var target = StorageMoveTarget;
        Rect2 cursor = DrawStorageWorkspace();
        if (!_storageTitleSelected) DrawRect(cursor, new Color(held is null ? "f4db88" : "fff4b8"), filled: false, width: 1);
        if (held is { } item)
        {
            var marker = new Rect2(cursor.Position + new Vector2(cursor.Size.X - 12, -7), new(16, 16));
            DrawRect(marker, new Color("f4db88")); DrawStorageItemIcon(item, marker);
        }
        DrawRect(new(4, 1, 72, 13), new Color("b8b8c0"));
        SourceText(_sourceSmall, new(6, 2), held is null ? PcText("gText_MoveItems") : "HOLDING ITEM");
        DrawStorageSelectedPokemon(target);
        string selected = held is { } heldItem ? StorageItemName(heldItem)
            : target is { HeldItem: not "ITEM_NONE" } ? StorageItemName(new("pret:" + target.HeldItem)) : "NO ITEM";
        SourceText(_sourceSmall, new(6, 132), selected);
        if (StorageItemBagOpen)
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
            SourceText(_sourceNormal, new(12, 8), "BAG / GIVE ITEM");
            int start = StorageItemBagStart;
            for (int i = start; i < Math.Min(start + 5, _storageItemBagStacks.Length + 1); i++)
            {
                string label = i == _storageItemBagStacks.Length ? PcText("gPCText_Cancel") : StorageItemName(_storageItemBagStacks[i].Item);
                SourceText(_sourceNormal, new(24, 32 + (i - start) * 16), label);
                if (i < _storageItemBagStacks.Length) SourceText(_sourceSmall, new(200, 34 + (i - start) * 16), "×" + _storageItemBagStacks[i].Quantity);
            }
            SourceText(_sourceNormal, new(12, 32 + (_storageItemBagCursor - start) * 16), "▶");
            if (_storageItemBagCursor < _storageItemBagStacks.Length)
            {
                var selectedItem = _storageItemBagStacks[_storageItemBagCursor].Item;
                DrawStorageItemIcon(selectedItem, new(8, 120, 24, 24));
                ItemLines(_sourceSmall, new(40, 116), StorageItemDescription(selectedItem), 13);
            }
        }
        if (_storageItemInfo is { } info)
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
            DrawStorageItemIcon(info, new(12, 12, 24, 24)); SourceText(_sourceNormal, new(44, 16), StorageItemName(info));
            ItemLines(_sourceNormal, new(12, 48), StorageItemDescription(info), 16);
            SourceText(_sourceSmall, new(12, 140), "ENTER / B: BACK");
        }
        if (_storageItemActions.Length != 0)
        {
            _battleSkin.DrawUserFrame(this, new(168, 32, 72, _storageItemActions.Length * 16 + 16));
            for (int i = 0; i < _storageItemActions.Length; i++) SourceText(_sourceNormal, new(184, 40 + i * 16), PcText("gPCText_" + _storageItemActions[i]));
            SourceText(_sourceNormal, new(172, 40 + _storageItemChoice * 16), "▶");
        }
        if (_session.State.Pc.ItemPrompt is { } prompt)
        {
            ItemMessageBox(PcText(prompt switch { CampaignStorageItemPrompt.PutInBag => "gText_PutItemInBag",
                CampaignStorageItemPrompt.ContinueOperations => "gText_ContinueBoxOperations", _ => "gText_ExitFromBox" }));
            _battleSkin.DrawUserFrame(this, new(176, 64, 64, 48));
            SourceText(_sourceNormal, new(192, 72), "YES"); SourceText(_sourceNormal, new(192, 88), "NO");
            SourceText(_sourceNormal, new(180, 72 + _storageItemPromptChoice * 16), "▶");
        }
        DrawStorageCustomizationOverlay();
        if (_pcNotice is not null) ItemMessageBox(_pcNotice);
    }
}
