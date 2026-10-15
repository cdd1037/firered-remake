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
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal const string Base = "content/generated/viridian-gym-presentation/";

    internal static ViridianGymPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Viridian Gym presentation path/size.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Viridian Gym resource symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Viridian Gym resource directory symlink rejected.");
            byte[] bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }
        using var manifest = JsonDocument.Parse(Read(Base + "manifest.json"));
        var m = manifest.RootElement;
        if (m.GetProperty("schemaVersion").GetInt32() != 1 || m.GetProperty("profile").GetString() != "firered-viridian-gym-presentation-v1")
            throw new InvalidDataException("Viridian Gym presentation identity differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (!path.EndsWith(".png", StringComparison.Ordinal)) continue;
            files.Add(path, Read(path));
        }
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
        }
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
        if (portraits.Any(p => !files.TryGetValue(p.TexturePath, out var rgba) || !NativeImages.HasSize(rgba, 64, 64)))
            throw new InvalidDataException("Viridian Gym portrait dimensions differ.");
        if (!portraits.Select(p => p.SourceTrainerPicId).ToHashSet().SetEquals([103, 105, 108, 110]))
            throw new InvalidDataException("Viridian Gym presentation inventory differs.");
        return new(files, trainers, portraits);
    }
}
