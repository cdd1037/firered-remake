using System.Text.Json;
using Godot;

public partial class CampaignView
{
    private sealed record SharedTrainerPresentation(string ClassName, Texture2D Portrait, Vector2 Position);
    private readonly Dictionary<(string Class, string Picture, int SourcePicture), SharedTrainerPresentation> _sharedTrainerPresentation = new();

    // Presentation is selected by the source class/picture pair, independent of
    // the map or trainer roster that first imported that portrait.
    private void LoadSharedTrainerPresentation()
    {
        _sharedTrainerPresentation.Clear();
        foreach (var manifest in new[] { SevenIslandManifest, SixIslandManifest })
        foreach (var row in manifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("portrait").GetString()!;
            var portrait = manifest.GetProperty("portraits").EnumerateArray().Single(p => p.GetProperty("id").GetString() == id);
            _sharedTrainerPresentation.TryAdd((row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("trainerPic").GetString()!, portrait.GetProperty("sourceTrainerPicId").GetInt32()),
                new(row.GetProperty("className").GetString()!, _forestTrainerPortraits[id],
                    new(portrait.GetProperty("x").GetInt32(), portrait.GetProperty("y").GetInt32())));
        }
        foreach (var row in _trainerTowerBindings.Values)
        {
            string id = row.GetProperty("portrait").GetString()!;
            _sharedTrainerPresentation.TryAdd((row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("trainerPic").GetString()!, row.GetProperty("sourceTrainerPicId").GetInt32()),
                new(row.GetProperty("className").GetString()!, _trainerTowerPortraits[id], _trainerTowerPortraitPositions[id]));
        }
    }

    private SharedTrainerPresentation? VisibleSharedTrainer
    {
        get
        {
            if (_session.State.ForestTrainerBattle is null) return null;
            var trainer = VisibleOrdinaryTrainer();
            return _sharedTrainerPresentation.GetValueOrDefault((trainer.TrainerClass, trainer.TrainerPic, trainer.SourceTrainerPic));
        }
    }
}
