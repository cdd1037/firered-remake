using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

// Box metadata and marking drafts belong to Application. This adapter owns only
// focus, menu cursors and the shared text editor; it never rewrites a Pokemon.
public partial class CampaignView
{
    private PretStorageCustomization? _storageCustomizationArt;
    private readonly Dictionary<string, Texture2D> _storageCustomizationTextures = new(StringComparer.Ordinal);
    private SourceGameFont? _storageTitleFont;
    private bool _storageTitleSelected, _storageBoxOptionsOpen, _storageJumpOpen;
    private int _storageCustomizationCursor, _storageBoxOptionCursor, _storageJumpBox, _storageBoundBox;
    private long _storageBoxOptionsRevision, _storageCustomizationRevision;
    private CampaignStorageCustomizationKind? _storageCustomizationKind;
    private ulong _storageCustomizationClosedFrame = ulong.MaxValue;
    private static readonly string[] StorageBoxOptionSymbols = ["gPCText_Jump", "gPCText_Wallpaper", "gPCText_Name", "gPCText_Cancel"];
    private static readonly string[] StorageMarkShapeNames = ["circle", "square", "triangle", "heart"];
    private bool StorageBoxNaming => _session?.State.Pc?.Customization?.Kind == CampaignStorageCustomizationKind.BoxName;
    private bool StorageCustomizationOverlayActive => _storageBoxOptionsOpen || _storageJumpOpen || _session?.State.Pc?.Customization is not null;
    private Rect2 StorageBoxTitleRect => _session.State.Pc!.View == CampaignPcView.Deposit ? new(32, 0, 176, 24) : new(108, 16, 108, 16);
    private string StorageBoxName(int box) => PokemonStorageCustomizationRules.BoxName(_session.State.Storage!, box);
    private string StorageCustomizationText(string symbol) => _storageCustomizationArt!.Text(symbol);

    private void LoadStorageCustomizationPresentation()
    {
        _storageCustomizationArt = PretStorageCustomization.Load(_root);
        foreach (var asset in _storageCustomizationArt.Assets)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(_storageCustomizationArt.ReadAssetTexture(asset.Id)) != Error.Ok)
                throw new InvalidDataException("Invalid authenticated storage customization PNG.");
            _storageCustomizationTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        var title = _storageCustomizationArt.Wallpapers[0];
        _storageTitleFont = SourceGameFont.Load(Path.Combine(_root, "content/generated/ui/font"), "normal",
            StorageRgb555(title.TitleTextRgb555), StorageRgb555(title.TitleShadowRgb555));
    }

    private void DisposeStorageCustomizationPresentation()
    {
        foreach(var texture in _storageCustomizationTextures.Values)texture.Dispose();
        _storageCustomizationTextures.Clear(); _storageCustomizationArt=null;
        _storageTitleFont?.Dispose(); _storageTitleFont=null;
        ResetStorageCustomizationPresentation();
    }

    private static Color StorageRgb555(int packed)
    {
        static float Channel(int value) => ((value << 3) | (value >> 2)) / 255f;
        return new(Channel(packed & 31), Channel(packed >> 5 & 31), Channel(packed >> 10 & 31));
    }

    private void ResetStorageCustomizationPresentation()
    {
        _storageTitleSelected = _storageBoxOptionsOpen = _storageJumpOpen = false;
        _storageCustomizationCursor = _storageBoxOptionCursor = 0;
        _storageCustomizationKind = null;
    }

    private void SynchronizeStorageCustomization()
    {
        var kind = _session.State.Pc?.Customization?.Kind;
        if (kind != _storageCustomizationKind)
        {
            if (_storageCustomizationKind is not null && kind is null) _storageCustomizationClosedFrame = Engine.GetProcessFrames();
            _storageCustomizationKind = kind; _storageCustomizationCursor = 0;
            _storageCustomizationRevision = _session.Revision;
            _held = null; _repeat = 0; ReleaseFieldTextInput();
        }
    }

    private bool ConsumeStorageCustomizationTransitionInput(InputEvent input)
    {
        if (_storageCustomizationClosedFrame != Engine.GetProcessFrames() || input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 or Key.Escape }) return false;
        GetViewport().SetInputAsHandled(); return true;
    }

    private void OpenStorageBoxOptions()
    {
        if (_busy || StorageCreatureOverlayActive || _storageItemActions.Length != 0 || _storageItemInfo is not null ||
            _session.State.Pc?.ItemPrompt is not null || StorageItemBagOpen || StorageCustomizationOverlayActive) return;
        _storageTitleSelected = true; _storageBoxOptionsOpen = true; _storageBoxOptionCursor = 0;
        _storageBoundBox = _session.State.Storage!.CurrentBox; _storageBoxOptionsRevision = _session.Revision;
    }

    private bool StorageBoxSelectionIsCurrent()
    {
        if (_session.Revision == _storageBoxOptionsRevision && _session.State.Storage!.CurrentBox == _storageBoundBox) return true;
        _storageBoxOptionsOpen = _storageJumpOpen = false;
        _pcNotice = "The selected BOX has changed.\nPlease choose it again."; return false;
    }

    private bool StorageCustomizationAction(CampaignCommand command)
    {
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return false; }
        bool accepted = Apply(command);
        if (accepted) { _storageCustomizationRevision = _session.Revision; return true; }
        _pcNotice = _status.Contains("name", StringComparison.Ordinal)
            ? "Use up to 8 source keyboard characters."
            : _status.Contains("identity", StringComparison.Ordinal) || _status.Contains("stale", StringComparison.Ordinal)
                ? "The selection has changed.\nPlease choose it again." : StorageError(_status);
        return false;
    }

    private bool StorageCustomizationIsCurrent()
    {
        if (_session.Revision == _storageCustomizationRevision) return true;
        _pcNotice = "The selection has changed.\nPlease choose it again."; return false;
    }

    private void ConfirmStorageBoxOption()
    {
        if (!StorageBoxSelectionIsCurrent()) return;
        _storageBoxOptionsOpen = false;
        if (_storageBoxOptionCursor == 3) return;
        if (_storageBoxOptionCursor == 0) { _storageJumpOpen = true; _storageJumpBox = _storageBoundBox; return; }
        StorageCustomizationAction(new(_storageBoxOptionCursor == 1 ? "storage_wallpaper_open" : "storage_box_name_open") { StorageBoxIndex = _storageBoundBox });
    }

    private CampaignCommand StorageMarkCommand(string kind, int? choice = null)
    {
        var modal = _session.State.Pc!.Customization!;
        return new(kind, Choice: choice) { StorageLocation = modal.Location,
            TargetInstanceId = modal.Location is null ? null : modal.InstanceId,
            HeldInstanceId = modal.Location is null ? modal.InstanceId : null };
    }

    private bool SubmitStorageBoxNameFromView(string text)
    {
        if (!StorageBoxNaming) return false;
        if (_pcNotice is not null) { _pcNotice = null; QueueRedraw(); return true; }
        if (StorageCustomizationIsCurrent())
            StorageCustomizationAction(new("storage_box_name", Text: text) { StorageBoxIndex = _session.State.Pc!.Customization!.BoxIndex });
        return true;
    }

    private bool StorageCustomizationInput(InputEventKey key)
    {
        if (!StorageCustomizationOverlayActive) return false;
        if (!key.Pressed || key.Echo) return true;
        _held = null; _repeat = 0;
        if (_pcNotice is not null) { if (key.Keycode is Key.Enter or Key.Backspace or Key.B) _pcNotice = null; return true; }
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return true; }
        if (StorageBoxNaming)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter) SubmitStorageBoxNameFromView(_nicknameEdit.Text);
            return true;
        }
        bool back = key.Keycode is Key.Backspace or Key.B;
        if (_storageBoxOptionsOpen)
        {
            if (back) _storageBoxOptionsOpen = false;
            else { FieldMenuMoveCursor(key, ref _storageBoxOptionCursor, 4); if (key.Keycode == Key.Enter) ConfirmStorageBoxOption(); }
            return true;
        }
        if (_storageJumpOpen)
        {
            if (back) { _storageJumpOpen = false; return true; }
            if (key.Keycode is Key.Left or Key.Up or Key.Pageup) _storageJumpBox = (_storageJumpBox + 13) % 14;
            if (key.Keycode is Key.Right or Key.Down or Key.Pagedown) _storageJumpBox = (_storageJumpBox + 1) % 14;
            if (key.Keycode == Key.Enter && StorageBoxSelectionIsCurrent())
            { _storageJumpOpen = false; if (_storageJumpBox != _storageBoundBox) StorageAction(new("storage_box", Choice: _storageJumpBox)); }
            return true;
        }
        if (back) { StorageCustomizationAction(new("pc_back")); return true; }
        var modal = _session.State.Pc!.Customization!;
        int count = modal.Kind == CampaignStorageCustomizationKind.Markings ? 6 : 4;
        if (key.Keycode == Key.Up) _storageCustomizationCursor = (_storageCustomizationCursor + count - 1) % count;
        if (key.Keycode == Key.Down) _storageCustomizationCursor = (_storageCustomizationCursor + 1) % count;
        if (key.Keycode != Key.Enter || !StorageCustomizationIsCurrent()) return true;
        switch (modal.Kind)
        {
            case CampaignStorageCustomizationKind.WallpaperSets:
                StorageCustomizationAction(new("storage_wallpaper_category", Choice: _storageCustomizationCursor) { StorageBoxIndex = modal.BoxIndex }); break;
            case CampaignStorageCustomizationKind.Wallpapers:
                int wallpaper = _storageCustomizationArt!.Categories.Single(c => c.Id == modal.WallpaperSet).WallpaperIds[_storageCustomizationCursor];
                StorageCustomizationAction(new("storage_wallpaper", Choice: wallpaper) { StorageBoxIndex = modal.BoxIndex }); break;
            case CampaignStorageCustomizationKind.Markings:
                StorageCustomizationAction(_storageCustomizationCursor == 5 ? new("storage_customization_cancel")
                    : StorageMarkCommand(_storageCustomizationCursor == 4 ? "storage_mark_confirm" : "storage_mark_toggle", _storageCustomizationCursor < 4 ? _storageCustomizationCursor : null)); break;
        }
        return true;
    }

    private bool StorageTitleInput(InputEventKey key)
    {
        if (_session.State.Pc?.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move or CampaignPcView.MoveItems)) return false;
        if (_storagePartyPeek || _storageMovePartyOpen || StorageCreatureOverlayActive || _storageItemActions.Length != 0 || _storageItemInfo is not null || StorageItemBagOpen || _session.State.Pc.ItemPrompt is not null) return false;
        if (key.Keycode == Key.Home || key.Keycode == Key.Up && !_storageMovePartyOpen && _session.State.Pc.View != CampaignPcView.Deposit && _boxCursor < 6 && !_storageTitleSelected)
        { _storageTitleSelected = true; return true; }
        if (!_storageTitleSelected) return false;
        if (key.Keycode == Key.Down) { _storageTitleSelected = false; _boxCursor = 2; return true; }
        if (key.Keycode == Key.Enter) { OpenStorageBoxOptions(); return true; }
        if (key.Keycode is Key.Left or Key.Right)
        { StorageAction(new("storage_box", Choice: (_session.State.Storage!.CurrentBox + (key.Keycode == Key.Right ? 1 : 13)) % 14)); return true; }
        if (key.Keycode is Key.Tab or Key.Backspace or Key.B) _storageTitleSelected = false;
        return key.Keycode == Key.Up;
    }

    private string[] StorageCustomizationMenuLabels() => _storageBoxOptionsOpen ? StorageBoxOptionSymbols.Select(StorageCustomizationText).ToArray()
        : _session.State.Pc!.Customization!.Kind == CampaignStorageCustomizationKind.WallpaperSets
            ? _storageCustomizationArt!.Categories.Select(c => c.Name).ToArray()
            : _storageCustomizationArt!.Categories.Single(c => c.Id == _session.State.Pc.Customization.WallpaperSet).WallpaperIds
                .Select(id => _storageCustomizationArt.Wallpapers.Single(w => w.Id == id).Name).ToArray();

    private Rect2 StorageCustomizationMenuFrame(string[] labels)
    {
        int width = (labels.Max(s => s.Length) + 2) * 8;
        return new(224 - width, 112 - labels.Length * 16, width + 16, labels.Length * 16 + 16);
    }

    private bool MouseStorageCustomization(Vector2 point)
    {
        if (_pcNotice is not null) return MouseConfirm(point, MouseMessage);
        if (StorageBoxNaming) return true;
        if (_storageJumpOpen)
        {
            if (new Rect2(88, 64, 24, 32).HasPoint(point)) return MouseKey(Key.Left);
            if (new Rect2(208, 64, 24, 32).HasPoint(point)) return MouseKey(Key.Right);
            return new Rect2(112, 64, 96, 32).HasPoint(point) && MouseKey();
        }
        if (_session.State.Pc!.Customization?.Kind == CampaignStorageCustomizationKind.Markings)
            return MouseSelect(point, ref _storageCustomizationCursor, 6, 184, 24, 48);
        var labels = StorageCustomizationMenuLabels(); var frame = StorageCustomizationMenuFrame(labels);
        return _storageBoxOptionsOpen ? MouseSelect(point, ref _storageBoxOptionCursor, labels.Length, frame.Position.X + 8, frame.Position.Y + 10, frame.Size.X - 16)
            : MouseSelect(point, ref _storageCustomizationCursor, labels.Length, frame.Position.X + 8, frame.Position.Y + 10, frame.Size.X - 16);
    }

    private bool NavigateStorageCustomizationPointer(Vector2 point, int direction)
    {
        if (_pcNotice is not null || StorageBoxNaming) return false;
        if (_storageJumpOpen) return direction != 0 && new Rect2(88, 64, 144, 32).HasPoint(point) && MouseNavigationKey(direction);
        if (_session.State.Pc!.Customization?.Kind == CampaignStorageCustomizationKind.Markings)
            return PointerRows(point, direction, ref _storageCustomizationCursor, 6, 184, 24, 48);
        var labels = StorageCustomizationMenuLabels(); var frame = StorageCustomizationMenuFrame(labels);
        return _storageBoxOptionsOpen ? PointerRows(point, direction, ref _storageBoxOptionCursor, labels.Length, frame.Position.X + 8, frame.Position.Y + 10, frame.Size.X - 16)
            : PointerRows(point, direction, ref _storageCustomizationCursor, labels.Length, frame.Position.X + 8, frame.Position.Y + 10, frame.Size.X - 16);
    }

    private bool MouseStorageBoxTitle(Vector2 point)
    {
        if (!StorageBoxTitleRect.HasPoint(point)) return false;
        _menuMouseActionFrame = Engine.GetProcessFrames(); OpenStorageBoxOptions(); return true;
    }

    private void DrawStorageWallpaperAndTitle()
    {
        var storage = _session.State.Storage!;
        var wallpaper = _storageCustomizationArt!.Wallpapers.Single(w => w.Id == PokemonStorageCustomizationRules.WallpaperId(storage, storage.CurrentBox));
        DrawTexture(_storageCustomizationTextures[wallpaper.AssetId], new(80, 16));
        DrawTexture(_storageAssets["box-arrow-left"], new(88, 20)); DrawTexture(_storageAssets["box-arrow-right"], new(224, 20));
        string name = StorageBoxName(storage.CurrentBox);
        float width = _storageTitleFont!.Font.GetStringSize(_storageTitleFont.PrepareText(name), fontSize: _storageTitleFont.Size).X;
        SourceText(_storageTitleFont, new(160 - (int)width / 2, 20), name);
        if (_storageTitleSelected) DrawRect(StorageBoxTitleRect, new Color("fff4b8"), filled: false);
    }

    private void DrawStorageMarkings(PartyPokemon? pokemon)
    {
        if (pokemon is not null) DrawTexture(_storageCustomizationTextures["marking-combo-" + pokemon.Markings], new(24, 146));
    }

    private void DrawStorageCustomizationOverlay()
    {
        if (!StorageCustomizationOverlayActive) return;
        if (StorageBoxNaming) { DrawNamingScreen(); return; }
        if (_storageJumpOpen)
        {
            ItemMessageBox(PcText("gText_JumpToWhichBox"));
            _battleSkin.DrawUserFrame(this, new(88, 56, 144, 48));
            SourceText(_sourceNormal, new(104, 70), "◀"); SourceText(_sourceNormal, new(120, 70), StorageBoxName(_storageJumpBox));
            SourceText(_sourceNormal, new(216, 70), "▶"); return;
        }
        var modal = _session.State.Pc!.Customization;
        if (modal?.Kind == CampaignStorageCustomizationKind.Markings)
        {
            ItemMessageBox(StorageCustomizationText("gText_MarkYourPkmn"));
            _battleSkin.DrawUserFrame(this, new(176, 16, 64, 112));
            for (int i = 0; i < 4; i++)
                DrawTexture(_storageCustomizationTextures[$"marking-{StorageMarkShapeNames[i]}-{((modal.Markings & 1 << i) != 0 ? "on" : "off")}"], new(204, 28 + i * 16));
            DrawTexture(_storageCustomizationTextures["marking-menu-ok-cancel"], new(192, 88));
            DrawTexture(_storageCustomizationTextures["marking-menu-cursor"], new(184, 28 + _storageCustomizationCursor * 16)); return;
        }
        string prompt = _storageBoxOptionsOpen ? "gText_WhatDoYouWantToDo" : modal!.Kind == CampaignStorageCustomizationKind.WallpaperSets ? "gText_PleasePickATheme" : "gText_PickTheWallpaper";
        ItemMessageBox(StorageCustomizationText(prompt));
        var labels = StorageCustomizationMenuLabels(); var frame = StorageCustomizationMenuFrame(labels);
        _battleSkin.DrawUserFrame(this, frame);
        for (int i = 0; i < labels.Length; i++) SourceText(_sourceNormal, frame.Position + new Vector2(16, 10 + i * 16), labels[i]);
        SourceText(_sourceNormal, frame.Position + new Vector2(8, 10 + (_storageBoxOptionsOpen ? _storageBoxOptionCursor : _storageCustomizationCursor) * 16), "▶");
    }
}
