using Game.Content;
using System.Text.Json;

internal sealed record ViridianGymTrainerPresentation(string Trainer, int SourceTrainerId, string Name,
    string TrainerClass, string ClassName, string TrainerPic, string Portrait, int EncounterMusicGender);
internal sealed record ViridianGymPortraitPresentation(string Id, string TrainerPic, int SourceTrainerPicId,
    string TexturePath, bool Inherited);
internal sealed record ViridianGymPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, ViridianGymTrainerPresentation> Trainers, ViridianGymPortraitPresentation[] Portraits);

/// <summary>Exact local source closure; two new portraits and authenticated predecessor resources.</summary>
internal static class ViridianGymPresentationAssets
{

    internal const string Base = "content/generated/ui/viridian-gym-presentation/";

    internal static ViridianGymPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "portraits");
        var m = package.Manifest;
        var files = package.Files;
        var trainers = new Dictionary<string, ViridianGymTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("sourceTrainerId").GetInt32(), row.GetProperty("name").GetString()!,
                row.GetProperty("trainerClass").GetString()!, row.GetProperty("className").GetString()!, row.GetProperty("trainerPic").GetString()!,
                row.GetProperty("portrait").GetString()!, row.GetProperty("encounterMusicGender").GetInt32()));
        }
        var portraits = m.GetProperty("portraits").EnumerateArray().Select(row => new ViridianGymPortraitPresentation(
            row.GetProperty("id").GetString()!, row.GetProperty("trainerPic").GetString()!, row.GetProperty("sourceTrainerPicId").GetInt32(),
            row.GetProperty("texturePath").GetString()!, row.GetProperty("inherited").GetBoolean())).ToArray();
        if (portraits.Any(p => !files.ContainsKey(p.TexturePath)))
            throw new InvalidDataException("Viridian Gym portrait dimensions differ.");
        if (!portraits.Select(p => p.SourceTrainerPicId).ToHashSet().SetEquals([103, 105, 108, 110]))
            throw new InvalidDataException("Viridian Gym presentation inventory differs.");
        return new(files, trainers, portraits);
    }
}
