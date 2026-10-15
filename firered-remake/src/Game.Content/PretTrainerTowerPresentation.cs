using System.Text.Json;

namespace Game.Content;

/// <summary>Original Trainer Tower sprite/palette resources. No audio or new player-owned species are admitted.</summary>
public static class PretTrainerTowerPresentation
{
    public const string Profile = "firered-trainer-tower-presentation-v1";
    public static JsonElement Load(string root)
    {
        var source = new SilphSourcePackage(root,"trainer-tower-presentation",            "scripts/trainer-tower-presentation.lock.json");
        var catalog = source.Document("catalog.json");
        SilphSourcePackage.Need(            catalog.GetProperty("sprites").GetArrayLength() == 80 && catalog.GetProperty("trainerPortraits").GetArrayLength() == 37 &&
            catalog.GetProperty("itemIcons").GetArrayLength() == 15 && catalog.GetProperty("assets").GetArrayLength() == 132 &&
            catalog.GetProperty("newOpponentSpecies").GetArrayLength() == 18 && catalog.GetProperty("newOwnedSpecies").GetArrayLength() == 0 &&
            catalog.GetProperty("doorAnimations").GetArrayLength() == 2 && !catalog.GetProperty("audioIncluded").GetBoolean(), "Trainer Tower presentation scope differs.");
        foreach (var asset in catalog.GetProperty("assets").EnumerateArray())
        {
            string prefix = "content/generated/trainer-tower-presentation/";
            string png = asset.GetProperty("texturePath").GetString()!;
            SilphSourcePackage.Need(png.StartsWith(prefix, StringComparison.Ordinal), "Tower asset path differs.");
            var bytes = PngRgba8.Decode(source.Outputs[png[prefix.Length..]], asset.GetProperty("width").GetInt32(), asset.GetProperty("height").GetInt32());
            SilphSourcePackage.Need(bytes.Length == asset.GetProperty("width").GetInt32() * asset.GetProperty("height").GetInt32() * 4 , "Tower asset payload differs.");
            foreach (var input in asset.GetProperty("sourcePaths").EnumerateArray())
                SilphSourcePackage.Need(source.Raw.ContainsKey(input.GetString()!), "Tower asset source missing.");
        }
        return catalog;
    }
}
