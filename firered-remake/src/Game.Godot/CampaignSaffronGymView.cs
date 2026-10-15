using System.Text;
using System.Text.Json;
using Game.Content;
using Godot;

// Source-authenticated Saffron art and bounded presentation projection.
public partial class CampaignView
{
    private readonly Dictionary<string, string> _saffronGymTrainerPortraits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _saffronGymTrainerClasses = new(StringComparer.Ordinal);
    private bool IsSaffronGymLeader(string id) => _catalog.SaffronGymEnabled && id == "pret:TRAINER_LEADER_SABRINA";

    private void LoadSaffronGymPresentation()
    {
        if (!_catalog.SaffronGymEnabled) return;
        var art = _catalog.SaffronGymArt ?? throw new InvalidDataException("Saffron Gym art catalog missing.");
        foreach (var sprite in art.Sprites)
        {
            using var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, PngRgba8.Decode(art.Files[sprite.TexturePath], sprite.Width, sprite.Height));
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = PngRgba8.Decode(art.Files[icon.TexturePath], icon.Width, icon.Height);
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = Image.CreateFromData(footprint.Width, footprint.Height, false, Image.Format.Rgba8, PngRgba8.Decode(art.Files[footprint.TexturePath], footprint.Width, footprint.Height));
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (var sprite in art.Sprites.Where(s => s.Side == "front" && s.PaletteVariant == "normal"))
            _portraits.Add(sprite.SourceSpeciesId, _battleArt[(sprite.SourceSpeciesId, "front", false)].Texture);
        LoadSaffronGymTrainerArt(); LoadSaffronGymInterface();
        foreach (var icon in _catalog.SaffronGymItems!.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, SilphItemDisplayRgba(icon));
            _earlyItemIcons.Add(icon.Item, (ImageTexture)SceneTextures.FromImage(image));
        }
        if (!_trainerCard!.Badges.Any(b => b.Flag == "FLAG_BADGE06_GET" && _trainerCardTextures.ContainsKey(b.AssetId)))
            throw new InvalidDataException("Original Marsh Badge card pixels missing.");
        Write("saffronGymPresentation", new { enemyOnlySpecies = new[] { 65, 80, 122 }, playableMaps = _catalog.SaffronGymRegion!.NewMaps.Count,
        reusedPadAnimation = true, badgeFlag = "FLAG_BADGE06_GET", audibleOutputVerified = false });
    }
    private byte[] ReadSaffronPresentation(string path)
    {
        if (System.IO.Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Invalid Saffron presentation path.");
        var file = new FileInfo(System.IO.Path.Combine(_root, path));
        if (file.LinkTarget is not null) throw new InvalidDataException("Saffron presentation symlink rejected.");
        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
            if (directory.LinkTarget is not null) throw new InvalidDataException("Saffron presentation parent symlink rejected.");
        byte[] bytes = System.IO.File.ReadAllBytes(file.FullName);
        return bytes;
    }
    private void LoadSaffronGymInterface()
    {

        using var json = JsonDocument.Parse(ReadSaffronPresentation("content/generated/saffron-gym-interface/manifest.json"));
        var root = json.RootElement;
        if (root.GetProperty("item").GetString() != "pret:ITEM_MAX_REPEL")
            throw new InvalidDataException("Saffron interface source contract differs.");
        byte[]? rgba = null;
        foreach (var row in root.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            byte[] bytes = ReadSaffronPresentation("content/generated/saffron-gym-interface/" + path);
            if (path == "max-repel.png") rgba = PngRgba8.Decode(bytes, 24, 24);
        }
        if (rgba is null || rgba.Length != 24 * 24 * 4)
            throw new InvalidDataException("Max Repel source pixels differ.");
        using (var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, rgba))
            _earlyItemIcons.Add(new("pret:ITEM_MAX_REPEL"), (ImageTexture)SceneTextures.FromImage(image));
        foreach (var row in root.GetProperty("trainerClasses").EnumerateObject()) _saffronGymTrainerClasses.Add(row.Name, row.Value.GetString()!);
    }
    private string? VisibleSaffronGymTrainerClass() => _session.State.ForestTrainerBattle is { } battle && _catalog.SaffronGymTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
        ? _saffronGymTrainerClasses[trainer.TrainerClass] : null;

    private void LoadSaffronGymTrainerArt()
    {

        byte[] Read(string path) => ReadSaffronPresentation(path);
        using var document = JsonDocument.Parse(Read("content/generated/saffron-gym-trainer-art/manifest.json"));
        var root = document.RootElement;
        var files = root.GetProperty("outputs").EnumerateArray().ToDictionary(o => o.GetProperty("path").GetString()!,
            o => Read("content/generated/saffron-gym-trainer-art/" + o.GetProperty("path").GetString()), StringComparer.Ordinal);
        foreach (var row in root.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!, trainerPic = row.GetProperty("trainerPic").GetString()!;
            byte[] pixels = PngRgba8.Decode(files[row.GetProperty("texturePath").GetString()!]);
            if (row.GetProperty("width").GetInt32() != 64 || row.GetProperty("height").GetInt32() != 64 || pixels.Length != 64 * 64 * 4) throw new InvalidDataException("Saffron Gym trainer pixels differ.");
            if(!(_forestTrainerPortraits.TryGetValue(id, out var old))){ using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, pixels); _forestTrainerPortraits.Add(id, (ImageTexture)SceneTextures.FromImage(image)); }
            _saffronGymTrainerPortraits.Add(trainerPic, id);
        }
        _saffronGymTrainerPortraits.Add("TRAINER_PIC_CHANNELER", "channeler-front");
        if (!_forestTrainerPortraits.ContainsKey("channeler-front")) throw new InvalidDataException("Inherited source Channeler portrait missing.");
        foreach (var trainer in _catalog.SaffronGymTrainers!.Trainers.Values)
            if (!_saffronGymTrainerPortraits.ContainsKey(trainer.TrainerPic)) throw new InvalidDataException("Saffron Gym trainer portrait missing: " + trainer.TrainerPic);
    }
    private string? VisibleSaffronGymTrainerPortrait() => _session.State.ForestTrainerBattle is { } battle && _catalog.SaffronGymTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
        ? _saffronGymTrainerPortraits[trainer.TrainerPic] : null;
}
