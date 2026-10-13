using Game.Content;
using Godot;

public partial class CampaignView
{
    private FuchsiaGymPresentationData? _fuchsiaGymPresentation;
    private bool IsFuchsiaGymLeader(string id) => _catalog.FuchsiaGymEnabled && id == "pret:TRAINER_LEADER_KOGA";

    private void LoadFuchsiaGymPresentation()
    {
        if (!_catalog.FuchsiaGymEnabled) return;
        if (_battleArt.Count != 428 || _storageIcons.Count != 107 || _route22Footprints.Count != 78)
            throw new InvalidDataException("Fuchsia Gym must preserve the 107-species presentation.");
        var presentation = FuchsiaGymPresentationAssets.Load(_root);
        var source = _catalog.FuchsiaGymTrainers ?? throw new InvalidDataException("Missing Fuchsia Gym trainer catalog.");
        if (source.Trainers.Count != presentation.Trainers.Count)
            throw new InvalidDataException("Fuchsia Gym visible trainer inventory differs.");
        foreach (var trainer in source.Trainers.Values)
        {
            if (!presentation.Trainers.TryGetValue(trainer.Id.Value, out var row) || row.Name != trainer.SourceTrainerName ||
                row.TrainerClass != trainer.TrainerClass || row.TrainerPic != trainer.TrainerPic ||
                row.EncounterMusicGender != trainer.EncounterMusicGender)
                throw new InvalidDataException("Unsupported Fuchsia Gym visible trainer: " + trainer.Id.Value);
        }
        foreach (var id in new[] { "juggler-front", "tamer-front", "koga-front" })
        {
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, presentation.Files[id + ".rgba"]);
            _forestTrainerPortraits.Add(id, (ImageTexture)SceneTextures.FromImage(image));
        }
        foreach (var icon in PretFuchsiaGymItems.LoadProject(_root).Icons)
        {
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            if (_earlyItemIcons.TryGetValue(icon.Item, out var existing))
            {
                using var current = existing.GetImage(); current.Convert(Image.Format.Rgba8);
                if (!current.GetData().SequenceEqual(image.GetData())) throw new InvalidDataException("Historical TM06 icon differs.");
            }
            else _earlyItemIcons.Add(icon.Item, (ImageTexture)SceneTextures.FromImage(image));
        }
        _fuchsiaGymPresentation = presentation;
        Write("fuchsiaGymPresentation", new { trainers = 7, addedPortraits = 3, species = 107,
            badgeFlag = "FLAG_BADGE05_GET", tm06Fanfare = "MUS_LEVEL_UP", inheritedCues = 303,
            FuchsiaGymPresentationAssets.ManifestSha256, audibleOutputVerified = false });
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
