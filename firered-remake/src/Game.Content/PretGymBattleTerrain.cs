using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated original settled Gym/Leader BG3 for the admitted Gym maps.</summary>
public sealed class PretGymBattleTerrain
{
    public const string LockSha256 = "2cdf1d129577cdaffb1923e7e191f7ace203c50b10ed29f79164878efdc28878";
    public const string CatalogSha256 = "070890afe9fc7fee021d9c93f519b2451c9dae8de6ae47eb77efb2e4d62043c3";
    public const string GymSceneSha256 = "3b4dc43162492edf48cbfbf65db03a36efb60e4660b7012f08a17ddf91d6e92f";
    public const string LeaderSceneSha256 = "3b4dc43162492edf48cbfbf65db03a36efb60e4660b7012f08a17ddf91d6e92f";
    public string GymSceneTexturePath { get; }
    public string LeaderSceneTexturePath { get; }
    public string GymSceneRgbaSha256 { get; }
    public string LeaderSceneRgbaSha256 { get; }

    private PretGymBattleTerrain(string root, string gymRgba, string leaderRgba)
    {
        GymSceneTexturePath = Path.GetFullPath(Path.Combine(root, "content/generated/gym-battle-terrain/gym-scene.png"));
        LeaderSceneTexturePath = Path.GetFullPath(Path.Combine(root, "content/generated/gym-battle-terrain/leader-scene.png"));
        GymSceneRgbaSha256 = gymRgba;
        LeaderSceneRgbaSha256 = leaderRgba;
    }

    // Deliberately exact; future Gym maps need their own admitted source/application selection.
    public static bool AppliesToMap(MapId mapId) => mapId.Value is "map.pewter-gym" or "map.cerulean-city-gym";

    public static PretGymBattleTerrain Load(string root)
    {
        byte[] Read(string relative, string expected, int expectedLength)
        {
            if (expectedLength is <= 0 or > 1_000_000 || Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("Gym terrain path/size bound differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("Gym terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Gym terrain directory symlink rejected.");
            using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != expectedLength) throw new InvalidDataException("Gym terrain size differs: " + relative);
            byte[] bytes = new byte[expectedLength];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("Gym terrain hash differs: " + relative);
            return bytes;
        }

        _ = Read("scripts/gym-battle-terrain.lock.json", LockSha256, 4955);
        _ = Read("content/generated/gym-battle-terrain/manifest.json", "7cd7d583faa5a0d8da3d784f7d10a6159cb8f5b4c8e15ebf731cd9d505055ba2", 5918);
        using var json = JsonDocument.Parse(Read("content/generated/gym-battle-terrain/gym-battle-terrain.json", CatalogSha256, 19138));
        var document = json.RootElement;
        var display = document.GetProperty("display");
        if (document.GetProperty("profile").GetString() != "firered-gym-battle-terrain-static-v1" ||
            document.GetProperty("sourceCommit").GetString() != "037335f4c725d7c9aecdac87066f2002b4bd7e14" ||
            document.GetProperty("repository").GetString() != "pret/pokefirered" ||
            document.GetProperty("inputs").GetArrayLength() != 13 || document.GetProperty("assets").GetArrayLength() != 4 ||
            document.GetProperty("variants").GetArrayLength() != 2 ||
            display.GetProperty("mapBattleScene").GetString() != "MAP_BATTLE_SCENE_GYM" ||
            display.GetProperty("sourceTileCount").GetInt32() != 77 ||
            display.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)")
            throw new InvalidDataException("Gym terrain identity differs.");
        foreach (var entry in document.GetProperty("inputs").EnumerateArray())
        {
            var bytes = Read("content/raw/gym-battle-terrain/" + entry.GetProperty("path").GetString(),
                entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            using var gitHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            gitHash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
            gitHash.AppendData(bytes);
            if (Convert.ToHexStringLower(gitHash.GetHashAndReset()) != entry.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Gym terrain source Git blob differs.");
        }
        string? gymRgba = null, leaderRgba = null;
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            (int Height, int Bytes) expected = id switch
            {
                "gym-scene" or "leader-scene" => (112, 107705),
                "gym-background" or "leader-background" => (160, 153838),
                _ => throw new InvalidDataException("Gym terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != expected.Height || asset.GetProperty("byteLength").GetInt32() != expected.Bytes ||
                asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("Gym terrain asset bounds differ.");
            _ = Read("content/generated/gym-battle-terrain/" + id + ".png", asset.GetProperty("sha256").GetString()!, expected.Bytes);
            if (id == "gym-scene") gymRgba = asset.GetProperty("rgbaSha256").GetString();
            if (id == "leader-scene") leaderRgba = asset.GetProperty("rgbaSha256").GetString();
        }
        return new(root, gymRgba ?? throw new InvalidDataException("Gym scene missing."),
            leaderRgba ?? throw new InvalidDataException("Leader scene missing."));
    }
}
