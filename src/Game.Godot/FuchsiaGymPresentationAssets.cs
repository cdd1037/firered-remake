using Game.Content;
using System.Text.Json;

internal sealed record FuchsiaGymTrainerPresentation(string Trainer, string Name, string TrainerClass,
    string ClassName, string Portrait, string TrainerPic, int EncounterMusicGender);
internal sealed record FuchsiaGymPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, FuchsiaGymTrainerPresentation> Trainers);

/// <summary>Source presentation. Reuses existing terrain, TM, badge and custom synthesis.</summary>
internal static class FuchsiaGymPresentationAssets
{

    private const string Base = "content/generated/ui/fuchsia-gym-presentation/";

    internal static FuchsiaGymPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, true);
        var m = package.Manifest;
        var files = package.Files;
        var trainers = new Dictionary<string, FuchsiaGymTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("name").GetString()!, row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("className").GetString()!, row.GetProperty("portrait").GetString()!,
                row.GetProperty("trainerPic").GetString()!, row.GetProperty("encounterMusicGender").GetInt32()));
        }
        if (new[] { "juggler-front", "tamer-front", "koga-front" }
            .Any(id => !files.ContainsKey(id + ".png")))
            throw new InvalidDataException("Fuchsia Gym presentation inventory differs.");
        return new(files, trainers);
    }
}
