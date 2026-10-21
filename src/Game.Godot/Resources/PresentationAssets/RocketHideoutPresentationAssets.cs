using System.Text.Json;
using Game.Content;

internal static class RocketHideoutPresentationAssets
{
    internal static PresentationPackage Load(string root, bool portraits = false) =>
        PresentationPackage.Load(root, "content/generated/" +
            (portraits ? "sprites/trainers/rocket-hideout/" : "maps/objects/rocket-hideout-presentation/"),
            relativeOutputKeys: true);

    internal static byte[] LoadTerrain(string root)
    {
        const string directory = "content/generated/battle/terrain/rocket-hideout/";
        var manifest = PresentationPackage.ReadManifest(root, directory);
        if (manifest.GetProperty("terrain").GetString() != "BATTLE_TERRAIN_INDOOR_2")
            throw new InvalidDataException("Rocket battle terrain differs.");
        return GeneratedResourceFile.Read(root, directory + "indoor2-scene.png");
    }
}
