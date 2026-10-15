using Game.Content;
using Godot;

public partial class CampaignView
{
    private ViridianGymPresentationData? _viridianGymPresentation;
    private bool IsViridianGymLeader(string id) => _catalog.ViridianGymEnabled && id == "pret:TRAINER_LEADER_GIOVANNI";
    private string SourceLeaderIntroMusic(string id) => IsViridianGymLeader(id) ? "MUS_ENCOUNTER_ROCKET" : "MUS_ENCOUNTER_GYM_LEADER";

    private void LoadViridianGymPresentation()
    {
        if (!_catalog.ViridianGymEnabled) return;
        if (!_catalog.FirstSeviiEnabled ||
            _session.RulesVersion != Game.Application.CampaignSession.ViridianGymRulesVersion &&
            !(_catalog.LeagueRivalEnabled && _session.RulesVersion == Game.Application.CampaignSession.LeagueRivalRulesVersion) &&
            !(_catalog.VictoryRoadEnabled && _session.RulesVersion == Game.Application.CampaignSession.VictoryRoadRulesVersion) && !(_catalog.PokemonLeagueEnabled && _session.RulesVersion == Game.Application.CampaignSession.PokemonLeagueRulesVersion) &&
            !(_catalog.NationalDexEnabled && _catalog.PokemonLeagueEnabled && _session.RulesVersion == Game.Application.CampaignSession.NationalDexRulesVersion) && !SeviiRubyPresentationProfile)
            throw new InvalidDataException("Viridian Gym presentation requires its authenticated successor profile.");
        var art = ViridianGymPresentationAssets.Load(_root);
        var source = _catalog.ViridianGymTrainerParties ?? throw new InvalidDataException("Viridian Gym trainers missing.");
        foreach (var trainer in source.Trainers.Values)
        {
            if (!art.Trainers.TryGetValue(trainer.Id.Value, out var row) || row.SourceTrainerId != trainer.SourceTrainerId || row.Name != trainer.SourceTrainerName ||
                row.TrainerClass != trainer.TrainerClass || row.TrainerPic != trainer.TrainerPic || row.EncounterMusicGender != trainer.EncounterMusicGender ||
                art.Portraits.Single(p => p.Id == row.Portrait).SourceTrainerPicId != trainer.SourceTrainerPic)
                throw new InvalidDataException("Viridian Gym visible trainer differs: " + trainer.Id.Value);
        }
        foreach (var portrait in art.Portraits)
        {
            byte[] png = art.Files[portrait.TexturePath];
            if (portrait.Inherited)
            {
                if (!_forestTrainerPortraits.TryGetValue(portrait.Id, out var old)) throw new InvalidDataException("Inherited gym portrait missing.");
            }
            else
            {
                using var image = NativeImages.LoadPng(png, 64, 64);
                _forestTrainerPortraits.Add(portrait.Id, (ImageTexture)SceneTextures.FromImage(image));
            }
        }
        if (!_earlyItemIcons.TryGetValue(new("pret:ITEM_TM28"), out var groundIcon)) throw new InvalidDataException("Inherited Ground machine icon missing.");
        _earlyItemIcons.Add(new("pret:ITEM_TM26"), groundIcon);
        _viridianGymPresentation = art;
        Write("viridianGymPresentation", new { ViridianGymPresentationAssets.SourceCommit,             map = "map.viridian-city-gym",
        leaderIntroMusic = "MUS_ENCOUNTER_ROCKET", physicalAudioVerified = false });
    }

    private ViridianGymTrainerPresentation? VisibleViridianGymTrainer()
    {
        if (_session.State.ForestTrainerBattle is not { } battle || _catalog.ViridianGymTrainerParties?.Trainers.ContainsKey(battle.Trainer) != true) return null;
        if (_viridianGymPresentation is null || !_viridianGymPresentation.Trainers.TryGetValue(battle.Trainer.Value, out var trainer))
            throw new InvalidDataException("Viridian Gym visible trainer is not authenticated.");
        return trainer;
    }
}
