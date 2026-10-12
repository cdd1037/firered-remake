using System.Security.Cryptography;
using System.Text.Json;

internal sealed record VictoryRoadTrainerPresentation(string Trainer, int SourceTrainerId, string Name,
    string TrainerClass, string ClassName, string TrainerPic, string Portrait, int EncounterMusicGender, bool DoubleBattle);
internal sealed record VictoryRoadPortraitPresentation(string Id, int SourceTrainerPicId, string RgbaPath, bool Inherited);
internal sealed record VictoryRoadIconPresentation(string Item, string RgbaPath);
internal sealed record VictoryRoadPokemonSpritePresentation(string SpeciesId, int SourceSpeciesId, string Side,
    string PaletteVariant, string RgbaPath, string TexturePath, string Sha256, string RgbaSha256, int X, int Y);
internal sealed record VictoryRoadPokemonIconPresentation(string SpeciesId, int SourceSpeciesId, string RgbaPath);
internal sealed record VictoryRoadTilePresentation(int MetatileId, int X, int Y);
internal sealed record VictoryRoadLayerPresentation(int Layer, string RgbaPath, int Width, int Height);
internal sealed record VictoryRoadPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, VictoryRoadTrainerPresentation> Trainers, VictoryRoadPortraitPresentation[] Portraits,
    VictoryRoadIconPresentation[] Items, VictoryRoadTilePresentation[] Tiles, VictoryRoadLayerPresentation[] Layers,
    VictoryRoadPokemonSpritePresentation[] Sprites, VictoryRoadPokemonIconPresentation[] PokemonIcons);

/// <summary>Authenticated, bounded presentation closure. No gameplay state or collision decisions.</summary>
internal static class VictoryRoadPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string ManifestSha256 = "ad233ecfa128335306e75a80acb527585696ab5965a6e009b34dfc96dcca3e04";
    internal const string LockSha256 = "4b7304533661748eb3b491549d3f6a803de6c56b2c1d03bd47a256e9f8d552c4";
    internal const string SynthSha256 = "15748913da40100c6cff3fb56430df051a7f4df9232d7f3a22187c35a1272f6f";
    internal const int SynthBytes = 475;
    internal const string Base = "content/generated/victory-road-presentation/";

    internal static VictoryRoadPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 3_000_000)
                throw new InvalidDataException("Unsafe Victory Road resource path or size.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Victory Road resource symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Victory Road resource directory symlink rejected.");
            if (file.Length != size) throw new InvalidDataException("Victory Road resource size differs: " + path);
            byte[] bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Victory Road resource bytes differ: " + path);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/victory-road-presentation.lock.json", LockSha256, 22148));
        using var manifest = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 50307));
        var l = locked.RootElement; var m = manifest.RootElement;
        if (m.GetProperty("schemaVersion").GetInt32() != 1 || m.GetProperty("profile").GetString() != "firered-victory-road-presentation-v1" ||
            m.GetProperty("sourceCommit").GetString() != SourceCommit || m.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(m.GetProperty("inputs"), l.GetProperty("files")) ||
            !JsonElement.DeepEquals(m.GetProperty("inheritedInputs"), l.GetProperty("inheritedInputs")))
            throw new InvalidDataException("Victory Road presentation identity differs.");
        foreach (var row in l.GetProperty("files").EnumerateArray())
            Read("content/raw/victory-road-presentation/" + row.GetProperty("path").GetString(), row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in l.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        var trainers = new Dictionary<string, VictoryRoadTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("sourceTrainerId").GetInt32(), row.GetProperty("name").GetString()!,
                row.GetProperty("trainerClass").GetString()!, row.GetProperty("className").GetString()!, row.GetProperty("trainerPic").GetString()!,
                row.GetProperty("portrait").GetString()!, row.GetProperty("encounterMusicGender").GetInt32(), row.GetProperty("doubleBattle").GetBoolean()));
        }
        var portraits = m.GetProperty("portraits").EnumerateArray().Select(r => new VictoryRoadPortraitPresentation(
            r.GetProperty("id").GetString()!, r.GetProperty("sourceTrainerPicId").GetInt32(), r.GetProperty("rgbaPath").GetString()!, r.GetProperty("inherited").GetBoolean())).ToArray();
        var items = m.GetProperty("items").EnumerateArray().Select(r => new VictoryRoadIconPresentation(r.GetProperty("item").GetString()!, r.GetProperty("rgbaPath").GetString()!)).ToArray();
        var tiles = m.GetProperty("tiles").EnumerateArray().Select(r => new VictoryRoadTilePresentation(r.GetProperty("metatileId").GetInt32(), r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32())).ToArray();
        var layers = m.GetProperty("layers").EnumerateArray().Select(r => new VictoryRoadLayerPresentation(r.GetProperty("layer").GetInt32(), r.GetProperty("rgbaPath").GetString()!, r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32())).ToArray();
        var sprites = m.GetProperty("sprites").EnumerateArray().Select(r => new VictoryRoadPokemonSpritePresentation(
            r.GetProperty("speciesId").GetString()!, r.GetProperty("sourceSpeciesId").GetInt32(), r.GetProperty("side").GetString()!,
            r.GetProperty("paletteVariant").GetString()!, r.GetProperty("rgbaPath").GetString()!, r.GetProperty("texturePath").GetString()!,
            r.GetProperty("sha256").GetString()!, r.GetProperty("rgbaSha256").GetString()!,
            r.GetProperty("singleBattleTopLeft").GetProperty("x").GetInt32(), r.GetProperty("singleBattleTopLeft").GetProperty("y").GetInt32())).ToArray();
        var pokemonIcons = m.GetProperty("pokemonIcons").EnumerateArray().Select(r => new VictoryRoadPokemonIconPresentation(
            r.GetProperty("speciesId").GetString()!, r.GetProperty("sourceSpeciesId").GetInt32(), r.GetProperty("rgbaPath").GetString()!)).ToArray();
        var expectedSprites = (from species in new[] { 87, 103 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Length != 8 || !sprites.Select(s => (s.SourceSpeciesId, s.Side, s.PaletteVariant)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.SpeciesId != (s.SourceSpeciesId == 87 ? "pret:SPECIES_DEWGONG" : "pret:SPECIES_EXEGGUTOR") ||
                files[s.RgbaPath].Length != 64 * 64 * 4 || Convert.ToHexStringLower(SHA256.HashData(files[s.RgbaPath])) != s.RgbaSha256 ||
                Convert.ToHexStringLower(SHA256.HashData(files[s.TexturePath])) != s.Sha256) ||
            pokemonIcons.Length != 2 || !pokemonIcons.Select(i => i.SourceSpeciesId).ToHashSet().SetEquals([87, 103]) ||
            pokemonIcons.Any(i => i.SpeciesId != (i.SourceSpeciesId == 87 ? "pret:SPECIES_DEWGONG" : "pret:SPECIES_EXEGGUTOR") || files[i.RgbaPath].Length != 32 * 32 * 4) ||
            !m.GetProperty("trainerOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual([87, 103]))
            throw new InvalidDataException("Victory Road trainer-only Pokemon presentation differs.");
        if (trainers.Count != 12 || trainers.Values.Count(t => t.DoubleBattle) != 1 || portraits.Length != 7 || portraits.Count(p => !p.Inherited) != 1 ||
            !portraits.Select(p => p.SourceTrainerPicId).ToHashSet().SetEquals([88, 102, 103, 105, 110, 111, 128]) ||
            portraits.Any(p => files[p.RgbaPath].Length != 64 * 64 * 4) || items.Length != 6 || items.Any(i => files[i.RgbaPath].Length != 24 * 24 * 4) ||
            !tiles.Select(t => t.MetatileId).ToHashSet().SetEquals([721, 737, 775, 791]) || layers.Length != 3 ||
            !layers.Select(l => l.Layer).ToHashSet().SetEquals([0, 1, 2]) || layers.Any(l => l.Width != 64 || l.Height != 16 || files[l.RgbaPath].Length != 64 * 16 * 4))
            throw new InvalidDataException("Victory Road presentation inventory differs.");
        return new(files, trainers, portraits, items, tiles, layers, sprites, pokemonIcons);
    }
}
