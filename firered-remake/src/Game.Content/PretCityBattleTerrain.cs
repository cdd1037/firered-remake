using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretCityBattleScene(string Terrain, string TexturePath, string Sha256, string RgbaSha256);

/// <summary>Authenticated settled Sand/Plain and S.S. Anne exterior Building BG3.</summary>
public sealed class PretCityBattleTerrain
{
    public const string LockSha256 = "c547f232bd86ba40f8384f73c9e77145fdf3a6980d1fc9ea90a6de6375c92849";
    public const string CatalogSha256 = "f557842cc5847eeb0e4754af285412af5a9c8194137f5ea7c781172e181c91d5";
    public const string PlainSceneSha256 = "2372d185d4870a88bd1961c1c5485f72e704e3f367037910d8fbd7ebe50f386a";
    public const string SandSceneSha256 = "99d52776a2dec02613c77087f264833956b98e7446fcb402eac090e181b15bf3";
    public const string BuildingSceneSha256 = "70ec0124f037ac796fbf747980e31e4bb78c255e774a7c833cc63e1d2281ddce";
    public PretCityBattleScene PlainScene { get; }
    public PretCityBattleScene SandScene { get; }
    public PretCityBattleScene BuildingScene { get; }
    public string PlainSceneTexturePath => PlainScene.TexturePath;
    public string SandSceneTexturePath => SandScene.TexturePath;
    public string BuildingSceneTexturePath => BuildingScene.TexturePath;

    private PretCityBattleTerrain(Dictionary<string, PretCityBattleScene> scenes)
    {
        PlainScene = scenes["plain-scene"];
        SandScene = scenes["sand-scene"];
        BuildingScene = scenes["building-scene"];
    }

    // Exact current maps; no prefix or unverified future-map selection.
    public static bool AppliesToMap(MapId mapId) => mapId.Value is
        "map.pallet-town" or "map.viridian-city" or "map.pewter-city" or "map.cerulean-city" or "map.vermilion-city" or
        "map.route-4" or "map.route-6" or "map.route-22" or "map.route-24" or "map.route-25" or "map.ssanne-exterior";

    /// <summary>
    /// Use the player's current source behavior, not the targeted fishing tile. Caller retains special battle
    /// overrides and gates the current city-services profile. Null means outside this bounded source selection.
    /// </summary>
    public PretCityBattleScene? TryGetScene(MapId mapId, int? playerSourceBehavior)
    {
        if (!AppliesToMap(mapId)) return null;
        return playerSourceBehavior switch
        {
            0x17 or 0x21 => SandScene, // Source sand test precedes map-type selection.
            0 => mapId.Value == "map.ssanne-exterior" ? BuildingScene : PlainScene,
            _ => null
        };
    }

    public static PretCityBattleTerrain Load(string root)
    {
        byte[] Read(string relative, string expected, int expectedLength)
        {
            if (expectedLength is <= 0 or > 1_000_000 || Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("City terrain path/size bound differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("City terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("City terrain directory symlink rejected.");
            using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != expectedLength) throw new InvalidDataException("City terrain size differs: " + relative);
            byte[] bytes = new byte[expectedLength];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("City terrain hash differs: " + relative);
            return bytes;
        }

        _ = Read("content/sources/city-battle-terrain.lock.json", LockSha256, 10507);
        _ = Read("content/generated/city-battle-terrain/manifest.json", "4828d2790443d37aa6a6716318e2bcc6e33f8d2719d001540a18bb3b501b42fc", 11796);
        using var json = JsonDocument.Parse(Read("content/generated/city-battle-terrain/city-battle-terrain.json", CatalogSha256, 26461));
        var document = json.RootElement;
        if (document.GetProperty("profile").GetString() != "firered-city-battle-terrain-static-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("repository").GetString() != "pret/pokefirered" ||
            document.GetProperty("inputs").GetArrayLength() != 28 || document.GetProperty("assets").GetArrayLength() != 6 ||
            document.GetProperty("variants").GetArrayLength() != 3 || document.GetProperty("maps").GetArrayLength() != 11)
            throw new InvalidDataException("City terrain identity differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/city-battle-terrain/" + entry.GetProperty("path").GetString(),
                entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
            gitHash.AppendData(bytes);
            if (Convert.ToHexStringLower(gitHash.GetHashAndReset()) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("City terrain source Git blob differs.");
        }
        var scenes = new Dictionary<string, PretCityBattleScene>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            (int Height, int Bytes) expected = id switch
            {
                "plain-scene" or "sand-scene" or "building-scene" => (112, 107705),
                "plain-background" or "sand-background" or "building-background" => (160, 153838),
                _ => throw new InvalidDataException("City terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != expected.Height || asset.GetProperty("byteLength").GetInt32() != expected.Bytes ||
                asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("City terrain asset bounds differ.");
            string relative = "content/generated/city-battle-terrain/" + id + ".png";
            string hash = asset.GetProperty("sha256").GetString()!;
            _ = Read(relative, hash, expected.Bytes);
            if (expected.Height == 112)
                scenes.Add(id, new(id[..^6], Path.GetFullPath(Path.Combine(root, relative)), hash, asset.GetProperty("rgbaSha256").GetString()!));
        }
        return new(scenes);
    }
}
