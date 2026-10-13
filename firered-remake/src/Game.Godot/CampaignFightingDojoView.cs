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
        if (!_catalog.FightingDojoEnabled) return;
        if (!_session.IsCurrentGame || !_catalog.TrainerTowerEnabled || _route22Footprints.Count != 129)
            throw new InvalidDataException("Fighting Dojo requires its current Trainer Tower presentation predecessor.");
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
        {
            using var image = (Image)texture.GetImage().Duplicate(); image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != row.GetProperty("width").GetInt32() || image.GetHeight() != row.GetProperty("height").GetInt32() ||
                !image.GetData().SequenceEqual(art.Files[row.GetProperty("rgbaPath").GetString()!]))
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
            if (_pokedexMenu is null || entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Fighting Dojo dex metadata differs from the authenticated source menu.");
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            using var image = Image.CreateFromData(16, 16, false, Image.Format.Rgba8, art.Files[row.GetProperty("rgbaPath").GetString()!]);
            _route22Footprints.Add(row.GetProperty("sourceSpeciesId").GetInt32(), SceneTextures.FromImage(image));
        }
        _fightingDojoArt = art;
        if (_fightingDojoTrainers.Count != 5 || _route22Footprints.Count != 131)
            throw new InvalidDataException("Fighting Dojo presentation inventory differs.");
        Write("fightingDojoPresentation", new { FightingDojoPresentationAssets.ManifestSha256, trainerBindings = 5,
            newFootprints = 2, dexEntries = 2, newBattleSprites = 0, newAudioCues = 0,
            nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private string? VisibleFightingDojoTrainerText(string key) => _session.State.ForestTrainerBattle is { } battle &&
        _fightingDojoTrainers.TryGetValue(battle.Trainer.Value, out var row) ? row.GetProperty(key).GetString() : null;
    private CaptureDexEntry? FightingDojoCaptureDexEntry(int species) => _fightingDojoArt?.DexEntries.GetValueOrDefault(species) is not { } e ? null :
        new(e.SourceSpeciesId, e.NationalNumber, e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms,
            e.HeightDisplay, e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle, e.Source, e.DescriptionSource);
    private PokedexMenuPage? FightingDojoPokedexPage(int species, PokedexMenuPage page) =>
        _fightingDojoArt?.DexEntries.GetValueOrDefault(species) is { } entry
            ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null } : null;
    private byte[]? ReadFightingDojoDexTexture(PokedexMenuTexture texture) => _fightingDojoArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? art.Files[texture.TexturePath] : null;
    private void DisposeFightingDojoPresentation()
    {
        _fightingDojoArt = null; _fightingDojoTrainers.Clear();
    }
}
