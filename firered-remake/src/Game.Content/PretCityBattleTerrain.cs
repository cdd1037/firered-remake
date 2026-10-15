using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretCityBattleScene(string Terrain, string TexturePath);

/// <summary>Authenticated settled Sand/Plain and S.S. Anne exterior Building BG3.</summary>
public sealed class PretCityBattleTerrain
{
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
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("City terrain path is invalid.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("City terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("City terrain directory symlink rejected.");
            return File.ReadAllBytes(info.FullName);
        }
        using var json = JsonDocument.Parse(Read("content/generated/battle/terrain/city/city-battle-terrain.json"));
        var document = json.RootElement;
        if (document.GetProperty("assets").GetArrayLength() != 6 ||
            document.GetProperty("variants").GetArrayLength() != 3 )
            throw new InvalidDataException("City terrain identity differs.");
        var scenes = new Dictionary<string, PretCityBattleScene>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            int expectedHeight = id switch
            {
                "plain-scene" or "sand-scene" or "building-scene" => 112,
                "plain-background" or "sand-background" or "building-background" => 160,
                _ => throw new InvalidDataException("City terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != expectedHeight ||
                asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("City terrain asset bounds differ.");
            string relative = "content/generated/battle/terrain/city/" + id + ".png";
            if (expectedHeight == 112)
                scenes.Add(id, new(id[..^6],Path.GetFullPath(Path.Combine(root, relative))));
        }
        return new(scenes);
    }
}
