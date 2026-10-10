using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated FireRed Youngster/Lass static fronts, with source palette expansion.</summary>
public static class PretRoute3TrainerArt
{
    public const string LockSha256 = "f90a97ca0c26f7ed9a38aedbc384b9fc1e8a29b409850ddc4ec8867f7d314de3";
    public const string GeneratedSha256 = "6b22683a427211c1d8dc7229948c42631d2b3f8ea2f4d88b8b4d67396ef7df5f";

    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        byte[] Read(string relative, string expected)
        {
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("Route3 trainer art path differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("Route3 trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Route3 trainer art directory symlink rejected.");
            byte[] bytes = File.ReadAllBytes(info.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("Route3 trainer art hash differs: " + relative);
            return bytes;
        }

        _ = Read("scripts/route3-trainer-art.lock.json", LockSha256);
        var manifest = Read("content/generated/route3-trainer-art/route3-trainer-art.json", GeneratedSha256);
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (document.GetProperty("profile").GetString() != "firered-route3-trainer-art-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("lockSha256").GetString() != LockSha256 ||
            document.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" ||
            document.GetProperty("inputs").GetArrayLength() != 9 || document.GetProperty("assets").GetArrayLength() != 2)
            throw new InvalidDataException("Route3 trainer art profile differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/route3-trainer-art/" + entry.GetProperty("path").GetString(), entry.GetProperty("sha256").GetString()!);
            if (bytes.Length != entry.GetProperty("byteLength").GetInt32())
                throw new InvalidDataException("Route3 trainer source size differs.");
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
            gitHash.AppendData(bytes);
            if (Convert.ToHexStringLower(gitHash.GetHashAndReset()) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Route3 trainer source Git blob differs.");
        }
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            if (id is not ("youngster-front" or "lass-front") || !ids.Add(id) ||
                asset.GetProperty("rgbaPath").GetString() != id + ".rgba" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 ||
                asset.GetProperty("sourceFrame").GetInt32() != 0 ||
                asset.GetProperty("sourceTrainerPicId").GetInt32() != (id == "youngster-front" ? 82 : 84) ||
                asset.GetProperty("x").GetInt32() != 144 || asset.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("Route3 trainer portrait identity differs.");
            var rgba = Read("content/generated/route3-trainer-art/" + id + ".rgba", asset.GetProperty("rgbaSha256").GetString()!);
            if (rgba.Length != 64 * 64 * 4) throw new InvalidDataException("Route3 trainer portrait size differs.");
            result.Add(new(id, 144, 8, rgba));
        }
        return result.ToImmutable();
    }
}
