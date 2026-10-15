using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Items;
using Godot;

/// <summary>Source pockets; the bounded TM CASE can be opened through its field Use action.</summary>
public partial class CampaignView
{
    private QuestItemUiDocument? _questPocketUi;
    private readonly Dictionary<string, ImageTexture> _questPocketTextures = new(StringComparer.Ordinal);
    private int _questPocket, _questPocketCursor, _questPocketAction;
    private bool _questPocketPopup;
    private string? _questPocketNotice;
    partial void ExtendMachineQuestPocket(ref QuestItemUiDocument document);

    private void LoadQuestPocketPresentation()
    {
        foreach (var texture in _questPocketTextures.Values) texture.Dispose();
        _questPocketTextures.Clear(); _questPocketUi = null; ResetQuestPocketPresentation();
        if (_catalog.QuestItems is null) return;
        var directory = System.IO.Path.Combine(_root, "content/generated/quest-item-ui");
        var document = PretQuestItemUi.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "quest-item-ui.json")));
        PretQuestItemUi.ValidateCatalog(document, _catalog.QuestItems);
        try
        {
            foreach (var asset in document.Assets)
            {
                using var image = new Image();
                if (image.LoadPngFromBuffer(PretQuestItemUi.ReadTexture(directory, asset)) != Error.Ok
                    || image.GetWidth() != asset.Width || image.GetHeight() != asset.Height || image.GetFormat() != Image.Format.Rgba8)
                    throw new InvalidDataException("Quest pocket texture pixels differ: " + asset.Id);
                _questPocketTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
            }
            if(_catalog.OldManTutorialEnabled)
            {
                var oldDirectory=System.IO.Path.Combine(_root,"content/generated/old-man-art");
                var oldArt=PretOldManArt.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(oldDirectory,"old-man-art.json")));
                var tv=oldArt.TeachyTv;var item=_catalog.QuestItems.Items.Single(x=>x.Id.Value==tv.Id);
                if(item.SourceItemSymbol!=tv.SourceSymbol||item.Name!=tv.Name||item.FieldUseFunction!=tv.FieldUseFunction)throw new InvalidDataException("Teachy TV presentation differs from source item.");
                var asset=oldArt.Assets.Single(x=>x.Id==tv.Icon);using var image=new Image();
                if(image.LoadPngFromBuffer(PretOldManArt.ReadTexture(oldDirectory,asset))!=Error.Ok||image.GetWidth()!=asset.Width||image.GetHeight()!=asset.Height||image.GetFormat()!=Image.Format.Rgba8)throw new InvalidDataException("Teachy TV texture pixels differ.");
                _questPocketTextures.Add(asset.Id,ImageTexture.CreateFromImage(image));
                // Same source icon window as the existing key item; authenticated extension leaves original assets intact.
                var icon=document.Assets.Single(x=>x.Id==document.Items.First().Icon);
                document=document with{Items=document.Items.Add(tv),Assets=document.Assets.Add(new(asset.Id,asset.TexturePath,asset.Width,asset.Height,icon.X,icon.Y))};
            }
            ExtendMachineQuestPocket(ref document);
            ExtendTownMapQuestPocket(ref document);
            ExtendCityServicesQuestPocket(ref document);
            ExtendRegionalServicesQuestPocket(ref document);
            ExtendItemfinderQuestPocket(ref document);
            ExtendEventTicketQuestPocket(ref document);
            ExtendVsSeekerQuestPocket(ref document);
            _questPocketUi = document;
        }
        catch
        {
            foreach (var texture in _questPocketTextures.Values) texture.Dispose();
            _questPocketTextures.Clear(); throw;
        }
    }

    private void ResetQuestPocketPresentation()
    {
        _questPocket = 0; _questPocketCursor = 0; _questPocketAction = 0;
        _questPocketPopup = false; _questPocketNotice = null;
    }

    private bool QuestPocketEnabled => _questPocketUi is not null && _catalog.QuestItems is not null
        && _session.State.ItemMenu is { Origin: "bag", View: "bag" } or { Origin: "bag", View: "message", ReturnView: "bag" };

    private ImmutableArray<InventoryStack> QuestPocketStacks => (_questPocket == 1
        ? _session.State.Inventory!.KeyItems : _session.State.Inventory!.Balls) ?? [];

    private int QuestPocketListStart => _catalog.SsAnneEnabled ? Math.Max(0, _questPocketCursor - 5) : 0;
    private QuestItemUiEntry QuestPocketItem(InventoryStack stack)
        => _questPocketUi!.Items.Single(x => x.Id == stack.Item.Value);

    private string[] QuestPocketActions() => _questPocket == 1
        ? _questPocketCursor<QuestPocketStacks.Length&&_session.SupportsRegisteredKeyItem(QuestPocketStacks[_questPocketCursor].Item.Value)
            ? [_itemSkin.Text("gOtherText_Use"), _session.State.RegisteredItem==QuestPocketStacks[_questPocketCursor].Item.Value?"DESELECT":"REGISTER",_itemSkin.Text("gFameCheckerText_Cancel")]
            : [_itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel")]
        : _session.State.ItemMenu?.TargetInstanceId is not null ? [_itemSkin.Text("gOtherText_Give"), _itemSkin.Text("gFameCheckerText_Cancel")]
        : [_itemSkin.Text("gOtherText_Give"), _itemSkin.Text("gOtherText_Toss"), _itemSkin.Text("gFameCheckerText_Cancel")];

    private bool TryQuestPocketInput(InputEventKey key)
    {
        if (HeldItemScreen || MailScreen)return false;
        if (!QuestPocketEnabled || key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        // Old key-item return views preserve their visual pocket. Reconcile it
        // on actual Bag input, never from a shared/Teachy TV drawing routine.
        if(key.Pressed&&!key.Echo&&_session.State.ItemMenu is { View:"bag", Toss:null } menu&&menu.Pocket!=_questPocket)
            Apply(new("bag_pocket",Choice:_questPocket));
        if (_questPocket == 0)
        {
            // The existing Potion popup, notice, quantity and command flow retain input ownership.
            if (_itemPopup || _itemQuantityOpen || _itemNotice is not null || key.Keycode is not (Key.Left or Key.Right)) return false;
            if (key.Pressed && !key.Echo && key.Keycode == Key.Right) SelectFieldBagPocket(1);
            return true;
        }
        if (!key.Pressed || key.Echo) return true;
        _held = null;
        if (_session.State.ItemMenu?.View == "message")
        {
            if (key.Keycode is Key.Enter or Key.Backspace) Apply(new("menu_ack"));
            return true;
        }
        if (_itemNotice is not null)
        {
            if (key.Keycode is Key.Enter or Key.Backspace) _itemNotice = null;
            return true;
        }
        if (_questPocketNotice is not null)
        {
            if (key.Keycode is Key.Enter or Key.Backspace) _questPocketNotice = null;
            return true;
        }
        if (_questPocketPopup)
        {
            var actions = QuestPocketActions();
            if (key.Keycode == Key.Up) _questPocketAction = Math.Max(0, _questPocketAction - 1);
            if (key.Keycode == Key.Down) _questPocketAction = Math.Min(actions.Length - 1, _questPocketAction + 1);
            if (key.Keycode == Key.Backspace) _questPocketPopup = false;
            if (key.Keycode == Key.Enter)
            {
                _questPocketPopup = false;
                if (_questPocket == 1 && _questPocketAction == 0 && _session.State.ItemMenu?.TargetInstanceId is not null &&
                    QuestPocketStacks[_questPocketCursor].Item.Value is not ("pret:ITEM_BERRY_POUCH" or "pret:ITEM_TM_CASE"))
                { _questPocketNotice = "This item can't be held."; return true; }
                if (_questPocket == 2 && _questPocketAction == 0)
                { StartGiveItem(QuestPocketStacks[_questPocketCursor].Item); return true; }
                if (_questPocket == 2 && actions[_questPocketAction] == _itemSkin.Text("gOtherText_Toss"))
                { OpenBagToss(QuestPocketStacks[_questPocketCursor].Item); return true; }
                if (_questPocket == 1 && _session.SupportsRegisteredKeyItem(QuestPocketStacks[_questPocketCursor].Item.Value) && _questPocketAction < 2)
                {
                    string registeredId = QuestPocketStacks[_questPocketCursor].Item.Value;
                    Apply(new(_questPocketAction == 0 ? _session.RegisteredKeyItemUseCommand(registeredId) : "item_register", Text: registeredId));
                    if (_questPocketAction == 1 && _session.State.RegisteredItem is not null) _questPocketNotice = "Registered. Press R in the field.";
                    return true;
                }
                if (_catalog.GameCornerCoinsEnabled && _questPocket == 1 && _questPocketAction == 0 && QuestPocketStacks[_questPocketCursor].Item.Value == "pret:ITEM_COIN_CASE")
                { Apply(new("field_coin_case",Text:"pret:ITEM_COIN_CASE")); return true; }
                if (_catalog.PokemonTowerEnabled && _questPocket == 1 && _questPocketAction == 0 && QuestPocketStacks[_questPocketCursor].Item.Value == "pret:ITEM_POKE_FLUTE")
                { Apply(new("field_poke_flute",Text:"pret:ITEM_POKE_FLUTE")); return true; }
                if (_questPocket == 1 && _questPocketAction == 0 && TryCityServiceItem(QuestPocketStacks[_questPocketCursor].Item.Value)) return true;
                if (_catalog.TownMapEnabled && _questPocket == 1 && _questPocketAction == 0 && QuestPocketStacks[_questPocketCursor].Item == QuestItemRules.TownMapId)
                { Apply(new("town_map_open",Text:QuestItemRules.TownMapId.Value)); return true; }
                if (_catalog.SsAnneEnabled && _questPocket == 1 && _questPocketAction == 0 && QuestPocketStacks[_questPocketCursor].Item.Value == "pret:ITEM_BERRY_POUCH")
                { Apply(new("berry_open")); return true; }
                if (_questPocket == 1 && _questPocketAction == 0 && QuestPocketStacks[_questPocketCursor].Item == PewterMachineRules.TmCaseId)
                { Apply(new("machine_open")); return true; }
                if (_questPocketAction != actions.Length - 1)
                    _questPocketNotice = _questPocket == 1
                        ? QuestPocketStacks[_questPocketCursor].Item==BillItemRules.FameCheckerId?"FAME CHECKER viewing is not\nimplemented yet.":QuestPocketStacks[_questPocketCursor].Item==QuestItemRules.TeachyTvId?"TEACHY TV playback is not\nimplemented yet.":_questPocketUi!.Texts["gText_OakForbidsUseOfItemHere"].Replace("{PLAYER}", _session.State.PlayerName, StringComparison.Ordinal)
                        : "This action isn't available.";
            }
            return true;
        }
        if (key.Keycode is Key.Left or Key.Right)
        {
            SelectFieldBagPocket(Math.Clamp(_questPocket + (key.Keycode == Key.Left ? -1 : 1), 0, 2));
            return true;
        }
        if (key.Keycode == Key.Backspace) { Apply(new("menu_back")); return true; }
        var stacks = QuestPocketStacks;
        _questPocketCursor = Math.Min(_questPocketCursor, stacks.Length);
        if (key.Keycode == Key.Up) _questPocketCursor = Math.Max(0, _questPocketCursor - 1);
        if (key.Keycode == Key.Down) _questPocketCursor = Math.Min(stacks.Length, _questPocketCursor + 1);
        if (key.Keycode == Key.Enter)
        {
            if (_questPocketCursor == stacks.Length) Apply(new("menu_back"));
            else { _questPocketPopup = true; _questPocketAction = 0; }
        }
        return true;
    }

    private void DrawQuestPocketAsset(string id)
    {
        var asset = _questPocketUi!.Assets.Single(x => x.Id == id);
        TextureFilter = TextureFilterEnum.Nearest;
        DrawTextureRect(_questPocketTextures[id], new(asset.X, asset.Y, asset.Width, asset.Height), false);
    }

    private bool DrawQuestPocket()
    {
        if (HeldItemScreen || MailScreen || !QuestPocketEnabled || _questPocket == 0) return false;
        DrawQuestPocketAsset(_questPocket == 1 ? "bag-key-items-background" : "bag-poke-balls-background");
        var title = _questPocketUi!.Texts[_questPocket == 1 ? "gText_KeyItems2" : "gText_PokeBalls2"];
        var width = _sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title), fontSize: _sourceWhite.Size).X;
        SourceText(_sourceWhite, OpeningItemUiSkin.BagTitleCenter - new Vector2((int)Math.Ceiling(width / 2), 0), title);
        if (_session.State.ItemMenu is { View:not "message", Toss:null } && !_questPocketPopup && _questPocketNotice is null && _itemNotice is null)
        {
            DrawQuestPocketAsset("pocket-left-arrow");
            if (_questPocket == 1) _itemSkin.DrawPocketRightArrow(this);
        }
        var stacks = QuestPocketStacks;
        _questPocketCursor = Math.Min(_questPocketCursor, stacks.Length);
        int start = QuestPocketListStart;
        int visible = _catalog.SsAnneEnabled ? 6 : stacks.Length + 1;
        for (var i = start; i < Math.Min(stacks.Length, start + visible); i++)
        {
            var item = QuestPocketItem(stacks[i]);
            SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, i - start), item.Name);
            // The source suppresses quantity for key items regardless of stack size.
            if (_questPocket == 2) SourceText(_sourceSmall, OpeningItemUiSkin.QuantityText(false, i - start),
                _itemSkin.FormatText("gText_TimesStrVar1", stacks[i].Quantity.ToString().PadLeft(3)));
        }
        if (stacks.Length < start + visible) SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, stacks.Length - start), _itemSkin.Text("gFameCheckerText_Cancel"));
        SourceText(_sourceNormal, OpeningItemUiSkin.ListCursor(false, _questPocketCursor - start), _itemSkin.Text("gText_SelectorArrow2"));
        if (_questPocketCursor < stacks.Length)
        {
            var item = QuestPocketItem(stacks[_questPocketCursor]);
            DrawQuestPocketAsset(item.Icon);
            ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, item.Description);
        }
        else ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, _itemSkin.Text("gText_CloseBag"));
        if (_questPocketPopup && _questPocketCursor < stacks.Length)
        {
            var actions = QuestPocketActions();
            // bag.c sWindowTemplates[10 + count - 1], including its 8px outer frame.
            int y = 144 - actions.Length * 16;
            _battleSkin.DrawStandardFrame(this, new(168, y, 72, actions.Length * 16 + 16));
            for (var i = 0; i < actions.Length; i++) SourceText(_sourceNormal, new(184, y + 10 + i * 16), actions[i]);
            SourceText(_sourceNormal, new(176, y + 10 + _questPocketAction * 16), _itemSkin.Text("gText_SelectorArrow2"));
            _itemSkin.DrawStandardFrame(this, OpeningItemUiSkin.BagSelectedFrame);
            ItemLines(_sourceNormal, OpeningItemUiSkin.BagSelectedText,
                _itemSkin.FormatText("gText_Var1IsSelected", QuestPocketItem(stacks[_questPocketCursor]).Name));
        }
        if (_session.State.ItemMenu?.View == "message")
        {
            // Reuse the existing Bag result panel; its left edge leaves the
            // selected key-item icon visible instead of covering it.
            _itemSkin.DrawStandardFrame(this, OpeningItemUiSkin.PcResultFrame);
            ItemLines(_sourceNormal, OpeningItemUiSkin.PcResultText, _session.VisibleItemMessage!);
            NotePokemonTowerFluteMessage();
        }
        if (_questPocketNotice is not null) ItemMessageBox(_questPocketNotice);
        if (_itemNotice is not null) ItemMessageBox(_itemNotice);
        return true;
    }
}
