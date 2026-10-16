using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private SeviiRubyPresentationData? _seviiRubyArt;
    private readonly Dictionary<string,JsonElement> _seviiRubyTrainers = new(StringComparer.Ordinal);
    private readonly Dictionary<int,PokedexMenuEntry> _seviiRubyDex = [];
    private readonly Dictionary<(string Map,int Layer),Texture2D> _seviiRubyTileLayers = [];
    private Texture2D? _seviiRubyMountain, _seviiRubyBrailleFont, _seviiRubyBrailleCursor, _seviiRubyBrailleFrame;
    private JsonElement SeviiRubyManifest => _seviiRubyArt?.Manifest ?? throw new InvalidDataException("Sevii Ruby presentation missing.");

    private void LoadSeviiRubyPresentation()
    {
        _seviiRubyArt = SeviiRubyPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false) => PresentationTexture(_seviiRubyArt.Files, row, replicatePalette);
        foreach (var row in SeviiRubyManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("Sevii Ruby sprite is outside the owned catalog.");
            string side = row.GetProperty("side").GetString()!; bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny)
            {
                _portraits.Add(species, texture);
                _seviiRubyDex.Add(species, (_pokedexMenu ?? throw new InvalidDataException("Sevii Ruby dex metadata missing.")).Entries[species] with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in SeviiRubyManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!), Pixels(row, replicatePalette: true));
        foreach (var row in SeviiRubyManifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            _route22Footprints.Add(species, Pixels(row));
            _seviiRubyDex[species] = _seviiRubyDex[species] with { Footprint = SeviiRubyDexTexture(row) };
        }
        foreach (var row in SeviiRubyManifest.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            if(!(_forestTrainerPortraits.TryGetValue(id, out var inherited)))_forestTrainerPortraits.Add(id, (ImageTexture)Pixels(row));
        }
        foreach (var row in SeviiRubyManifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            var trainer = _catalog.SeviiRubyTrainers?.Trainers.GetValueOrDefault(new(id)) ??
                throw new InvalidDataException("Sevii Ruby trainer is not admitted.");
            if (trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() || trainer.SourceTrainerName != row.GetProperty("name").GetString() ||
                trainer.TrainerClass != row.GetProperty("trainerClass").GetString() || trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                trainer.DoubleBattle != row.GetProperty("doubleBattle").GetBoolean() || trainer.EncounterMusicGender != row.GetProperty("encounterMusicGender").GetInt32() || !_forestTrainerPortraits.ContainsKey(row.GetProperty("portrait").GetString()!))
                throw new InvalidDataException("Sevii Ruby trainer presentation binding differs.");
            _seviiRubyTrainers.Add(id, row);
        }
        foreach (var row in SeviiRubyManifest.GetProperty("items").EnumerateArray())
        {
            string item = row.GetProperty("item").GetString()!, asset = "sevii-ruby-" + item[5..].ToLowerInvariant();
            var icon = (ImageTexture)Pixels(row);
            if(!(_earlyItemIcons.TryGetValue(new(item), out var inherited)))_earlyItemIcons.Add(new(item), icon);
            if (row.GetProperty("pocket").GetString() != "POCKET_KEY_ITEMS") continue;
            var document = _questPocketUi ?? throw new InvalidDataException("Sevii Ruby key-item pocket missing.");
            if (document.Items.Any(i => i.Id == item)) throw new InvalidDataException("Duplicated Sevii Ruby key item.");
            var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
            using var image = icon.GetImage(); _questPocketTextures.Add(asset, SceneTextures.FromImage(image));
            _questPocketUi = document with
            {
                Items = document.Items.Add(new(item, item[5..], row.GetProperty("name").GetString()!, row.GetProperty("description").GetString()!, asset, row.GetProperty("fieldUseFunction").GetString()!)),
                Assets = document.Assets.Add(new(asset,row.GetProperty("texturePath").GetString()!,24,24,position.X,position.Y))
            };
        }
        foreach (var row in SeviiRubyManifest.GetProperty("layers").EnumerateArray())
            _seviiRubyTileLayers.Add((row.GetProperty("map").GetString()!, row.GetProperty("layer").GetInt32()), Pixels(row));
        _seviiRubyMountain = Pixels(SeviiRubyManifest.GetProperty("terrains").EnumerateArray().Single(t => t.GetProperty("terrainId").GetInt32() == 6));
        var braille = SeviiRubyManifest.GetProperty("braille");
        _seviiRubyBrailleFont = Pixels(braille.GetProperty("asset"));
        _seviiRubyBrailleCursor = Pixels(braille.GetProperty("cursor"));
        _seviiRubyBrailleFrame = Pixels(braille.GetProperty("frame"));

    }

    private static PokedexMenuTexture SeviiRubyDexTexture(JsonElement row) => new(row.GetProperty("texturePath").GetString()!,row.GetProperty("width").GetInt32(),row.GetProperty("height").GetInt32());
    private CaptureDexEntry? SeviiRubyCaptureDexEntry(int species)
    {
        if (!_seviiRubyDex.TryGetValue(species, out var entry)) return null;
        return new(entry.SourceSpeciesId, entry.NationalNumber, entry.Name, entry.Category, entry.DisplayCategory,
            entry.HeightDecimeters, entry.WeightHectograms, entry.HeightDisplay, entry.WeightDisplay, entry.Description,
            entry.Footprint!.TexturePath, entry.Portrait!.TexturePath, entry.HabitatTitle);
    }
    private byte[]? ReadSeviiRubyDexTexture(PokedexMenuTexture texture) => _seviiRubyDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture)
        ? _seviiRubyArt!.Files[texture.TexturePath] : null;
    private string? SeviiRubyTrainerText(string? id, string key) => id is not null && _seviiRubyTrainers.TryGetValue(id, out var row) ? row.GetProperty(key).GetString() : null;
    private string? SeviiRubyDoubleText(string key) => SeviiRubyTrainerText(_session.State.DoubleBattle?.Trainer.Value, key);
    private Texture2D? SeviiRubyDoublePortrait => SeviiRubyDoubleText("portrait") is {} id ? _forestTrainerPortraits[id] : null;
    private bool IsSeviiRubyPatchedMap(MapId map) => map.Value is "map.mt-ember-exterior" or "map.one-island-pokemon-center-1f";
    private bool TrySeviiRubyPatch(Image canvas, FieldMetatileEdit edit, int layer, MapId map)
    {
        if (!IsSeviiRubyPatchedMap(map)) return false;
        var tile = SeviiRubyManifest.GetProperty("tiles").EnumerateArray().SingleOrDefault(t => t.GetProperty("map").GetString() == map.Value && t.GetProperty("metatileId").GetInt32() == edit.SourceMetatileId);
        if (tile.ValueKind == JsonValueKind.Undefined || tile.GetProperty("x").GetInt32() != edit.X || tile.GetProperty("y").GetInt32() != edit.Y)
            throw new InvalidDataException("Sevii Ruby dynamic metatile is outside the authenticated source patch.");
        using var image = _seviiRubyTileLayers[(map.Value, layer)].GetImage();
        canvas.BlitRect(image, new(0, 0, 16, 16), new(edit.X * 16, edit.Y * 16));
        return true;
    }
    private bool SeviiRubyBrailleVisible => FieldDialogueVisible &&
        _session.State.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) == true;
    private bool TrySeviiRubyBrailleInput(InputEventKey key)
    {
        if (!SeviiRubyBrailleVisible || _session.State.Script.Wait?.Kind != StoryWaitKind.ButtonPress ||
            key.Keycode is Key.Escape or Key.F12 or Key.F5 or Key.F9) return false;
        _held = null; _repeat = 0;
        if (key.Pressed && !key.Echo && key.Keycode is Key.Enter or Key.Backspace &&
            _waitElapsed >= _waitDuration && _tracks.All(t => t.Time >= t.Duration))
            Apply(new("button_press", Text: key.Keycode == Key.Enter ? "A" : "B"));
        return true;
    }
    private bool DrawSeviiRubyBraille()
    {
        if (!SeviiRubyBrailleVisible) return false;
        var braille = SeviiRubyManifest.GetProperty("braille");
        string symbol = _session.State.Dialogue!.TextSymbol;
        var message = SixIslandBrailleMessage(symbol) ?? braille.GetProperty("messages").EnumerateArray().SingleOrDefault(m => m.GetProperty("label").GetString() == symbol);
        if (message.ValueKind == JsonValueKind.Undefined || message.GetProperty("text").GetString() != _session.VisibleText)
            throw new InvalidDataException("Braille text differs from the authenticated source bank.");
        string text = message.GetProperty("text").GetString()!;
        var frame = braille.GetProperty("frame");
        DrawTexture(_seviiRubyBrailleFrame!, new(frame.GetProperty("x").GetInt32(), frame.GetProperty("y").GetInt32()));
        int x = braille.GetProperty("textX").GetInt32(), y = braille.GetProperty("textY").GetInt32();
        foreach (char character in text)
        {
            var glyph = braille.GetProperty("glyphs").EnumerateArray().Single(g => g.GetProperty("character").GetString() == character.ToString());
            DrawTextureRectRegion(_seviiRubyBrailleFont!, new(x,y,16,16), new(glyph.GetProperty("x").GetInt32(),glyph.GetProperty("y").GetInt32(),16,16));
            x += braille.GetProperty("glyphWidth").GetInt32();
        }
        bool cursorVisible = _session.State.Script.Variables.GetValueOrDefault("VAR_0x8006") == 0 &&
            _session.State.PresentationEffects.Any(e => e.Opcode == "special" && e.Args.SequenceEqual(new[]{"BrailleCursorToggle"}));
        if (cursorVisible)
        {
            var cursor = braille.GetProperty("cursor");
            // Source cursor starts at y2=0 and emits the first zero phase after its initial delay.
            int phase = Math.Max(0, (int)Math.Floor(_waitElapsed * 60) / cursor.GetProperty("ticksPerOffset").GetInt32() - 1) % 4;
            DrawTexture(_seviiRubyBrailleCursor!, new(text.Length * 16 + cursor.GetProperty("xOffset").GetInt32(),
                cursor.GetProperty("y").GetInt32() + cursor.GetProperty("bobOffsets")[phase].GetInt32()));
        }
        return true;
    }
    private Color SourceScreenFadeColor
    {
        get
        {
            var fade = _session.State.PresentationEffects.LastOrDefault(e => e.Opcode is "fadescreen" or "fadescreenspeed");
            bool white = fade?.Args.FirstOrDefault() is "FADE_TO_WHITE" or "FADE_FROM_WHITE" or "2" or "3";
            return white ? new Color(1,1,1,PokemonLeagueFieldFadeAlpha) : new Color(0,0,0,PokemonLeagueFieldFadeAlpha);
        }
    }
    private void DisposeSeviiRubyPresentation()
    {
        _seviiRubyMountain = null; _seviiRubyBrailleFont = null; _seviiRubyBrailleCursor = null; _seviiRubyBrailleFrame = null;
        _seviiRubyArt = null; _seviiRubyTrainers.Clear(); _seviiRubyDex.Clear(); _seviiRubyTileLayers.Clear();
        foreach (string key in _questPocketTextures.Keys.Where(k => k.StartsWith("sevii-ruby-", StringComparison.Ordinal)).ToArray())
        { _questPocketTextures.Remove(key); }
    }
}
