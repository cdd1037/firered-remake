using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Capture-only presentation. Application state owns every ball, RNG draw,
/// dex flag, nickname, party member and replacement decision.</summary>
public partial class CampaignView
{
    private CaptureDexDocument? _captureDex;
    private readonly Dictionary<string, ImageTexture> _captureTextures = new(StringComparer.Ordinal);
    private SourceGameFont _captureDexNormal = null!, _captureDexSmall = null!, _captureDexDescription = null!, _captureDexTitle = null!, _captureDexFooter = null!;
    private int _captureBagPocket, _captureBagSelected, _capturePouchSelected;
    private bool _captureBerries;

    // CampaignView combines this with the existing starter naming condition. The shared
    // LineEdit owns text/edit/submit; global F5/F9/F12/Escape stay with CampaignView.
    private bool CaptureNamingActive => _catalog.CaptureEnabled &&
        (_session.State.WildBattle?.Capture?.Stage == CampaignCaptureStage.Naming || _session.State.SafariBattle?.Capture?.Stage == CampaignCaptureStage.Naming);

    private void ResetCaptureBag() { _captureBagPocket = 0; _captureBagSelected = 0; _captureBerries = false; }

    private void LoadCapturePresentation()
    {
        if (!_catalog.CaptureEnabled) return;
        var folder = System.IO.Path.Combine(_root, "content/generated/capture-dex");
        var document = _catalog.ForestEnabled?PretForestCaptureDex.Load(_root):PretCaptureDex.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "capture-dex.json")));
        foreach (var texture in _captureTextures.Values) texture.Dispose();
        _captureTextures.Clear();
        foreach (var asset in document.Assets)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(_catalog.ForestEnabled?PretForestCaptureDex.ReadTexture(_root, asset):PretCaptureDex.ReadTexture(folder, asset)) != Error.Ok ||
                image.GetWidth() != asset.Width || image.GetHeight() != asset.Height || image.GetFormat() != Image.Format.Rgba8 ||
                Convert.ToHexStringLower(SHA256.HashData(image.GetData())) != asset.RgbaSha256)
                throw new InvalidDataException("Capture texture pixels differ: " + asset.Id);
            _captureTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        var backJson = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "capture-back-art.json"));
        PretCaptureDex.ValidateBackArtifact(backJson);
        using var back = JsonDocument.Parse(backJson);
        var sprites = back.RootElement.GetProperty("sprites");
        if (sprites.GetArrayLength() != 4) throw new InvalidDataException("Captured-mon back sprite set incomplete.");
        foreach (var sprite in sprites.EnumerateArray())
        {
            var file = sprite.GetProperty("texturePath").GetString()!;
            var species = sprite.GetProperty("sourceSpeciesId").GetInt32();
            var side = sprite.GetProperty("side").GetString()!;
            var variant = sprite.GetProperty("paletteVariant").GetString()!;
            if (species is not (16 or 19) || side != "back" || variant is not ("normal" or "shiny"))
                throw new InvalidDataException("Captured-mon back sprite identity invalid.");
            using var image = new Image();
            if (image.LoadPngFromBuffer(PretCaptureDex.ReadBackTexture(folder, file)) != Error.Ok || image.GetWidth() != 64 || image.GetHeight() != 64)
                throw new InvalidDataException("Captured-mon back sprite dimensions invalid.");
            var position = sprite.GetProperty("singleBattleTopLeft");
            var key = (species, side, variant == "shiny");
            _battleArt.Remove(key); // Texture() assets are retained and released by the scene texture owner.
            // Texture applies the same source RGB555 conversion as every battle sprite.
            _battleArt.Add(key, new(Texture(System.IO.Path.Combine(folder, file)),
                new(position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32())));
        }
        var fontDirectory = System.IO.Path.Combine(_root, "content/generated/source-font");
        static Color Rgb(ImmutableArray<int> rgb) => Color.Color8((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
        _captureDexNormal = SourceGameFont.Load(fontDirectory, document.Layout.NameFont, Rgb(document.Layout.ForegroundColor), Rgb(document.Layout.StatsShadowColor));
        _captureDexSmall = SourceGameFont.Load(fontDirectory, document.Layout.StatsFont, Rgb(document.Layout.ForegroundColor), Rgb(document.Layout.StatsShadowColor));
        _captureDexDescription = SourceGameFont.Load(fontDirectory, document.Layout.DescriptionFont, Rgb(document.Layout.ForegroundColor), Rgb(document.Layout.DescriptionShadowColor));
        _captureDexTitle = SourceGameFont.Load(fontDirectory, document.Layout.TitleFont, Rgb(document.Layout.ControlForegroundColor), Rgb(document.Layout.ControlShadowColor));
        _captureDexFooter = SourceGameFont.Load(fontDirectory, document.Layout.FooterFont, Rgb(document.Layout.ControlForegroundColor), Rgb(document.Layout.ControlShadowColor));
        _captureDex = document;
    }

    private ImmutableArray<InventoryStack> CaptureBagStacks => _session.State.Inventory is not { } inventory ? [] :
        _captureBerries ? inventory.Berries ?? [] : _captureBagPocket switch { 0 => inventory.BagItems, 1 => inventory.KeyItems ?? [], _ => inventory.Balls ?? [] };

    private bool TryCaptureWildInput(InputEventKey key, CampaignWildBattle battle)
    {
        if (!_catalog.CaptureEnabled) return false;
        if (_session.State.ForestTrainerBattle is null && battle.Capture is { } capture)
        {
            if (capture.Stage is CampaignCaptureStage.DexNotice or CampaignCaptureStage.DexEntry or CampaignCaptureStage.SentToPc)
            {
                if (key.Keycode == Key.Enter && _wildElapsed > .1) Apply(new("ack"));
            }
            else if (capture.Stage == CampaignCaptureStage.NicknameChoice)
            {
                if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
                if (key.Keycode is Key.Enter or Key.Backspace)
                    ApplyWildChoice(new("capture_choice", Choice: key.Keycode == Key.Enter && _wildCursor == 0 ? 1 : 0));
            }
            // LineEdit has first refusal on ordinary typing. Never dispatch battle/menu
            // actions while Naming is active, including Enter and Backspace.
            return true;
        }
        if (battle.Phase != CampaignBattlePhase.ChooseMove) return false;
        if (battle.Replacement is { } replacement)
        {
            if (replacement == CampaignReplacementStage.UseNext)
            {
                if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
                if (key.Keycode is Key.Enter or Key.Backspace)
                    ApplyWildChoice(new("battle_next", Choice: key.Keycode == Key.Enter && _wildCursor == 0 ? 1 : 0));
            }
            else
            {
                if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
                if (key.Keycode == Key.Down) _wildCursor = Math.Min(_session.State.Party.Length - 1, _wildCursor + 1);
                if (key.Keycode == Key.Enter)
                {
                    var selected = _session.State.Party[_wildCursor];
                    if (_wildCursor == battle.ActivePartyIndex || selected.Pokemon.CurrentHp == 0 || selected.IsEgg)
                        _wildNotice = "Choose a living POKéMON\nthat is not already in battle.";
                    else ApplyWildChoice(new("battle_switch", PartyIndex: _wildCursor));
                }
                // A failed escape leads here. Back cannot return to the action menu
                // with a fainted active Pokémon or automatically pick a reserve.
            }
            return true;
        }
        if (_wildMenu is not ("bag" or "bag-actions")) return false;
        if (_wildMenu == "bag-actions")
        {
            if (key.Keycode is Key.Up or Key.Down) _wildCursor ^= 1;
            if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == 1)
            { _wildMenu = "bag"; _wildCursor = _captureBagSelected; }
            else if (key.Keycode == Key.Enter)
            {
                if (TryOpenSingleBattleItem(CaptureBagStacks[_captureBagSelected].Item,battle)) return true;
                if (_captureBagPocket == 0) {
                    var item=CaptureBagStacks[_captureBagSelected].Item;
                    bool enabled=_session.BattleMedicineEnabled&&EarlyItem(item)?.UseSupport is EarlyShopUseSupport.Potion or EarlyShopUseSupport.StatusCure or EarlyShopUseSupport.DeferredSleep;
                    if(!enabled&&item!=OpeningInventoryRules.PotionId) { _wildNotice = "This item's battle use is\nnot implemented yet."; _wildNoticeReturn = "bag"; return true; }
                    _wildMedicineItem=enabled?item:null;_wildMenu = "medicine-target"; _wildCursor = enabled?battle.ActivePartyIndex:0; }
                else if (_captureBagPocket == 1)
                {
                    if(_catalog.PokemonTowerEnabled && CaptureBagStacks[_captureBagSelected].Item.Value == "pret:ITEM_POKE_FLUTE")
                    { ApplyWildChoice(new("battle_poke_flute",Text:"pret:ITEM_POKE_FLUTE")); return true; }
                    _wildMenu = "bag"; _wildCursor = _captureBagSelected;
                    _wildNotice = "Key items cannot be used\nin this battle.";
                }
                else
                {
                    // Capacity and available-ball checks remain authoritative application
                    // checks. UI cancellation never dispatches a command.
                    if (!ApplyWildChoice(new("battle_ball", Text: CaptureBagStacks[_captureBagSelected].Item.Value)))
                    {
                        if (_status != "storage_delivery_unsupported" && _status.Contains("unsupported", StringComparison.Ordinal) || _lastCommandFault)
                            _wildNotice = "This capture is not supported.\nNo BALL was used.";
                        _wildMenu = "bag"; _wildCursor = _captureBagSelected; _wildNoticeReturn = "bag";
                    }
                }
            }
            return true;
        }
        if (!_captureBerries && key.Keycode is Key.Left or Key.Right)
        {
            _captureBagPocket = Math.Clamp(_captureBagPocket + (key.Keycode == Key.Left ? -1 : 1), 0, 2);
            _wildCursor = 0; _captureBagSelected = 0; return true;
        }
        var stacks = CaptureBagStacks;
        _wildCursor = Math.Clamp(_wildCursor, 0, stacks.Length);
        if (key.Keycode == Key.Up) _wildCursor = Math.Max(0, _wildCursor - 1);
        if (key.Keycode == Key.Down) _wildCursor = Math.Min(stacks.Length, _wildCursor + 1);
        if (key.Keycode == Key.Backspace || key.Keycode == Key.Enter && _wildCursor == stacks.Length)
        { if(_captureBerries){_captureBerries=false;_wildCursor=_capturePouchSelected;}
          else {_wildMenu = "actions"; _wildCursor = 0;} }
        else if (key.Keycode == Key.Enter)
        { _captureBagSelected = _wildCursor; _wildMenu = "bag-actions"; _wildCursor = 0; }
        return true;
    }

    private bool DrawCaptureWildOverlay(CampaignWildBattle battle)
    {
        if (!_catalog.CaptureEnabled) return false;
        if (_session.State.ForestTrainerBattle is null && battle.Capture is { } capture)
        {
            if (capture.Stage == CampaignCaptureStage.DexEntry) DrawCaptureDexEntry(capture.Pokemon.SourceSpeciesId);
            else if (capture.Stage == CampaignCaptureStage.Naming) DrawCaptureNamingPanel();
            else
            {
                DrawRect(new(0, 0, 240, 160), Colors.Black);
                _battleSkin.DrawBackground(this); DrawTexture(CurrentWildBattleTerrain, Vector2.Zero);
                var playerAppearance = FuchsiaBattleAppearance(battle, WildBattleSide.Player, battle.DisplayPlayer);
                var player = _battleArt[(playerAppearance.Species, "back", playerAppearance.Shiny)];
                DrawTexture(player.Texture, player.Position);
                DrawWildHealth(battle.DisplayPlayer, Game.Domain.WildBattle.WildBattleSide.Player, battle.DisplayPlayer.CurrentHp);
                _battleSkin.DrawBattleMessage(this);
                var symbol = capture.Stage == CampaignCaptureStage.DexNotice ? "sText_PkmnDataAddedToDex" : "sText_GiveNicknameCaptured";
                var text=capture.Stage==CampaignCaptureStage.SentToPc?_session.VisibleCaptureMessage!:string.Join('\n',WildSourcePages(symbol));
                WildLines(text);
                if (capture.Stage == CampaignCaptureStage.NicknameChoice) DrawCaptureYesNo();
                else DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, text.Split('\n'), 16, _wildElapsed, true);
            }
            if (_wildNotice is not null) ItemMessageBox(_wildNotice);
            return true;
        }
        if (battle.Phase == CampaignBattlePhase.ChooseMove && battle.Replacement is { } replacement)
        {
            DrawRect(new(0, 0, 240, 160), Colors.Black);
            _battleSkin.DrawBattleMessage(this);
            if (replacement == CampaignReplacementStage.Select)
            { DrawCaptureParty(battle, true); WildLines("Choose a POKéMON."); }
            else
            {
                _battleSkin.DrawBackground(this); DrawTexture(CurrentWildBattleTerrain, Vector2.Zero);
                var opponentAppearance = FuchsiaBattleAppearance(battle, WildBattleSide.Opponent, battle.DisplayOpponent);
                var asset = _battleArt[(opponentAppearance.Species, "front", opponentAppearance.Shiny)];
                DrawTexture(asset.Texture, asset.Position); DrawWildHealth(battle.DisplayOpponent, Game.Domain.WildBattle.WildBattleSide.Opponent, battle.DisplayOpponent.CurrentHp);
                _battleSkin.DrawBattleMessage(this); WildLines(string.Join('\n', WildSourcePages("sText_UseNextPkmn"))); DrawCaptureYesNo();
            }
            if (_wildNotice is not null) ItemMessageBox(_wildNotice);
            return true;
        }
        if (battle.Phase != CampaignBattlePhase.ChooseMove || _wildMenu is not ("bag" or "bag-actions")) return false;
        DrawCaptureBag(); return true;
    }

    private void DrawCaptureYesNo()
    {
        _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
        SourceText(_sourceNormal, new(200, 72), "YES"); SourceText(_sourceNormal, new(200, 88), "NO");
        SourceText(_sourceNormal, new(188, 72 + _wildCursor * 16), "▶");
    }

    private void DrawCaptureBag()
    {
        var stacks = CaptureBagStacks;
        int selected = Math.Clamp(_wildMenu == "bag-actions" ? _captureBagSelected : _wildCursor, 0, stacks.Length);
        if (_captureBagPocket == 0 || _captureBerries)
        {
            if(_session.FullBattleItemsEnabled) DrawSingleBattleItemList(stacks,selected);
            else DrawItemList(_session.State.Inventory!, "bag", selected);
        }
        else
        {
            DrawQuestPocketAsset(_captureBagPocket == 1 ? "bag-key-items-background" : "bag-poke-balls-background");
            var title = _questPocketUi!.Texts[_captureBagPocket == 1 ? "gText_KeyItems2" : "gText_PokeBalls2"];
            var width = _sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title), fontSize: _sourceWhite.Size).X;
            SourceText(_sourceWhite, OpeningItemUiSkin.BagTitleCenter - new Vector2((int)Math.Ceiling(width / 2), 0), title);
            if (_wildMenu == "bag" && _wildNotice is null)
            { DrawQuestPocketAsset("pocket-left-arrow"); if (_captureBagPocket == 1) _itemSkin.DrawPocketRightArrow(this); }
            int start = _catalog.SsAnneEnabled ? Math.Max(0, selected - 5) : 0;
            int visible = _catalog.SsAnneEnabled ? 6 : stacks.Length + 1;
            for (int i = start; i < Math.Min(stacks.Length, start + visible); i++)
            {
                SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, i - start), QuestPocketItem(stacks[i]).Name);
                if (_captureBagPocket == 2) SourceText(_sourceSmall, OpeningItemUiSkin.QuantityText(false, i - start), _itemSkin.FormatText("gText_TimesStrVar1", stacks[i].Quantity.ToString().PadLeft(3)));
            }
            if (stacks.Length < start + visible) SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, stacks.Length - start), _itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal, OpeningItemUiSkin.ListCursor(false, selected - start), _itemSkin.Text("gText_SelectorArrow2"));
            if (selected < stacks.Length)
            {
                var item = QuestPocketItem(stacks[selected]); DrawQuestPocketAsset(item.Icon);
                ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, item.Description);
            }
            else ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, stacks.IsEmpty && _captureBagPocket == 2
                ? "There are no POKé BALLS\nin the BAG." : _itemSkin.Text("gText_CloseBag"));
        }
        if (_wildMenu == "bag-actions" && selected < stacks.Length)
        {
            _battleSkin.DrawStandardFrame(this, OpeningItemUiSkin.BattleBagActionFrame);
            var actions = new[] { _itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel") };
            for (int i = 0; i < actions.Length; i++) SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionText(i), actions[i]);
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionCursor(_wildCursor), _itemSkin.Text("gText_SelectorArrow2"));
            _itemSkin.DrawStandardFrame(this, OpeningItemUiSkin.BagSelectedFrame);
            var name = _captureBagPocket == 0 || _captureBerries ? SingleBattleItemName(stacks[selected].Item) : QuestPocketItem(stacks[selected]).Name;
            ItemLines(_sourceNormal, OpeningItemUiSkin.BagSelectedText, _itemSkin.FormatText("gText_Var1IsSelected", name));
        }
        if (_wildNotice is not null) ItemMessageBox(_wildNotice);
    }

    private void DrawCaptureParty(CampaignWildBattle battle, bool selecting, bool cancellable = false)
    {
        // All six persistent slots fit; names/HP come from their own member. Active HP
        // uses the event-time display so an acknowledged hit never jumps ahead.
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
        int rowHeight = cancellable ? 14 : 16;
        for (int i = 0; i < _session.State.Party.Length; i++)
        {
            var owned = _session.State.Party[i]; var pokemon = i == battle.ActivePartyIndex ? battle.DisplayPlayer : owned.Pokemon;
            string prefix = i == battle.ActivePartyIndex ? ">" : " ";
            SourceText(_sourceNormal, new(20, 8 + i * rowHeight), prefix + PresentedOwnedName(owned));
            if (!owned.IsEgg) SourceText(_sourceSmall, new(130, 9 + i * rowHeight), $"L{pokemon.Level} {pokemon.CurrentHp}/{pokemon.Stats.MaxHp}");
            DrawForestPartyStatus(owned, new(198, 11 + i * rowHeight));
            if (selecting && i == _wildCursor) SourceText(_sourceNormal, new(8, 8 + i * rowHeight), "▶");
        }
        if (cancellable)
        {
            int y = 8 + _session.State.Party.Length * rowHeight;
            SourceText(_sourceNormal, new(20, y), "CANCEL");
            if (_wildCursor == _session.State.Party.Length) SourceText(_sourceNormal, new(8, y), "▶");
        }
    }

    private void DrawCaptureNamingPanel()
    {
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(8, 16, 224, 136));
        var capture = _session.State.SafariBattle?.Capture ?? _session.State.WildBattle!.Capture!;
        DrawTexture(PresentedPokemonIcon(capture.Pokemon),new(192,48));
        SourceText(_sourceNormal, new(20, 26), capture.Origin.DefaultName + "'s nickname?");
        SourceText(_sourceSmall, new(20, 98), "Type a name, then press Enter.");
        SourceText(_sourceSmall, new(20, 114), "Leave blank to keep " + capture.Origin.DefaultName + ".");
        if (_status.StartsWith("nickname_", StringComparison.Ordinal))
            SourceText(_sourceSmall, new(20, 133), _status == "nickname_length" ? "Use at most 10 characters." : "That character is not available.");
    }

    private void DrawCaptureDexEntry(int sourceSpeciesId)
    {
        var document = _captureDex ?? throw new InvalidDataException("Capture dex presentation was not loaded.");
        var entry = CaptureDexEntry(sourceSpeciesId); var layout = document.Layout;
        var background = document.Assets.Single(x => x.Id == "capture-dex-background");
        DrawTexture(_captureTextures[background.Id], new(background.X, background.Y));
        if(_route22Footprints.TryGetValue(sourceSpeciesId,out var route22Footprint))DrawTexture(route22Footprint,new(layout.FootprintX,layout.FootprintY));
        else
        {
            var footprint = document.Assets.Single(x => x.TexturePath == entry.FootprintPath);
            DrawTexture(_captureTextures[footprint.Id], new(footprint.X, footprint.Y));
        }
        // Source Dex uses the representative Unown PID and fixed trainer ID SHINY_ODDS (8).
        var appearance=PresentedDexAppearance(sourceSpeciesId);
        DrawTexture(_battleArt[(appearance.Species, "front", appearance.Shiny)].Texture, new(layout.FrontX, layout.FrontY));
        var titleWidth = _captureDexTitle.Font.GetStringSize(_captureDexTitle.PrepareText(entry.HabitatTitle), fontSize: _captureDexTitle.Size).X;
        SourceText(_captureDexTitle, new((int)((layout.HeaderCenterWidth - titleWidth) / 2), layout.HeaderY), entry.HabitatTitle);
        // The special two-cell NO/A-button glyphs are not in the current shared font atlas.
        // Numeric id and NEXT retain the source coordinates; exact glyph import is recorded.
        SourceText(_captureDexSmall, new(layout.NumberX, layout.NumberY), entry.NationalNumber.ToString("D3"));
        SourceText(_captureDexNormal, new(layout.NameX, layout.NameY), entry.Name, layout.NameLetterSpacing);
        SourceText(_captureDexSmall, new(layout.CategoryX, layout.CategoryY), entry.DisplayCategory + layout.CategorySuffix, layout.StatsLetterSpacing);
        SourceText(_captureDexSmall, new(layout.HeightX, layout.HeightY), layout.HeightLabel);
        SourceText(_captureDexSmall, new(layout.HeightX + layout.MeasureValueOffsetX, layout.HeightY), entry.HeightDisplay);
        SourceText(_captureDexSmall, new(layout.WeightX, layout.WeightY), layout.WeightLabel);
        SourceText(_captureDexSmall, new(layout.WeightX + layout.MeasureValueOffsetX, layout.WeightY), entry.WeightDisplay);
        var lines = entry.Description.Split('\n');
        // English text.c advances only by glyph width: source argument letterSpacing 1
        // has effective spacing 0. The imported layout records both separately.
        float descriptionWidth = lines.Max(line => _captureDexDescription.Font.GetStringSize(_captureDexDescription.PrepareText(line), fontSize: _captureDexDescription.Size).X);
        for (int i = 0; i < lines.Length; i++) SourceText(_captureDexDescription,
            new((int)((layout.DescriptionCenterWidth - descriptionWidth) / 2), layout.DescriptionY + i * (layout.DescriptionLineHeight + layout.DescriptionLineSpacing)),
            lines[i], layout.DescriptionLetterSpacing);
        const string footer = "NEXT";
        var footerWidth = _captureDexFooter.Font.GetStringSize(footer, fontSize: _captureDexFooter.Size).X;
        SourceText(_captureDexFooter, new(layout.FooterRightX - footerWidth, layout.FooterY), footer);
    }
}
