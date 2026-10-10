using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated FireRed Super Nerd/Hiker/male Rocket grunt static fronts, with source palette expansion.</summary>
public static class PretMtMoonTrainerArt
{
    public const string LockSha256 = "16e4fd0fa4f8a3ed416971c6e1c20693bac5fdc9640107db7c5b277c16b513c8";
    public const string GeneratedSha256 = "dfb25c3c715441b3b965eb7997167065942e9d387249c7feb244ed7b710d7f06";

    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        byte[] Read(string relative, string expected, int expectedLength)
        {
            if (expectedLength is <= 0 or > 100_000) throw new InvalidDataException("MtMoon trainer art size bound differs.");
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("MtMoon trainer art path differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("MtMoon trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("MtMoon trainer art directory symlink rejected.");
            using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != expectedLength) throw new InvalidDataException("MtMoon trainer art size differs: " + relative);
            byte[] bytes = new byte[expectedLength];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw new InvalidDataException("MtMoon trainer art trailing data: " + relative);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("MtMoon trainer art hash differs: " + relative);
            return bytes;
        }

        _ = Read("scripts/mtmoon-trainer-art.lock.json", LockSha256, 2796);
        var manifest = Read("content/generated/mtmoon-trainer-art/mtmoon-trainer-art.json", GeneratedSha256, 4688);
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (document.GetProperty("profile").GetString() != "firered-mtmoon-trainer-art-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("lockSha256").GetString() != LockSha256 ||
            document.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" ||
            document.GetProperty("inputs").GetArrayLength() != 11 || document.GetProperty("assets").GetArrayLength() != 3)
            throw new InvalidDataException("MtMoon trainer art profile differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/mtmoon-trainer-art/" + entry.GetProperty("path").GetString(), entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            if (bytes.Length != entry.GetProperty("byteLength").GetInt32())
                throw new InvalidDataException("MtMoon trainer source size differs.");
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
            gitHash.AppendData(bytes);
            if (Convert.ToHexStringLower(gitHash.GetHashAndReset()) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("MtMoon trainer source Git blob differs.");
        }
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            (string Stem, int PicId) expected = id switch
            {
                "super-nerd-front" => ("super_nerd", 89),
                "hiker-front" => ("hiker", 90),
                "rocket-m-front" => ("rocket_grunt_m", 109),
                _ => throw new InvalidDataException("MtMoon trainer portrait identity differs.")
            };
            if (!ids.Add(id) ||
                asset.GetProperty("rgbaPath").GetString() != id + ".rgba" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 ||
                asset.GetProperty("sourceFrame").GetInt32() != 0 ||
                asset.GetProperty("sourceImage").GetString() != "graphics/trainers/front_pics/" + expected.Stem + "_front_pic.png" ||
                asset.GetProperty("sourcePalette").GetString() != "graphics/trainers/palettes/" + expected.Stem + ".pal" ||
                asset.GetProperty("sourceTrainerPicId").GetInt32() != expected.PicId ||
                asset.GetProperty("x").GetInt32() != 144 || asset.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("MtMoon trainer portrait identity differs.");
            var rgba = Read("content/generated/mtmoon-trainer-art/" + id + ".rgba", asset.GetProperty("rgbaSha256").GetString()!, 64 * 64 * 4);
            if (rgba.Length != 64 * 64 * 4) throw new InvalidDataException("MtMoon trainer portrait size differs.");
            result.Add(new(id, 144, 8, rgba));
        }
        return result.ToImmutable();
    }
}
