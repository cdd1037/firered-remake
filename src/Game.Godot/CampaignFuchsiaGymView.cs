using Game.Content;
using Godot;

public partial class CampaignView
{
    private FuchsiaGymPresentationData? _fuchsiaGymPresentation;
    private bool IsFuchsiaGymLeader(string id) => _catalog.FuchsiaGymEnabled && id == "pret:TRAINER_LEADER_KOGA";

    private void LoadFuchsiaGymPresentation()
    {
        if (!_catalog.FuchsiaGymEnabled) return;
        var presentation = FuchsiaGymPresentationAssets.Load(_root);
        var source = _catalog.FuchsiaGymTrainers ?? throw new InvalidDataException("Missing Fuchsia Gym trainer catalog.");
        foreach (var trainer in source.Trainers.Values)
        {
            if (!presentation.Trainers.TryGetValue(trainer.Id.Value, out var row) || row.Name != trainer.SourceTrainerName ||
                row.TrainerClass != trainer.TrainerClass || row.TrainerPic != trainer.TrainerPic ||
                row.EncounterMusicGender != trainer.EncounterMusicGender)
                throw new InvalidDataException("Unsupported Fuchsia Gym visible trainer: " + trainer.Id.Value);
        }
        foreach (var id in new[] { "juggler-front", "tamer-front", "koga-front" })
        {
            using var image = NativeImages.LoadPng(presentation.Files[id + ".png"], 64, 64);
            _forestTrainerPortraits.Add(id, (ImageTexture)SceneTextures.FromImage(image));
        }
        foreach (var icon in PretFuchsiaGymItems.LoadProject(_root).Icons)
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item, out var existing)))_earlyItemIcons.Add(icon.Item, ItemTexture("data/items/fuchsia-gym", icon));
        }
        _fuchsiaGymPresentation = presentation;
        Write("fuchsiaGymPresentation", new {             badgeFlag = "FLAG_BADGE05_GET", tm06Fanfare = "MUS_LEVEL_UP", audibleOutputVerified = false });
    }

    private FuchsiaGymTrainerPresentation? VisibleFuchsiaGymTrainer()
    {
        if (_session.State.ForestTrainerBattle is not { } battle ||
            _catalog.FuchsiaGymTrainers?.Trainers.ContainsKey(battle.Trainer) != true) return null;
        if (_fuchsiaGymPresentation is null || !_fuchsiaGymPresentation.Trainers.TryGetValue(battle.Trainer.Value, out var trainer))
            throw new InvalidDataException("Unsupported Fuchsia Gym visible trainer: " + battle.Trainer.Value);
        return trainer;
    }
}
