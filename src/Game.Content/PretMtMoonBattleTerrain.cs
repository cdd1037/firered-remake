using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated settled BG3 for the three original Mt. Moon cave maps.</summary>
public sealed class PretMtMoonBattleTerrain
{
    public string SceneTexturePath { get; }
    private PretMtMoonBattleTerrain(string sceneTexturePath)
    { SceneTexturePath = sceneTexturePath; }

    // Deliberately bounded to the three authenticated maps; other underground areas are not inferred.
    public static bool AppliesToMap(MapId mapId) => mapId.Value is "map.mt-moon-1f" or "map.mt-moon-b1f" or "map.mt-moon-b2f";

    public static PretMtMoonBattleTerrain Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) ||
                relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("MtMoon terrain path/size bound differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("MtMoon terrain symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("MtMoon terrain directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
        }
        using var json = JsonDocument.Parse(Read("content/generated/battle/terrain/mtmoon/mtmoon-battle-terrain.json"));
        var document = json.RootElement; var display = document.GetProperty("display");
        if (document.GetProperty("assets").GetArrayLength() != 2 ||
            display.GetProperty("terrain").GetString() != "BATTLE_TERRAIN_CAVE" || display.GetProperty("terrainId").GetInt32() != 7 ||
            display.GetProperty("sourceTileCount").GetInt32() != 84 || display.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)")
            throw new InvalidDataException("MtMoon terrain identity differs.");
        bool hasScene = false;
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            int height = id switch
            {
                "background-scene" => 112, "background" => 160,
                _ => throw new InvalidDataException("MtMoon terrain asset identity differs.")
            };
            if (asset.GetProperty("texturePath").GetString() != id + ".png" || asset.GetProperty("width").GetInt32() != 240 ||
                asset.GetProperty("height").GetInt32() != height || asset.GetProperty("x").GetInt32() != 0 || asset.GetProperty("y").GetInt32() != 0)
                throw new InvalidDataException("MtMoon terrain asset bounds differ.");
            if (id == "background-scene") hasScene = true;
        }
        if (!hasScene) throw new InvalidDataException("MtMoon terrain scene missing.");
        return new(Path.GetFullPath(Path.Combine(root, "content/generated/battle/terrain/mtmoon/background-scene.png")));
    }
}
