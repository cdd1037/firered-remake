using Game.Content;
using System.Text.Json;
using Godot;

/// <summary>Trainer-only rematch art extends the existing shared battle presentation.</summary>
public partial class CampaignView
{
    private LeagueRematchPresentationData? _leagueRematchArt;
    private JsonElement LeagueRematchManifest => _leagueRematchArt?.Manifest ?? throw new InvalidDataException("League rematch presentation missing.");

    private void LoadLeagueRematchPresentation()
    {
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
            if (_catalog.OpponentPokemon!.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId != species)
                throw new InvalidDataException("League rematch sprite is not an admitted opponent species.");
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            using var image = NativeImages.LoadPng(_leagueRematchArt.Files[row.GetProperty("texturePath").GetString()!], 64, 64);
            var texture = SceneTextures.FromImage(image);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
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

    }

    private void VerifyLeagueRematchMoveGeometry()
    {
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

    }
}
