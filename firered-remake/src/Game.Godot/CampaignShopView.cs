using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<ItemId, ImageTexture> _earlyItemIcons = [];
    private int _shopCursor, _shopQuantity = 1, _shopConfirm, _fieldMedicineTarget;
    private string? _shopKey, _shopNotice;
    private bool _fieldMedicineTargetOpen;
    private void LoadShopPresentation()
    {
        if (_catalog.EarlyShopDocument is not { } document) return;
        foreach (var icon in document.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretEarlyShop.IconRgba(icon));
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
        if (_catalog.CeruleanEnabled)
        {
            var icon = PretCeruleanItems.LoadProject(_root).Icon;
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in _catalog.NpcTradeElyssa.Items.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8,
                _catalog.NpcTradeElyssa.IconFiles[icon.RgbaPath]);
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
        LoadPokeDollIcon();
        LoadOrdinarySupplementIcons();
        if (OrdinaryItemInventoryCatalog.For(_catalog.EarlyShop)?.IsAddition(OrdinaryItemInventoryCatalog.RetroMailId) == true)
        {
            using var retroMail = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, PretDepartmentFourthFloor.LoadIconRgba(_root));
            _earlyItemIcons.Add(OrdinaryItemInventoryCatalog.RetroMailId, ImageTexture.CreateFromImage(retroMail));
        }
        foreach (var icon in _catalog.NpcTradeMail.Items.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8,
                _catalog.NpcTradeMail.AssetFiles[icon.RgbaPath]);
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
    }
    private void LoadOrdinarySupplementIcons()
    {
        if (OrdinaryItemInventoryCatalog.For(_catalog.EarlyShop) is null) return;
        foreach (var icon in PretOrdinaryItemSupplement.LoadDocument(_root).Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8,
                PretOrdinaryItemSupplement.LoadIconRgba(_root));
            _earlyItemIcons.Add(icon.Item, ImageTexture.CreateFromImage(image));
        }
    }
    private void LoadPokeDollIcon()
    {
        if (_catalog.PokeDollShopStock is null) return;
        using var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, PretPokeDollItems.LoadIconRgba(_root));
        _earlyItemIcons.Add(PokeDollInventoryCatalog.PokeDollId, ImageTexture.CreateFromImage(image));
    }
    private EarlyShopItemDefinition? EarlyItem(ItemId id) => _catalog.EarlyShop?.Items.SingleOrDefault(i => i.Id == id);
    private FieldMedicineEffect FieldMedicineEffectFor(ItemId id) => _catalog.EarlyShop is {} shop
        ? AuthenticatedFieldMedicine.Resolve(shop, id).Effect : FieldMedicineEffect.Unsupported;
    private bool CanSelectFieldMedicine(ItemId id) => FieldMedicineEffectFor(id) is
        FieldMedicineEffect.HealHp or FieldMedicineEffect.CureStatus or FieldMedicineEffect.CureSleep ||
        _catalog.EarlyShop is {} shop && _catalog.PresetDoubleItems is {} sourceItems && (SourceFieldStatusCureRules.Supports(shop, sourceItems, id) || SourceFieldHpMedicineRules.Supports(shop, sourceItems, id));
    private string InventoryName(ItemId id) => EarlyItem(id)?.Name ??
        (_catalog.PewterMachines is {} machines ? PewterMachineRules.Definition(machines, id)?.Machine.Item.Name : null) ??
        (id == OpeningInventoryRules.PotionId ? _itemSkin.PotionName : _catalog.QuestItems?.Items.SingleOrDefault(i => i.Id == id)?.Name ?? throw new InvalidDataException("inventory_item_name_missing"));
    private string InventoryDescription(ItemId id) => EarlyItem(id)?.Description.Replace("\\n", "\n", StringComparison.Ordinal).TrimEnd('$') ?? _itemSkin.PotionDescription;
    private void DrawInventoryIcon(ItemId id)
    {
        if (_earlyItemIcons.TryGetValue(id, out var icon)) DrawTexture(icon, new(8, 112));
        else if (id == OpeningInventoryRules.PotionId) _itemSkin.DrawPotion(this);
    }
    private void SynchronizeShop()
    {
        var shop = _session.State.Shop;
        string? key = shop is null ? null : shop.ShopId + ":" + shop.Stage + ":" + shop.Mode;
        if (key != _shopKey) { _shopKey = key; _shopCursor = 0; _shopQuantity = 1; _shopConfirm = 0; _shopNotice = null; }
        if (shop is not null) _held = null;
    }
    private bool ShopInput(InputEventKey key)
    {
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed) return true;
        var shop = _session.State.Shop!;
        if (_shopNotice is not null) { if (key.Keycode is Key.Enter or Key.Backspace) _shopNotice = null; return true; }
        if (key.Keycode == Key.Backspace) { Apply(new("shop_back")); return true; }
        if (shop.Stage == CampaignShopStage.Message) { if (key.Keycode == Key.Enter) Apply(new("shop_ack")); return true; }
        if (shop.Stage == CampaignShopStage.Quantity)
        {
            int maximum = _session.CurrentShopQuote!.MaximumQuantity;
            if (key.Keycode is Key.Up or Key.Right) _shopQuantity = Math.Min(maximum, _shopQuantity + (key.Keycode == Key.Right ? 10 : 1));
            if (key.Keycode is Key.Down or Key.Left) _shopQuantity = Math.Max(1, _shopQuantity - (key.Keycode == Key.Left ? 10 : 1));
            if (key.Keycode == Key.Enter) Apply(new("shop_quantity", Quantity: _shopQuantity));
            return true;
        }
        if (shop.Stage == CampaignShopStage.Confirmation)
        {
            if (key.Keycode is Key.Up or Key.Down) _shopConfirm ^= 1;
            if (key.Keycode == Key.Enter) Apply(new("shop_confirm", Choice: _shopConfirm));
            return true;
        }
        int last = shop.Stage == CampaignShopStage.Menu ? 2 : _session.VisibleShopItems.Length;
        if (key.Keycode == Key.Up) _shopCursor = Math.Max(0, _shopCursor - 1);
        if (key.Keycode == Key.Down) _shopCursor = Math.Min(last, _shopCursor + 1);
        if (key.Keycode == Key.Enter)
        {
            if (shop.Stage == CampaignShopStage.Menu) Apply(new("shop_menu", Text: new[] { "buy", "sell", "cancel" }[_shopCursor]));
            else if (_shopCursor == last) Apply(new("shop_back"));
            else Apply(new("shop_select", Text: _session.VisibleShopItems[_shopCursor].Value));
        }
        return true;
    }
    private void DrawShop()
    {
        var shop = _session.State.Shop!;
        var stage = shop.Stage == CampaignShopStage.Message ? shop.ReturnStage : shop.Stage;
        _battleSkin.DrawStandardFrame(this, new(0, 0, 88, 32));
        ItemLabel(new(8, 9), "¥" + _session.State.Money);
        if (stage == CampaignShopStage.Menu)
        {
            _battleSkin.DrawStandardFrame(this, new(144, 0, 96, 64));
            string[] choices = ["gText_ShopBuy", "gText_ShopSell", "gText_ShopQuit"];
            for (int i = 0; i < 3; i++) ItemLabel(new(160, 8 + i * 16), _session.ShopText(choices[i]));
            ItemLabel(new(150, 8 + _shopCursor * 16), "▶");
            ItemMessageBox(shop.MessageSymbol is null && _session.ShopAvailabilityNotice is {} notice ? notice : _session.ShopText(shop.MessageSymbol ?? "Text_MayIHelpYou"));
            return;
        }
        _battleSkin.DrawStandardFrame(this, new(80, 0, 160, 112));
        var items = _session.VisibleShopItems;
        int selected = shop.Item is { } item && stage is CampaignShopStage.Quantity or CampaignShopStage.Confirmation ? items.IndexOf(item) : _shopCursor;
        selected = Math.Clamp(selected, 0, items.Length);
        int start = Math.Max(0, selected - 5);
        for (int i = start; i <= Math.Min(items.Length, start + 5); i++)
        {
            int row = i - start;
            if (i == items.Length) { ItemLabel(new(96, 8 + row * 16), _itemSkin.Text("gFameCheckerText_Cancel")); continue; }
            var definition = _session.ShopItem(items[i]);
            ItemLabel(new(96, 8 + row * 16), definition.Name);
            ItemLabel(new(197, 8 + row * 16), "¥" + (shop.Mode == "buy" ? definition.Price : definition.Price / 2), 8);
        }
        ItemLabel(new(85, 8 + (selected - start) * 16), "▶");
        if (selected < items.Length)
        {
            var definition = _session.ShopItem(items[selected]);
            ItemLabel(new(7, 43), "IN BAG: " + definition.Quantity, 8);
            if (definition.Move is { } move) ItemLabel(new(7, 65), MoveName(move), 8);
            else if (_earlyItemIcons.TryGetValue(definition.Id, out var icon)) DrawTexture(icon, new(24, 65));
        }
        if (shop.Stage == CampaignShopStage.Message) ItemMessageBox(_session.ShopText(shop.MessageSymbol!));
        else if (stage == CampaignShopStage.Quantity)
        {
            ItemMessageBox(_session.ShopText(shop.Mode == "buy" ? "gText_Var1CertainlyHowMany" : "gText_HowManyWouldYouLikeToSell"));
            _battleSkin.DrawStandardFrame(this, new(0, 80, 80, 32));
            ItemLabel(new(12, 90), "× " + _shopQuantity);
        }
        else if (stage == CampaignShopStage.Confirmation)
        {
            ItemMessageBox(_session.ShopText(shop.Mode == "buy" ? "gText_Var1AndYouWantedVar2" : "gText_ICanPayThisMuch_WouldThatBeOkay"));
            _battleSkin.DrawStandardFrame(this, new(0, 64, 64, 48));
            ItemLabel(new(20, 73), "YES"); ItemLabel(new(20, 89), "NO"); ItemLabel(new(8, 73 + _shopConfirm * 16), "▶");
        }
        else ItemMessageBox(selected < items.Length ? _session.ShopItem(items[selected]).Description.Replace("\\n", "\n", StringComparison.Ordinal).TrimEnd('$') : _session.ShopText("gText_QuitShopping"));
    }
    private bool FieldMedicineTargetInput(InputEventKey key)
    {
        int cancel = _session.State.Party.Length;
        if (key.Keycode == Key.Up) _fieldMedicineTarget = Math.Max(0, _fieldMedicineTarget - 1);
        if (key.Keycode == Key.Down) _fieldMedicineTarget = Math.Min(cancel, _fieldMedicineTarget + 1);
        if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _fieldMedicineTarget == cancel) { _fieldMedicineTargetOpen = false; return true; }
        if (key.Keycode == Key.Enter)
        {
            var item = FieldItemStacks[_itemCursor].Item;
            var revision = _session.Revision;
            Apply(new("field_medicine", Text: item.Value, PartyIndex: _fieldMedicineTarget));
            _fieldMedicineTargetOpen = false;
            if (_session.Revision == revision) _itemNotice = _status == "medicine_effect_unsupported" ? "This item's use is not\nimplemented yet." : _status;
            else if (_session.State.ItemMenu?.View != "message") _itemNotice = InventoryName(item) + " was used.";
            _itemCursor = Math.Min(_itemCursor, FieldItemStacks.Length);
        }
        return true;
    }
    private void DrawFieldMedicineTargets()
    {
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
        for (int i = 0; i <= _session.State.Party.Length; i++)
        {
            if (i == _session.State.Party.Length)
            {
                ItemLabel(new(20, 8 + 14 * i), _itemSkin.Text("gFameCheckerText_Cancel"));
                continue;
            }
            var member = _session.State.Party[i];
            ItemLabel(new(20, 8 + 14 * i), PresentedOwnedName(member));
            if (MtMoonTargetActive && IsFieldEvolution(_mtMoonTargetItem))
                ItemLabel(new(142,8+14*i),MachineText(_session.FieldEvolutionPartyTarget(member,
                    _mtMoonTargetItem) is not null ? "gText_Able2" : "gText_NotAble"));
            else if (!member.IsEgg) ItemLabel(new(112, 8 + 14 * i), member.Pokemon.CurrentHp + "/" + member.Pokemon.Stats.MaxHp);
            DrawForestPartyStatus(member, new(198, 11 + 14 * i));
        }
        ItemLabel(new(8, 8 + 14 * _fieldMedicineTarget), "▶");
        ItemMessageBox("Use on which POKéMON?");
    }
}
