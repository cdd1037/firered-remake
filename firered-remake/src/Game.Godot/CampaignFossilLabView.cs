using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Godot;

/// <summary>Source fossil species art and exact two/three-row fossil selection menus.</summary>
public partial class CampaignView
{
    private FossilLabPresentationData? _fossilLabArt;
    private string? _fossilLabMenuWait;
    private int _fossilLabCursor;

    private void LoadFossilLabPresentation()
    {
        if (!_catalog.FossilLabEnabled) return;
        if (!_session.IsCurrentGame || !_catalog.FightingDojoEnabled)
            throw new InvalidDataException("Fossil Lab requires its current Fighting Dojo presentation predecessor.");
        var art = PretFossilLabPresentation.Load(_root);
        Texture2D Pixels(JsonElement row)
        {
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32());
            return SceneTextures.FromImage(image);
        }
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string id = row.GetProperty("speciesId").GetString()!, side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId != species)
                throw new InvalidDataException("Fossil species art is not admitted by the owned catalog.");
            var texture = Pixels(row);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!), Pixels(row));
        // League already installed the four Aerodactyl battlers, portrait and icon.
        // Verify those exact textures without registering the same keys twice.
        void RequireInheritedPixels(Texture2D texture, JsonElement row)
        {
            using var image = texture.GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != row.GetProperty("width").GetInt32() || image.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("Inherited Aerodactyl pixels differ.");
        }
        foreach (var row in art.Manifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string id = row.GetProperty("speciesId").GetString()!, side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId != species ||
                !_battleArt.TryGetValue((species, side, shiny), out var existing) ||
                existing.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Owned Aerodactyl League binding differs.");
            RequireInheritedPixels(existing.Texture, row);
        }
        foreach (var row in art.Manifest.GetProperty("inheritedPokemonIcons").EnumerateArray())
            RequireInheritedPixels(_storageIcons[new(row.GetProperty("speciesId").GetString()!)], row);
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
            _route22Footprints.Add(row.GetProperty("sourceSpeciesId").GetInt32(), Pixels(row));
        foreach (var entry in art.DexEntries.Values)
            if (_pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Fossil Lab dex metadata differs from the authenticated source menu.");
        _fossilLabArt = art;
        LoadOldAmberPocket(art);
        Write("fossilLabPresentation", new {             oldAmberPocket = true,
            nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void LoadOldAmberPocket(FossilLabPresentationData art)
    {
        var source = art.Manifest.GetProperty("oldAmber");
        var icon = source.GetProperty("icon");
        string id = source.GetProperty("id").GetString()!, assetId = icon.GetProperty("id").GetString()!;
        var item = _catalog.EarlyShop!.Items.Single(i => i.Id.Value == id);
        if (item.SourceItemId != 354 || item.SourceItemSymbol != source.GetProperty("sourceSymbol").GetString() ||
            item.Name != source.GetProperty("name").GetString() || item.Description != source.GetProperty("description").GetString() ||
            item.FieldUseFunction != source.GetProperty("fieldUseFunction").GetString())
            throw new InvalidDataException("Old Amber bag presentation differs from its admitted item.");
        var document = _questPocketUi ?? throw new InvalidDataException("Old Amber requires the current Key Items pocket.");
        if (document.Items.Any(i => i.Id == id)) throw new InvalidDataException("Old Amber bag presentation is duplicated.");
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        using var image = NativeImages.LoadPng(art.Files[icon.GetProperty("texturePath").GetString()!], 24, 24);
        _questPocketTextures.Add(assetId, SceneTextures.FromImage(image));
        _questPocketUi = document with
        {
            Items = document.Items.Add(new(id, item.SourceItemSymbol, item.Name, source.GetProperty("description").GetString()!, assetId, item.FieldUseFunction)),
            Assets = document.Assets.Add(new(assetId,icon.GetProperty("texturePath").GetString()!,24,24,position.X,position.Y))
        };
    }

    private void VerifyFossilLabMoveGeometry()
    {
        if (_fossilLabArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Fossil Lab inherited move geometry differs.");
        }
    }
    private CaptureDexEntry? FossilLabCaptureDexEntry(int species) => _fossilLabArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle);
    private PokedexMenuPage? FossilLabPokedexPage(int species, PokedexMenuPage page) =>
        _fossilLabArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadFossilLabDexTexture(PokedexMenuTexture texture) => _fossilLabArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private void DisposeFossilLabPresentation()
    {
        _fossilLabArt = null; _fossilLabMenuWait = null; _fossilLabCursor = 0;
    }

    private bool FossilLabMenuActive => _catalog.FossilLabEnabled && _session.State.FossilMenu is not null;
    private bool FossilLabMenuVisible => FossilLabMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);
    private int FossilLabMenuWidth => ((int)_session.State.FossilMenu!.Options.Max(row =>
        _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(row), fontSize: _sourceNormal.Size).X) + 9) / 8 * 8 + 8;
    private int FossilLabMenuCount => _session.State.FossilMenu!.Options.Length;
    private int FossilLabMenuHeight => (_fossilLabArt ?? throw new InvalidDataException("Fossil menu source missing.")).Manifest
        .GetProperty("menus").EnumerateArray().Single(m => m.GetProperty("menuId").GetString() == _session.State.FossilMenu!.MenuId)
        .GetProperty("interiorHeightTiles").GetInt32() * 8 + 16;
    private void SynchronizeFossilLabPresentation()
    {
        string? wait = FossilLabMenuActive ? _session.State.Script.Wait?.CorrelationId : null;
        if (wait != _fossilLabMenuWait) { _fossilLabMenuWait = wait; _fossilLabCursor = 0; }
        if (!FossilLabMenuActive) return;
        var menu = _session.State.FossilMenu!;
        var row = (_fossilLabArt ?? throw new InvalidDataException("Fossil menu source missing.")).Manifest.GetProperty("menus")
            .EnumerateArray().SingleOrDefault(m => m.GetProperty("menuId").GetString() == menu.MenuId);
        if (row.ValueKind == JsonValueKind.Undefined || menu.Left != 0 || menu.Top != 0 ||
            !menu.Options.SequenceEqual(row.GetProperty("rows").EnumerateArray().Select(r => r.GetString()!)) ||
            _session.State.Script.Wait?.Kind != StoryWaitKind.MenuChoice)
            throw new InvalidDataException("Runtime fossil menu differs from the source.");
    }
    private bool TryFossilLabInput(InputEventKey key)
    {
        if (!FossilLabMenuActive || key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !FossilLabMenuVisible || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration || _fanfareRemaining > 0) return true;
        // Original Menu_ProcessInputNoWrapAround applies to every menu with at most three rows.
        if (key.Keycode == Key.Up) _fossilLabCursor = Math.Max(0, _fossilLabCursor - 1);
        else if (key.Keycode == Key.Down) _fossilLabCursor = Math.Min(FossilLabMenuCount - 1, _fossilLabCursor + 1);
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _fossilLabCursor));
        else if (key.Keycode == Key.Backspace) Apply(new("ack", Choice: 127));
        return true;
    }
    private bool NavigateFossilLabPointer(Vector2 point, int direction) => FossilLabMenuVisible &&
        PointerRows(point, direction, ref _fossilLabCursor, FossilLabMenuCount, 8, 8, FossilLabMenuWidth, 14);
    private bool MouseFossilLab(Vector2 point) => FossilLabMenuVisible &&
        MouseSelect(point, ref _fossilLabCursor, FossilLabMenuCount, 8, 8, FossilLabMenuWidth, 14);
    private void DrawFossilLabMenu()
    {
        if (!FossilLabMenuVisible) return;
        _battleSkin.DrawUserFrame(this, new(0, 0, FossilLabMenuWidth + 16, FossilLabMenuHeight));
        for (int row = 0; row < FossilLabMenuCount; row++) SourceText(_sourceNormal, new(16, 10 + row * 14), _session.State.FossilMenu!.Options[row]);
        SourceText(_sourceNormal, new(8, 10 + _fossilLabCursor * 14), "▶");
    }
}
