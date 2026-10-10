using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Game.Content;


/// <summary>Authenticated original static trainer frame pixels, never a runtime palette effect.</summary>
public static class PretPewterTrainerArt
{
    public const string LockSha256 = "1c0fb117c2641549e094e165fac0fdf5539253a18cbee1488b34f3e437a7e03d";
    public const string GeneratedSha256 = "64abad4c92c40ab00dee3d9aa13410ba580de92a3d6b5b24dd326a0b6fb331e8";
    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        byte[] Read(string relative, string expected)
        {
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is ".." or ".") || relative.Contains('\\'))
                throw new InvalidDataException("Trainer art path differs.");
            string path = Path.GetFullPath(Path.Combine(root, relative));
            var info = new FileInfo(path);
            if (info.LinkTarget is not null) throw new InvalidDataException("Trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Trainer art directory symlink rejected.");
            byte[] bytes = File.ReadAllBytes(path);
            if (Digest(bytes) != expected) throw new InvalidDataException("Trainer art hash differs: " + relative);
            return bytes;
        }
        _ = Read("scripts/pewter-trainer-art.lock.json", LockSha256);
        var manifest = Read("content/generated/pewter-trainer-art/pewter-trainer-art.json", GeneratedSha256);
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (document.GetProperty("profile").GetString() != "firered-pewter-trainer-art-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("lockSha256").GetString() != LockSha256 ||
            document.GetProperty("inputs").GetArrayLength() != 9 || document.GetProperty("assets").GetArrayLength() != 2)
            throw new InvalidDataException("Trainer art profile differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/pewter-trainer-art/" + entry.GetProperty("path").GetString(), entry.GetProperty("sha256").GetString()!);
            if (bytes.Length != entry.GetProperty("byteLength").GetInt32()) throw new InvalidDataException("Trainer source size differs.");
        }
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            if (id is not ("camper-front" or "brock-front") || asset.GetProperty("rgbaPath").GetString() != id + ".rgba" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 || asset.GetProperty("sourceFrame").GetInt32() != 0)
                throw new InvalidDataException("Trainer portrait identity differs.");
            var rgba = Read("content/generated/pewter-trainer-art/" + id + ".rgba", asset.GetProperty("rgbaSha256").GetString()!);
            if (rgba.Length != 64 * 64 * 4) throw new InvalidDataException("Trainer portrait size differs.");
            result.Add(new(id, asset.GetProperty("x").GetInt32(), asset.GetProperty("y").GetInt32(), rgba));
        }
        return result.ToImmutable();
    }
}
