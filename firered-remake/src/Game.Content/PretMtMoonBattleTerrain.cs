using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated settled BG3 for the three original Mt. Moon cave maps.</summary>
public sealed class PretMtMoonBattleTerrain
{
    public const string LockSha256 = "2c49265b99d0b27c2da7810ba617e3fc7e732648fb3468aa8cd596d391c0a786";
    public const string CatalogSha256 = "d3f8ca887a3a4217acd311ed824b52c92140d50308f1d2efe83a7251032383c5";
    public const string SceneSha256 = "1c96c7a6d5db2b45684e5f5390189fb260a527a0769865c5e72f66d081fa9213";
    public string SceneTexturePath { get; }
    public string SceneRgbaSha256 { get; }
    private PretMtMoonBattleTerrain(string sceneTexturePath, string sceneRgbaSha256)
    { SceneTexturePath = sceneTexturePath; SceneRgbaSha256 = sceneRgbaSha256; }

    // Deliberately bounded to the three authenticated maps; other underground areas are not inferred.
    public static bool AppliesToMap(MapId mapId) => mapId.Value is "map.mt-moon-1f" or "map.mt-moon-b1f" or "map.mt-moon-b2f";

    public static PretMtMoonBattleTerrain Load(string root)
    {
        byte[] Read(string relative, string expected, int expectedLength)
        {
            if (expectedLength is <= 0 or > 1_000_000 || Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("MtMoon terrain path/size bound differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("MtMoon terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("MtMoon terrain directory symlink rejected.");
            using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != expectedLength) throw new InvalidDataException("MtMoon terrain size differs: " + relative);
            byte[] bytes = new byte[expectedLength]; stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("MtMoon terrain hash differs: " + relative);
            return bytes;
        }

        _ = Read("scripts/mtmoon-battle-terrain.lock.json", LockSha256, 5605);
        _ = Read("content/generated/mtmoon-battle-terrain/manifest.json", "19f28184009b609ba5f504f1a9534531a9af6cb1526d3ffb3373d9bdb294f354", 6253);
        using var json = JsonDocument.Parse(Read("content/generated/mtmoon-battle-terrain/mtmoon-battle-terrain.json", CatalogSha256, 14313));
        var document = json.RootElement; var display = document.GetProperty("display");
        if (document.GetProperty("profile").GetString() != "firered-mtmoon-battle-terrain-static-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("repository").GetString() != "pret/pokefirered" ||
            document.GetProperty("inputs").GetArrayLength() != 15 || document.GetProperty("assets").GetArrayLength() != 2 ||
            display.GetProperty("terrain").GetString() != "BATTLE_TERRAIN_CAVE" || display.GetProperty("terrainId").GetInt32() != 7 ||
            display.GetProperty("sourceTileCount").GetInt32() != 84 || display.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)")
            throw new InvalidDataException("MtMoon terrain identity differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/mtmoon-battle-terrain/" + entry.GetProperty("path").GetString(),
                entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0")); gitHash.AppendData(bytes);
            if (Convert.ToHexStringLower(gitHash.GetHashAndReset()) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("MtMoon terrain source Git blob differs.");
        }
        string? sceneRgbaSha256 = null;
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            (int Height, int Bytes) expected = id switch
            {
                "background-scene" => (112, 107705), "background" => (160, 153838),
                _ => throw new InvalidDataException("MtMoon terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != expected.Height || asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("MtMoon terrain asset bounds differ.");
            _ = Read("content/generated/mtmoon-battle-terrain/" + id + ".png", asset.GetProperty("sha256").GetString()!, expected.Bytes);
            if (id == "background-scene") sceneRgbaSha256 = asset.GetProperty("rgbaSha256").GetString()!;
        }
        return new(Path.GetFullPath(Path.Combine(root, "content/generated/mtmoon-battle-terrain/background-scene.png")),
            sceneRgbaSha256 ?? throw new InvalidDataException("MtMoon terrain scene missing."));
    }
}
