using System.Security.Cryptography;
using System.Text.Json;

internal sealed record ViridianGymTrainerPresentation(string Trainer, int SourceTrainerId, string Name,
    string TrainerClass, string ClassName, string TrainerPic, string Portrait, int EncounterMusicGender);
internal sealed record ViridianGymPortraitPresentation(string Id, string TrainerPic, int SourceTrainerPicId,
    string RgbaPath, string RgbaSha256, bool Inherited);
internal sealed record ViridianGymPresentationData(Dictionary<string, byte[]> Files,
    Dictionary<string, ViridianGymTrainerPresentation> Trainers, ViridianGymPortraitPresentation[] Portraits,
    string MachineIconRgbaSha256);

/// <summary>Exact local source closure; two new portraits and authenticated predecessor resources.</summary>
internal static class ViridianGymPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string ManifestSha256 = "830d64f21e660f9aa8fce5365504b5b5cc1959dfb9848e88b3610c24c94635b4";
    internal const string LockSha256 = "8467efb0a1fd76293dbe1c24ea34421331d3df6630e69201797621e421086236";
    internal const string Base = "content/generated/viridian-gym-presentation/";

    internal static ViridianGymPresentationData Load(string root)
    {
        byte[] Read(string path, string hash, int size)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or "..") || size is <= 0 or > 3_000_000)
                throw new InvalidDataException("Unsafe Viridian Gym presentation path/size.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Viridian Gym resource symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Viridian Gym resource directory symlink rejected.");
            if (file.Length != size) throw new InvalidDataException("Viridian Gym resource size differs: " + path);
            byte[] bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Viridian Gym resource bytes differ: " + path);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/viridian-gym-presentation.lock.json", LockSha256, 11166));
        using var manifest = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 19595));
        var l = locked.RootElement; var m = manifest.RootElement;
        if (m.GetProperty("schemaVersion").GetInt32() != 1 || m.GetProperty("profile").GetString() != "firered-viridian-gym-presentation-v1" ||
            m.GetProperty("sourceCommit").GetString() != SourceCommit || m.GetProperty("lockSha256").GetString() != LockSha256 ||
            !JsonElement.DeepEquals(m.GetProperty("inputs"), l.GetProperty("files")) ||
            !JsonElement.DeepEquals(m.GetProperty("inheritedInputs"), l.GetProperty("inheritedInputs")))
            throw new InvalidDataException("Viridian Gym presentation identity differs.");
        foreach (var row in l.GetProperty("files").EnumerateArray())
            Read("content/raw/viridian-gym-presentation/" + row.GetProperty("path").GetString(), row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in l.GetProperty("inheritedInputs").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
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
            row.GetProperty("rgbaPath").GetString()!, row.GetProperty("rgbaSha256").GetString()!, row.GetProperty("inherited").GetBoolean())).ToArray();
        if (trainers.Count != 9 || portraits.Length != 4 || portraits.Count(p => !p.Inherited) != 2 ||
            !portraits.Select(p => p.SourceTrainerPicId).ToHashSet().SetEquals([103, 105, 108, 110]) ||
            portraits.Any(p => !files.TryGetValue(p.RgbaPath, out var rgba) || rgba.Length != 64 * 64 * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) != p.RgbaSha256))
            throw new InvalidDataException("Viridian Gym presentation inventory differs.");
        return new(files, trainers, portraits, m.GetProperty("reward").GetProperty("iconRgbaSha256").GetString()!);
    }
}
