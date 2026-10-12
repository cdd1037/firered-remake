using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Godot;

// Source-authenticated Saffron art and bounded presentation projection.
public partial class CampaignView
{
    private static bool SaffronGymProfileRequested => OS.GetEnvironment("FIRERED_SAFFRON_GYM") == "1" || OS.GetCmdlineUserArgs().Contains("--story-saffron-gym");
    private readonly Dictionary<string, string> _saffronGymTrainerPortraits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _saffronGymTrainerClasses = new(StringComparer.Ordinal);
    private bool IsSaffronGymLeader(string id) => _catalog.SaffronGymEnabled && id == "pret:TRAINER_LEADER_SABRINA";

    private void LoadSaffronGymPresentation()
    {
        if (!_catalog.SaffronGymEnabled) return;
        var art = _catalog.SaffronGymArt ?? throw new InvalidDataException("Saffron Gym art catalog missing.");
        if (_battleArt.Count != 460 || _storageIcons.Count != 115 || _route22Footprints.Count != 86)
            throw new InvalidDataException("Saffron Gym requires the complete Silph presentation.");
        foreach (var sprite in art.Sprites)
        {
            using var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, art.Files[sprite.RgbaPath]);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = art.Files[icon.RgbaPath].ToArray();
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = Image.CreateFromData(footprint.Width, footprint.Height, false, Image.Format.Rgba8, art.Files[footprint.RgbaPath]);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (var sprite in art.Sprites.Where(s => s.Side == "front" && s.PaletteVariant == "normal"))
            _portraits.Add(sprite.SourceSpeciesId, _battleArt[(sprite.SourceSpeciesId, "front", false)].Texture);
        if (_battleArt.Count != 464 || _storageIcons.Count != 116 || _route22Footprints.Count != 87)
            throw new InvalidDataException("Saffron Gym enemy presentation inventory differs.");
        LoadSaffronGymTrainerArt(); LoadSaffronGymInterface();
        foreach (var icon in _catalog.SaffronGymItems!.Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, SilphItemDisplayRgba(icon));
            _earlyItemIcons.Add(icon.Item, (ImageTexture)SceneTextures.FromImage(image));
        }
        if (!_trainerCard!.Badges.Any(b => b.Flag == "FLAG_BADGE06_GET" && _trainerCardTextures.ContainsKey(b.AssetId)))
            throw new InvalidDataException("Original Marsh Badge card pixels missing.");
        Write("saffronGymPresentation", new { ownedSpecies = 113, enemyOnlySpecies = new[] { 65, 80, 122 }, battleSprites = 464,
            icons = 116, footprints = 87, playableMaps = _catalog.SaffronGymRegion!.NewMaps.Count,
            PretSaffronGymArt.ManifestSha256, reusedPadAnimation = true, badgeFlag = "FLAG_BADGE06_GET", audibleOutputVerified = false });
    }
    private byte[] ReadSaffronPresentation(string path, string hash, int size)
    {
        if (System.IO.Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Invalid Saffron presentation path.");
        var file = new FileInfo(System.IO.Path.Combine(_root, path));
        if (file.LinkTarget is not null) throw new InvalidDataException("Saffron presentation symlink rejected.");
        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
            if (directory.LinkTarget is not null) throw new InvalidDataException("Saffron presentation parent symlink rejected.");
        if (file.Length != size || size <= 0 || size > 4 * 1024 * 1024) throw new InvalidDataException("Saffron presentation length differs.");
        byte[] bytes = System.IO.File.ReadAllBytes(file.FullName);
        if (bytes.Length != size || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Saffron presentation identity differs: " + path);
        return bytes;
    }
    private void LoadSaffronGymInterface()
    {
        const string lockHash = "60d721dcc902aced2516b80e8c388c8ea5cd00b49d1f2220206f1fa258aeff21";
        _ = ReadSaffronPresentation("scripts/saffron-gym-interface.lock.json", lockHash, 1182);
        using var json = JsonDocument.Parse(ReadSaffronPresentation("content/generated/saffron-gym-interface/manifest.json", "01579aa3d396f9785d96b218427aff8770275d946079fcf7de020c8da72911e2", 1821));
        var root = json.RootElement;
        if (root.GetProperty("sourceCommit").GetString() != PretWorldMap.SourceCommit || root.GetProperty("lockSha256").GetString() != lockHash ||
            root.GetProperty("inputs").GetArrayLength() != 5 || root.GetProperty("item").GetString() != "pret:ITEM_MAX_REPEL")
            throw new InvalidDataException("Saffron interface source contract differs.");
        foreach (var row in root.GetProperty("inputs").EnumerateArray())
        {
            byte[] bytes = ReadSaffronPresentation("content/raw/saffron-gym-interface/" + row.GetProperty("path").GetString(), row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Saffron interface source Git blob differs.");
        }
        byte[]? rgba = null;
        foreach (var row in root.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            byte[] bytes = ReadSaffronPresentation("content/generated/saffron-gym-interface/" + path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (path == "max-repel.rgba") rgba = bytes;
        }
        if (rgba is null || rgba.Length != 24 * 24 * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != root.GetProperty("rgbaSha256").GetString())
            throw new InvalidDataException("Max Repel source pixels differ.");
        using (var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, rgba))
            _earlyItemIcons.Add(new("pret:ITEM_MAX_REPEL"), (ImageTexture)SceneTextures.FromImage(image));
        foreach (var row in root.GetProperty("trainerClasses").EnumerateObject()) _saffronGymTrainerClasses.Add(row.Name, row.Value.GetString()!);
    }
    private string? VisibleSaffronGymTrainerClass() => _session.State.ForestTrainerBattle is { } battle && _catalog.SaffronGymTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
        ? _saffronGymTrainerClasses[trainer.TrainerClass] : null;

    private void LoadSaffronGymTrainerArt()
    {
        const string manifestHash = "76c86b61199e9afc734d8177cb19c13917882dbaa72bce4d93a1bbb761804c0f";
        const string lockHash = "fa1f08584e533ca0de9d0b53f5929ffb42d0adf6e4ee005664ee8213ee3be9bb";
        byte[] Read(string path, string hash, int size) => ReadSaffronPresentation(path, hash, size);
        _ = Read("scripts/saffron-gym-presentation.lock.json", lockHash, 6432);
        using var document = JsonDocument.Parse(Read("content/generated/saffron-gym-trainer-art/manifest.json", manifestHash, 8170));
        var root = document.RootElement;
        if (root.GetProperty("sourceCommit").GetString() != PretWorldMap.SourceCommit || root.GetProperty("lockSha256").GetString() != lockHash ||
            root.GetProperty("portraits").GetArrayLength() != 2 || root.GetProperty("inputs").GetArrayLength() != 31)
            throw new InvalidDataException("Saffron Gym trainer art source contract differs.");
        foreach (var source in root.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/saffron-gym-presentation/" + source.GetProperty("path").GetString(), source.GetProperty("sha256").GetString()!, source.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != source.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Saffron Gym trainer source Git blob differs.");
        }
        var files = root.GetProperty("outputs").EnumerateArray().ToDictionary(o => o.GetProperty("path").GetString()!,
            o => Read("content/generated/saffron-gym-trainer-art/" + o.GetProperty("path").GetString(), o.GetProperty("sha256").GetString()!, o.GetProperty("byteLength").GetInt32()), StringComparer.Ordinal);
        foreach (var row in root.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!, trainerPic = row.GetProperty("trainerPic").GetString()!;
            byte[] pixels = files[row.GetProperty("rgbaPath").GetString()!];
            if (row.GetProperty("width").GetInt32() != 64 || row.GetProperty("height").GetInt32() != 64 || pixels.Length != 64 * 64 * 4 ||
                Convert.ToHexStringLower(SHA256.HashData(pixels)) != row.GetProperty("rgbaSha256").GetString()) throw new InvalidDataException("Saffron Gym trainer pixels differ.");
            if (_forestTrainerPortraits.TryGetValue(id, out var old))
            { using var current = old.GetImage(); current.Convert(Image.Format.Rgba8); if (!current.GetData().SequenceEqual(pixels)) throw new InvalidDataException("Inherited trainer pixels differ: " + id); }
            else { using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, pixels); _forestTrainerPortraits.Add(id, (ImageTexture)SceneTextures.FromImage(image)); }
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
