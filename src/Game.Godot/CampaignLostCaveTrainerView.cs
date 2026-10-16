using System.Text.Json;
using Game.Domain.Trainers;
using Godot;

public partial class CampaignView
{
    private sealed record LostCaveTrainerPresentation(string ClassName, Texture2D Portrait, Vector2 Position);
    private readonly Dictionary<string, LostCaveTrainerPresentation> _lostCaveTrainerPresentation = new(StringComparer.Ordinal);

    private void LoadLostCaveTrainerPresentation()
    {
        if (!_catalog.LostCaveEnabled) return;
        _lostCaveTrainerPresentation.Clear();
        foreach (var trainer in _catalog.LostCaveTrainers.Trainers.Values)
        {
            LostCaveTrainerPresentation? presentation = null;
            foreach (var manifest in new[] { SevenIslandManifest, SixIslandManifest })
            {
                var row = manifest.GetProperty("trainers").EnumerateArray().FirstOrDefault(r =>
                    r.GetProperty("trainerClass").GetString() == trainer.TrainerClass &&
                    r.GetProperty("trainerPic").GetString() == trainer.TrainerPic);
                if (row.ValueKind == JsonValueKind.Undefined) continue;
                string id = row.GetProperty("portrait").GetString()!;
                var portrait = manifest.GetProperty("portraits").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
                if (portrait.GetProperty("sourceTrainerPicId").GetInt32() != trainer.SourceTrainerPic)
                    throw new InvalidDataException("Lost Cave inherited trainer portrait source differs.");
                presentation = new(row.GetProperty("className").GetString()!, _forestTrainerPortraits[id],
                    new(portrait.GetProperty("x").GetInt32(), portrait.GetProperty("y").GetInt32()));
                break;
            }
            if (presentation is null)
            {
                var row = _trainerTowerBindings.Values.FirstOrDefault(r =>
                    r.GetProperty("trainerClass").GetString() == trainer.TrainerClass &&
                    r.GetProperty("trainerPic").GetString() == trainer.TrainerPic &&
                    r.GetProperty("sourceTrainerPicId").GetInt32() == trainer.SourceTrainerPic);
                if (row.ValueKind == JsonValueKind.Undefined)
                    throw new InvalidDataException("Lost Cave source trainer presentation missing: " + trainer.Id.Value);
                string id = row.GetProperty("portrait").GetString()!;
                presentation = new(row.GetProperty("className").GetString()!, _trainerTowerPortraits[id], _trainerTowerPortraitPositions[id]);
            }
            _lostCaveTrainerPresentation.Add(trainer.Id.Value, presentation);
        }
    }

    private LostCaveTrainerPresentation? VisibleLostCaveTrainer => _session.State.ForestTrainerBattle is { } battle
        ? _lostCaveTrainerPresentation.GetValueOrDefault(battle.Trainer.Value) : null;
}
