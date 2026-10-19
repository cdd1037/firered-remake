using System.Text.Json;
using Godot;

public partial class CampaignView
{
    private sealed record RegisteredTrainerPresentation(string Portrait);
    private readonly Dictionary<string, RegisteredTrainerPresentation> _registeredTrainerPresentation = new(StringComparer.Ordinal);
    private RegisteredTrainerPresentation? VisibleRegisteredTrainer => _session.State.ForestTrainerBattle is { } battle
        ? _registeredTrainerPresentation.GetValueOrDefault(battle.Trainer.Value) : null;

    private sealed record SharedTrainerPresentation(Texture2D Portrait, Vector2 Position);
    private readonly Dictionary<(string Class, string Picture, int SourcePicture), SharedTrainerPresentation> _sharedTrainerPresentation = new();

    // Presentation is selected by the source class/picture pair, independent of
    // the map or trainer roster that first imported that portrait.
    private void LoadSharedTrainerPresentation()
    {
        _sharedTrainerPresentation.Clear();
        _registeredTrainerPresentation.Clear();
        // Keep the old regional first-match precedence once, at composition time.
        foreach (var rows in new[] { _fightingDojoTrainers, _sevenIslandTrainers, _fiveIslandTrainers,
            _sixIslandTrainers, _fourIslandTrainers, _seviiRubyTrainers, _leagueTrainers })
        foreach (var (id, row) in rows)
            _registeredTrainerPresentation.TryAdd(id, new((ReferenceEquals(rows, _leagueTrainers) ? "league-" : "") + row.GetProperty("portrait").GetString()!));
        foreach (var manifest in new[] { SevenIslandManifest, SixIslandManifest })
        {
            var portraits = manifest.GetProperty("portraits").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString()!, StringComparer.Ordinal);
            foreach (var row in manifest.GetProperty("trainers").EnumerateArray())
            {
                string id = row.GetProperty("portrait").GetString()!;
                var portrait = portraits[id];
                _sharedTrainerPresentation.TryAdd((row.GetProperty("trainerClass").GetString()!,
                    row.GetProperty("trainerPic").GetString()!, portrait.GetProperty("sourceTrainerPicId").GetInt32()),
                    new(_forestTrainerPortraits[id],
                        new(portrait.GetProperty("x").GetInt32(), portrait.GetProperty("y").GetInt32())));
            }
        }
        foreach (var row in _trainerTowerBindings.Values)
        {
            string id = row.GetProperty("portrait").GetString()!;
            _sharedTrainerPresentation.TryAdd((row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("trainerPic").GetString()!, row.GetProperty("sourceTrainerPicId").GetInt32()),
                new(_trainerTowerPortraits[id], _trainerTowerPortraitPositions[id]));
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
