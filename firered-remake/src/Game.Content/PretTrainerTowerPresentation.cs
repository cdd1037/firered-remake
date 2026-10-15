using System.Text.Json;

namespace Game.Content;

/// <summary>Original Trainer Tower sprite/palette resources. No audio or new player-owned species are admitted.</summary>
public static class PretTrainerTowerPresentation
{
    public const string Profile = "firered-trainer-tower-presentation-v1";
    public static JsonElement Load(string root)
    {
        var files = PretSilphPresentationSource.LoadGenerated(root, "content/generated/trainer-tower-presentation/");
        var catalog = SafariSource.Parse<JsonElement>(files["catalog.json"]);
        SilphSourcePackage.Need(            catalog.GetProperty("sprites").GetArrayLength() == 80 && catalog.GetProperty("trainerPortraits").GetArrayLength() == 37 &&
            catalog.GetProperty("itemIcons").GetArrayLength() == 15 && catalog.GetProperty("assets").GetArrayLength() == 132 &&
            catalog.GetProperty("newOpponentSpecies").GetArrayLength() == 18 && catalog.GetProperty("newOwnedSpecies").GetArrayLength() == 0 &&
            catalog.GetProperty("doorAnimations").GetArrayLength() == 2 && !catalog.GetProperty("audioIncluded").GetBoolean(), "Trainer Tower presentation scope differs.");
        foreach (var asset in catalog.GetProperty("assets").EnumerateArray())
        {
            string prefix = "content/generated/trainer-tower-presentation/";
            string png = asset.GetProperty("texturePath").GetString()!;
            SilphSourcePackage.Need(png.StartsWith("content/generated/", StringComparison.Ordinal), "Tower asset path differs.");
            SilphSourcePackage.Need(asset.GetProperty("width").GetInt32() > 0 && asset.GetProperty("height").GetInt32() > 0, "Tower asset dimensions differ.");
            var bytes = png.StartsWith(prefix, StringComparison.Ordinal)
                ? files[png[prefix.Length..]] : SafariSource.Read(root, png);
            SilphSourcePackage.Need(bytes.Length > 0, "Tower asset payload is empty.");
        }
        return catalog;
    }
}
