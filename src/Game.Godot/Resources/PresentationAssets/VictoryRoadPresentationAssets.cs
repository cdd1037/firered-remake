using Game.Content;
using System.Text.Json;

internal sealed record VictoryRoadTrainerPresentation(string Trainer, int SourceTrainerId, string Name,
    string TrainerClass, string ClassName, string TrainerPic, string Portrait, int EncounterMusicGender, bool DoubleBattle);
internal sealed record VictoryRoadPortraitPresentation(string Id, int SourceTrainerPicId, string TexturePath, bool Inherited);
internal sealed record VictoryRoadIconPresentation(string Item, string TexturePath);
internal sealed record VictoryRoadPokemonSpritePresentation(string SpeciesId, int SourceSpeciesId, string Side, string PaletteVariant, string TexturePath, int X, int Y);
internal sealed record VictoryRoadPokemonIconPresentation(string SpeciesId, int SourceSpeciesId, string TexturePath);
internal sealed record VictoryRoadTilePresentation(int MetatileId, int X, int Y);
internal sealed record VictoryRoadLayerPresentation(int Layer, string TexturePath, int Width, int Height);
internal sealed record VictoryRoadPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, VictoryRoadTrainerPresentation> Trainers, VictoryRoadPortraitPresentation[] Portraits,
    VictoryRoadIconPresentation[] Items, VictoryRoadTilePresentation[] Tiles, VictoryRoadLayerPresentation[] Layers,
    VictoryRoadPokemonSpritePresentation[] Sprites, VictoryRoadPokemonIconPresentation[] PokemonIcons);

/// <summary>Presentation asset bindings. No gameplay state or collision decisions.</summary>
internal static class VictoryRoadPresentationAssets
{

    internal const string Base = "content/generated/sprites/pokemon/victory-road/";

    internal static VictoryRoadPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "portraits", "items", "layers", "sprites", "pokemonIcons");
        var m = package.Manifest;
        var files = package.Files;
        var trainers = new Dictionary<string, VictoryRoadTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("sourceTrainerId").GetInt32(), row.GetProperty("name").GetString()!,
                row.GetProperty("trainerClass").GetString()!, row.GetProperty("className").GetString()!, row.GetProperty("trainerPic").GetString()!,
                row.GetProperty("portrait").GetString()!, row.GetProperty("encounterMusicGender").GetInt32(), row.GetProperty("doubleBattle").GetBoolean()));
        }
        var portraits = m.GetProperty("portraits").EnumerateArray().Select(r => new VictoryRoadPortraitPresentation(
            r.GetProperty("id").GetString()!, r.GetProperty("sourceTrainerPicId").GetInt32(), r.GetProperty("texturePath").GetString()!, r.GetProperty("inherited").GetBoolean())).ToArray();
        var items = m.GetProperty("items").EnumerateArray().Select(r => new VictoryRoadIconPresentation(r.GetProperty("item").GetString()!, r.GetProperty("texturePath").GetString()!)).ToArray();
        var tiles = m.GetProperty("tiles").EnumerateArray().Select(r => new VictoryRoadTilePresentation(r.GetProperty("metatileId").GetInt32(), r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32())).ToArray();
        var layers = m.GetProperty("layers").EnumerateArray().Select(r => new VictoryRoadLayerPresentation(r.GetProperty("layer").GetInt32(), r.GetProperty("texturePath").GetString()!, r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32())).ToArray();
        var sprites = m.GetProperty("sprites").EnumerateArray().Select(r => new VictoryRoadPokemonSpritePresentation(
            r.GetProperty("speciesId").GetString()!,r.GetProperty("sourceSpeciesId").GetInt32(),r.GetProperty("side").GetString()!,            r.GetProperty("paletteVariant").GetString()!,r.GetProperty("texturePath").GetString()!,            r.GetProperty("singleBattleTopLeft").GetProperty("x").GetInt32(),r.GetProperty("singleBattleTopLeft").GetProperty("y").GetInt32())).ToArray();
        var pokemonIcons = m.GetProperty("pokemonIcons").EnumerateArray().Select(r => new VictoryRoadPokemonIconPresentation(
            r.GetProperty("speciesId").GetString()!, r.GetProperty("sourceSpeciesId").GetInt32(), r.GetProperty("texturePath").GetString()!)).ToArray();
        var expectedSprites = (from species in new[] { 87, 103 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (sprites.Any(s => s.SpeciesId != (s.SourceSpeciesId == 87 ? "pret:SPECIES_DEWGONG" : "pret:SPECIES_EXEGGUTOR") ||
                !files.ContainsKey(s.TexturePath)))
            throw new InvalidDataException("Victory Road sprite format differs.");
        if (!layers.Select(layer => layer.Layer).ToHashSet().SetEquals([0, 1, 2]) ||
            layers.Any(layer => layer.Width != 64 || layer.Height != 16))
            throw new InvalidDataException("Victory Road layer format differs.");
        if (!sprites.Select(s => (s.SourceSpeciesId, s.Side, s.PaletteVariant)).ToHashSet().SetEquals(expectedSprites) || !pokemonIcons.Select(i => i.SourceSpeciesId).ToHashSet().SetEquals([87, 103]) ||
            pokemonIcons.Any(i => i.SpeciesId != (i.SourceSpeciesId == 87 ? "pret:SPECIES_DEWGONG" : "pret:SPECIES_EXEGGUTOR")) ||
            !m.GetProperty("trainerOnlySpecies").EnumerateArray().Select(s => s.GetInt32()).SequenceEqual([87, 103]))
            throw new InvalidDataException("Victory Road trainer-only Pokemon presentation differs.");
        if (!portraits.Select(p => p.SourceTrainerPicId).ToHashSet().SetEquals([88, 102, 103, 105, 110, 111, 128]) ||
            !tiles.Select(t => t.MetatileId).ToHashSet().SetEquals([721, 737, 775, 791]))
            throw new InvalidDataException("Victory Road presentation inventory differs.");
        return new(files, trainers, portraits, items, tiles, layers, sprites, pokemonIcons);
    }
}
