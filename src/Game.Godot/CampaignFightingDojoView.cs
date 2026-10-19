using System.Text.Json;
using Game.Content;
using Godot;

/// <summary>Dojo bindings reuse League Hitmon art and the original Black Belt portrait.</summary>
public partial class CampaignView
{
    private FightingDojoPresentationData? _fightingDojoArt;
    private readonly Dictionary<string, JsonElement> _fightingDojoTrainers = new(StringComparer.Ordinal);

    private void LoadFightingDojoPresentation()
    {
        var art = FightingDojoPresentationAssets.Load(_root);
        var trainers = _catalog.FightingDojoTrainers;
        foreach (var row in art.Manifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            if (!trainers.Trainers.TryGetValue(new(id), out var trainer) ||
                trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() ||
                trainer.SourceTrainerName != row.GetProperty("name").GetString() ||
                trainer.TrainerClass != row.GetProperty("trainerClass").GetString() ||
                trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                trainer.SourceTrainerPic != row.GetProperty("sourceTrainerPicId").GetInt32() ||
                trainer.EncounterMusicGender != row.GetProperty("encounterMusicGender").GetInt32())
                throw new InvalidDataException("Fighting Dojo visible trainer differs.");
            _fightingDojoTrainers.Add(id, row);
        }
        void Verify(Texture2D texture, JsonElement row)
        {            if (texture.GetWidth() != row.GetProperty("width").GetInt32() || texture.GetHeight() != row.GetProperty("height").GetInt32())
                throw new InvalidDataException("Inherited Fighting Dojo presentation pixels differ.");
}
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            var key = (species, row.GetProperty("side").GetString()!, row.GetProperty("paletteVariant").GetString() == "shiny");
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species ||
                !_battleArt.TryGetValue(key, out var inherited) || inherited.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Owned Fighting Dojo sprite binding differs.");
            Verify(inherited.Texture, row);
            if (key.Item2 == "front" && !key.Item3) Verify(_portraits[species], row);
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
            Verify(_storageIcons[new(row.GetProperty("speciesId").GetString()!)], row);
        foreach (var row in art.Manifest.GetProperty("portraits").EnumerateArray())
            Verify(_forestTrainerPortraits[row.GetProperty("id").GetString()!], row);
        foreach (var entry in art.DexEntries.Values)
            if (_pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Fighting Dojo dex metadata differs from the authenticated source menu.");
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], 16, 16);
            _route22Footprints.Add(row.GetProperty("sourceSpeciesId").GetInt32(), SceneTextures.FromImage(image));
        }
        _fightingDojoArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }

    private void DisposeFightingDojoPresentation()
    {
        _fightingDojoArt = null; _fightingDojoTrainers.Clear();
    }
}
