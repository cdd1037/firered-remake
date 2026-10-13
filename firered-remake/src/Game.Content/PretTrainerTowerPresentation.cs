using System.Text.Json;

namespace Game.Content;

/// <summary>Original Trainer Tower sprite/palette resources. No audio or new player-owned species are admitted.</summary>
public static class PretTrainerTowerPresentation
{
    public const string Profile = "firered-trainer-tower-presentation-v1";
    public const string FullSourceHash = "af7872d4a722b9261a088016b1c812ae9b78688805b8dd7c6b697106a92e91b3";
    public const string ManifestSha256 = "311f9ab7ff2cc2a5561313c7ee56cb0609a54c2571d09e761a62f913d581056d";
    public const string LockSha256 = "5620e4232381a2320d2f000ce85e1c01f840acfcf805eceb9e1d0cdaecbd1fa3";
    public static JsonElement Load(string root)
    {
        var source = new SilphSourcePackage(root, "trainer-tower-presentation", Profile,
            "scripts/trainer-tower-presentation.lock.json", LockSha256, 60228, ManifestSha256, 43760, 201, 265);
        var catalog = source.Document("catalog.json");
        SilphSourcePackage.Need(source.Manifest.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("sprites").GetArrayLength() == 80 && catalog.GetProperty("trainerPortraits").GetArrayLength() == 37 &&
            catalog.GetProperty("itemIcons").GetArrayLength() == 15 && catalog.GetProperty("assets").GetArrayLength() == 132 &&
            catalog.GetProperty("newOpponentSpecies").GetArrayLength() == 18 && catalog.GetProperty("newOwnedSpecies").GetArrayLength() == 0 &&
            catalog.GetProperty("doorAnimations").GetArrayLength() == 2 && !catalog.GetProperty("audioIncluded").GetBoolean(), "Trainer Tower presentation scope differs.");
        foreach (var asset in catalog.GetProperty("assets").EnumerateArray())
        {
            string prefix = "content/generated/trainer-tower-presentation/";
            string rgba = asset.GetProperty("rgbaPath").GetString()!, png = asset.GetProperty("texturePath").GetString()!;
            SilphSourcePackage.Need(rgba.StartsWith(prefix, StringComparison.Ordinal) && png.StartsWith(prefix, StringComparison.Ordinal), "Tower asset path differs.");
            var bytes = source.Outputs[rgba[prefix.Length..]];
            SilphSourcePackage.Need(bytes.Length == asset.GetProperty("width").GetInt32() * asset.GetProperty("height").GetInt32() * 4 &&
                SafariSource.Hash(bytes) == asset.GetProperty("rgbaSha256").GetString() &&
                SafariSource.Hash(source.Outputs[png[prefix.Length..]]) == asset.GetProperty("sha256").GetString(), "Tower asset payload differs.");
            foreach (var input in asset.GetProperty("sourcePaths").EnumerateArray())
                SilphSourcePackage.Need(source.Raw.ContainsKey(input.GetString()!), "Tower asset source missing.");
        }
        return catalog;
    }
    public static void ValidateEvidence(string root) => _ = Load(root);
}
