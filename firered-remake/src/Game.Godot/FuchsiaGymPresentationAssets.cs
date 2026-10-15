using Game.Content;
using System.Text.Json;

internal sealed record FuchsiaGymTrainerPresentation(string Trainer, string Name, string TrainerClass,
    string ClassName, string Portrait, string TrainerPic, int EncounterMusicGender);
internal sealed record FuchsiaGymPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, FuchsiaGymTrainerPresentation> Trainers);

/// <summary>Source presentation. Reuses existing terrain, TM, badge and custom synthesis.</summary>
internal static class FuchsiaGymPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private const string Base = "content/generated/ui/fuchsia-gym-presentation/";

    internal static FuchsiaGymPresentationData Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) || relative.Contains('\\') ||
                relative.Split('/').Any(p => p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Fuchsia Gym presentation path or size.");
            var file = new FileInfo(Path.Combine(root, relative));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fuchsia Gym presentation symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Fuchsia Gym presentation directory symlink rejected.");
            var data = File.ReadAllBytes(file.FullName);
            return data;
        }
        using var manifest = JsonDocument.Parse(Read(Base + "manifest.json"));
        var m = manifest.RootElement;         if (m.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Fuchsia Gym presentation provenance differs.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(Base + path));
        }
        var trainers = new Dictionary<string, FuchsiaGymTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("name").GetString()!, row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("className").GetString()!, row.GetProperty("portrait").GetString()!,
                row.GetProperty("trainerPic").GetString()!, row.GetProperty("encounterMusicGender").GetInt32()));
        }
        if (new[] { "juggler-front", "tamer-front", "koga-front" }
            .Any(id => !files.TryGetValue(id + ".png", out var pixels) || !NativeImages.HasSize(pixels, 64, 64)))
            throw new InvalidDataException("Fuchsia Gym presentation inventory differs.");
        return new(files, trainers);
    }
}
