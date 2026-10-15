using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated original settled Gym/Leader BG3 for the admitted Gym maps.</summary>
public sealed class PretGymBattleTerrain
{
    public string GymSceneTexturePath { get; }
    public string LeaderSceneTexturePath { get; }

    private PretGymBattleTerrain(string root)
    {
        GymSceneTexturePath = Path.GetFullPath(Path.Combine(root, "content/generated/gym-battle-terrain/gym-scene.png"));
        LeaderSceneTexturePath = Path.GetFullPath(Path.Combine(root, "content/generated/gym-battle-terrain/leader-scene.png"));
    }

    // Deliberately exact; future Gym maps need their own admitted source/application selection.
    public static bool AppliesToMap(MapId mapId) => mapId.Value is "map.pewter-gym" or "map.cerulean-city-gym";

    public static PretGymBattleTerrain Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("Gym terrain path/size bound differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("Gym terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Gym terrain directory symlink rejected.");
            return File.ReadAllBytes(info.FullName);
        }
        using var json = JsonDocument.Parse(Read("content/generated/gym-battle-terrain/gym-battle-terrain.json"));
        var document = json.RootElement;
        var display = document.GetProperty("display");
        if (document.GetProperty("assets").GetArrayLength() != 4 ||
            document.GetProperty("variants").GetArrayLength() != 2 ||
            display.GetProperty("mapBattleScene").GetString() != "MAP_BATTLE_SCENE_GYM" ||
            display.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)")
            throw new InvalidDataException("Gym terrain identity differs.");
        var sceneIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            int expectedHeight = id switch
            {
                "gym-scene" or "leader-scene" => 112,
                "gym-background" or "leader-background" => 160,
                _ => throw new InvalidDataException("Gym terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != expectedHeight ||
                asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("Gym terrain asset bounds differ.");
            if (id is "gym-scene" or "leader-scene") sceneIds.Add(id);
        }
        if (!sceneIds.SetEquals(["gym-scene", "leader-scene"])) throw new InvalidDataException("Gym terrain scene missing.");
        return new(root);
    }
}
