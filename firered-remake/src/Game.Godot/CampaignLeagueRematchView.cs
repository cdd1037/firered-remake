using System.Text.Json;
using Godot;

/// <summary>Trainer-only rematch art extends the existing shared battle presentation.</summary>
public partial class CampaignView
{
    private LeagueRematchPresentationData? _leagueRematchArt;
    private readonly Dictionary<int, string> _leagueRematchSpeciesNames = [];
    private JsonElement LeagueRematchManifest => _leagueRematchArt?.Manifest ?? throw new InvalidDataException("League rematch presentation missing.");

    private void LoadLeagueRematchPresentation()
    {
        if (!_catalog.LeagueRematchEnabled) return;
        if (!_session.IsCurrentGame || !_catalog.CeruleanCaveEnabled || _battleArt.Count != 652 ||
            _storageIcons.Count != 163 || _route22Footprints.Count != 116 || _leagueTrainers.Count != 7 ||
            _catalog.PokemonContent?.Catalog.Species.Count != 139 || _catalog.LeagueRematchOpponents?.Catalog.Species.Count != 168)
            throw new InvalidDataException("League rematch requires its complete current Cerulean Cave predecessor.");
        _leagueRematchArt = LeagueRematchPresentationAssets.Load(_root);
        foreach (var row in LeagueRematchManifest.GetProperty("inheritedSprites").EnumerateArray())
        {
            var key = (208, row.GetProperty("side").GetString()!, row.GetProperty("paletteVariant").GetString() == "shiny");
            if (!_battleArt.TryGetValue(key, out var inherited) || inherited.Texture.GetWidth() != 64 || inherited.Texture.GetHeight() != 64 ||
                inherited.Position != new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()))
                throw new InvalidDataException("Inherited Steelix battle presentation differs.");
        }
        foreach (var row in LeagueRematchManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string id = row.GetProperty("speciesId").GetString()!;
            if (_catalog.LeagueRematchOpponents!.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId != species ||
                _catalog.PokemonContent!.Catalog.Species.ContainsKey(new(id)))
                throw new InvalidDataException("League rematch sprite is not an admitted trainer-only species.");
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, _leagueRematchArt.Files[row.GetProperty("rgbaPath").GetString()!]);
            var texture = SceneTextures.FromImage(image);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
        }
        foreach (var row in LeagueRematchManifest.GetProperty("speciesNames").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (_catalog.LeagueRematchOpponents!.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("League rematch species name is not admitted.");
            _leagueRematchSpeciesNames.Add(species, row.GetProperty("name").GetString()!);
        }
        var parties = _catalog.LeagueRematchTrainerParties ?? throw new InvalidDataException("League rematch trainers missing.");
        foreach (var row in LeagueRematchManifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            var trainer = parties.Trainers[new(id)];
            if (trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() || trainer.TrainerClass != row.GetProperty("trainerClass").GetString() ||
                trainer.SourceTrainerName != row.GetProperty("name").GetString() || trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                !_leagueTrainers.ContainsKey(row.GetProperty("inheritedTrainer").GetString()!) || !_leagueTextures.ContainsKey(row.GetProperty("portrait").GetString()!))
                throw new InvalidDataException("League rematch trainer presentation binding differs.");
            _leagueTrainers.Add(id, row);
        }
        if (_battleArt.Count != 672 || _storageIcons.Count != 163 || _route22Footprints.Count != 116 ||
            _leagueRematchSpeciesNames.Count != 6 || _leagueTrainers.Count != 14)
            throw new InvalidDataException("League rematch presentation inventory differs.");
        Write("leagueRematchPresentation", new { LeagueRematchPresentationAssets.ManifestSha256, newBattleSprites = 20,
            inheritedSteelixSprites = 4, battleSprites = 672, icons = 163, footprints = 116, ownedSpecies = 139,
            opponentSpecies = 168, rematchTrainerAliases = 7, newMoveAnimations = 0, nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private string? LeagueRematchSpeciesName(int species) => _leagueRematchSpeciesNames.GetValueOrDefault(species);

    private void VerifyLeagueRematchMoveGeometry()
    {
        if (!_catalog.LeagueRematchEnabled) return;
        foreach (var row in LeagueRematchManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("League rematch source-move battler geometry differs.");
        }
    }

    private void DisposeLeagueRematchPresentation()
    {
        if (_leagueRematchArt is not null)
            foreach (var row in LeagueRematchManifest.GetProperty("trainers").EnumerateArray())
                _leagueTrainers.Remove(row.GetProperty("trainer").GetString()!);
        _leagueRematchArt = null;
        _leagueRematchSpeciesNames.Clear();
    }
}
