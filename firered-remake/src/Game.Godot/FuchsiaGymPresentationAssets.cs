using System.Security.Cryptography;
using System.Text.Json;

internal sealed record FuchsiaGymTrainerPresentation(string Trainer, string Name, string TrainerClass,
    string ClassName, string Portrait, string TrainerPic, int EncounterMusicGender);
internal sealed record FuchsiaGymPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, FuchsiaGymTrainerPresentation> Trainers);

/// <summary>Source-authenticated static presentation. Reuses existing terrain, TM, badge and custom synthesis.</summary>
internal static class FuchsiaGymPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string ManifestSha256 = "dca53956ac73f000efc098319b6e6afb516d3c8f1491cb2969ce56485adeb116";
    internal const string LockSha256 = "3811310c38b131d5ece0631cb9269e6dcc7575e3b635f86a0100bade35f01a88";
    private const string Base = "content/generated/fuchsia-gym-presentation/";

    internal static FuchsiaGymPresentationData Load(string root)
    {
        byte[] Read(string relative, string hash, int expected)
        {
            if (Path.IsPathRooted(relative) || relative.Contains('\\') ||
                relative.Split('/').Any(p => p is "" or "." or "..") || expected is <= 0 or > 3_000_000)
                throw new InvalidDataException("Unsafe Fuchsia Gym presentation path or size.");
            var file = new FileInfo(Path.Combine(root, relative));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fuchsia Gym presentation symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Fuchsia Gym presentation directory symlink rejected.");
            if (file.Length != expected) throw new InvalidDataException("Fuchsia Gym presentation size differs: " + relative);
            var data = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(data)) != hash)
                throw new InvalidDataException("Fuchsia Gym presentation bytes differ: " + relative);
            return data;
        }
        using var locked = JsonDocument.Parse(Read(Base + "source-lock.json", LockSha256, 14009));
        using var manifest = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 23516));
        var m = manifest.RootElement; var l = locked.RootElement;
        if (m.GetProperty("schemaVersion").GetInt32() != 1 ||
            m.GetProperty("sourceCommit").GetString() != SourceCommit ||
            m.GetProperty("profile").GetString() != "firered-fuchsia-gym-presentation-v1" ||
            m.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(m.GetProperty("inputs"), l.GetProperty("files")) ||
            !JsonElement.DeepEquals(m.GetProperty("reusedInputs"), l.GetProperty("inputs")))
            throw new InvalidDataException("Fuchsia Gym presentation provenance differs.");
        foreach (var row in l.GetProperty("files").EnumerateArray())
            Read(row.GetProperty("projectPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        foreach (var row in l.GetProperty("inputs").EnumerateArray())
            Read(row.GetProperty("path").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(Base + path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        var trainers = new Dictionary<string, FuchsiaGymTrainerPresentation>(StringComparer.Ordinal);
        foreach (var row in m.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!;
            trainers.Add(id, new(id, row.GetProperty("name").GetString()!, row.GetProperty("trainerClass").GetString()!,
                row.GetProperty("className").GetString()!, row.GetProperty("portrait").GetString()!,
                row.GetProperty("trainerPic").GetString()!, row.GetProperty("encounterMusicGender").GetInt32()));
        }
        if (files.Count != 6 || trainers.Count != 7 || new[] { "juggler-front", "tamer-front", "koga-front" }
            .Any(id => !files.TryGetValue(id + ".rgba", out var pixels) || pixels.Length != 64 * 64 * 4))
            throw new InvalidDataException("Fuchsia Gym presentation inventory differs.");
        return new(files, trainers);
    }
}
