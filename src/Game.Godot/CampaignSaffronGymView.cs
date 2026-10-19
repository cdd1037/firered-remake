using System.Text;
using System.Text.Json;
using Game.Content;
using Godot;

// Source-authenticated Saffron art and bounded presentation projection.
public partial class CampaignView
{
    private readonly Dictionary<string, string> _saffronGymTrainerPortraits = new(StringComparer.Ordinal);
    private bool IsSaffronGymLeader(string id) => id == "pret:TRAINER_LEADER_SABRINA";

    private void LoadSaffronGymPresentation()
    {
        var art = _catalog.SaffronGymArt ?? throw new InvalidDataException("Saffron Gym art catalog missing.");
        RegisterPokemonArt(art.Sprites, art.Icons, art.Footprints, art.Files);

        LoadSaffronGymTrainerArt(); LoadSaffronGymInterface();
        foreach (var icon in _catalog.SaffronGymItems!.Icons)
        {
            _earlyItemIcons.Add(icon.Item, ItemTexture("data/regions/saffron-gym", icon));
        }
        if (!_trainerCard!.Badges.Any(b => b.Flag == "FLAG_BADGE06_GET" && _trainerCardTextures.ContainsKey(b.AssetId)))
            throw new InvalidDataException("Original Marsh Badge card pixels missing.");

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

        using var json = JsonDocument.Parse(ReadSaffronPresentation("content/generated/ui/saffron-gym-interface/manifest.json"));
        var root = json.RootElement;
        if (root.GetProperty("item").GetString() != "pret:ITEM_MAX_REPEL")
            throw new InvalidDataException("Saffron interface source contract differs.");
        byte[]? rgba = null;
        foreach (var row in root.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            byte[] bytes = ReadSaffronPresentation("content/generated/ui/saffron-gym-interface/" + path);
            if (path == "max-repel.png") rgba = NativeImages.ReadPixels(bytes, 24, 24);
        }
        if (rgba is null || rgba.Length != 24 * 24 * 4)
            throw new InvalidDataException("Max Repel source pixels differ.");
        using (var image = Image.CreateFromData(24, 24, false, Image.Format.Rgba8, rgba))
            _earlyItemIcons.Add(new("pret:ITEM_MAX_REPEL"), (ImageTexture)SceneTextures.FromImage(image));
    }
    private void LoadSaffronGymTrainerArt()
    {

        byte[] Read(string path) => ReadSaffronPresentation(path);
        using var document = JsonDocument.Parse(Read("content/generated/sprites/trainers/saffron-gym/manifest.json"));
        var root = document.RootElement;
        var files = root.GetProperty("outputs").EnumerateArray().ToDictionary(o => o.GetProperty("path").GetString()!,
            o => Read("content/generated/sprites/trainers/saffron-gym/" + o.GetProperty("path").GetString()), StringComparer.Ordinal);
        foreach (var row in root.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!, trainerPic = row.GetProperty("trainerPic").GetString()!;
            byte[] png = files[row.GetProperty("texturePath").GetString()!];
            if (row.GetProperty("width").GetInt32() != 64 || row.GetProperty("height").GetInt32() != 64) throw new InvalidDataException("Saffron Gym trainer pixels differ.");
            if(!(_forestTrainerPortraits.TryGetValue(id, out var old))){ using var image = NativeImages.LoadPng(png, 64, 64); _forestTrainerPortraits.Add(id, (ImageTexture)SceneTextures.FromImage(image)); }
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
