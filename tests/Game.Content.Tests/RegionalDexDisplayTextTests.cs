using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class RegionalDexDisplayTextTests
{
    private const string PokedexPath = "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json";
    private static readonly (string Region, int Species)[] Regions =
    [
        ("eevee", 133), ("snorlax", 143), ("silph", 65), ("saffron-gym", 80),
        ("cinnabar", 78), ("surf", 54), ("safari", 113), ("fuchsia-route", 83)
    ];
    private static PretPokedexMenu Canonical => TestAssets.Cached(() => PretPokedexMenu.Load(TestAssets.Root));

    public static IEnumerable<object[]> DisplayTextCases => Regions.SelectMany(region =>
        new[] { "name", "category", "displayCategory", "description" }.Select(textField =>
            new object[] { region.Region, region.Species, textField }));

    [TestMethod, DynamicData(nameof(DisplayTextCases))]
    public void RegionalArtUsesEditedCanonicalDisplayText(string region, int species, string field)
    {
        using var output = CopyPackage(region);
        const string replacement = "A replaceable display 文本 longer than a nickname";
        TestAssets.EditJson(output.Path, PokedexPath, document => Entry(document, species)[field] = replacement);

        var canonical = PretPokedexMenu.Load(output.Path).Entries[species];
        var (dex, capture) = Load(region, output.Path);
        var original = Canonical.Entries[species];
        Assert.AreEqual(canonical.Name, dex[species].Name);
        Assert.AreEqual(canonical.Category, dex[species].Category);
        Assert.AreEqual(canonical.DisplayCategory, dex[species].DisplayCategory);
        Assert.AreEqual(canonical.Description, dex[species].Description);
        Assert.AreEqual(canonical.Name, capture[species].Name);
        Assert.AreEqual(canonical.Category, capture[species].Category);
        Assert.AreEqual(canonical.DisplayCategory, capture[species].DisplayCategory);
        Assert.AreEqual(canonical.Description, capture[species].Description);
        Assert.AreEqual(original.SourceSpeciesId, dex[species].SourceSpeciesId);
        Assert.AreEqual(original.NationalNumber, dex[species].NationalNumber);
        Assert.AreEqual(original.HeightDecimeters, dex[species].HeightDecimeters);
        Assert.AreEqual(original.WeightHectograms, dex[species].WeightHectograms);
        Assert.AreEqual(original.Type1, dex[species].Type1);
        Assert.AreEqual(original.Type2, dex[species].Type2);
        Assert.AreEqual(dex[species].Portrait!.TexturePath, capture[species].FrontSpritePath);
        Assert.AreEqual(dex[species].Footprint!.TexturePath, capture[species].FootprintPath);
    }

    [TestMethod]
    [DataRow("name", "")]
    [DataRow("name", " \t\n")]
    [DataRow("category", "")]
    [DataRow("category", " \t\n")]
    [DataRow("description", "")]
    [DataRow("description", " \t\n")]
    public void CanonicalDexStillRequiresNonblankDisplayText(string field, string value)
    {
        using var output = CopyPackage("cinnabar");
        TestAssets.EditJson(output.Path, PokedexPath, document => Entry(document, 78)[field] = value);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Load("cinnabar", output.Path));
        Assert.AreEqual("Pokédex display text is missing.", error.Message);
    }

    [TestMethod]
    [DataRow("species")]
    [DataRow("height")]
    [DataRow("weight")]
    [DataRow("geometry")]
    public void RegionalArtStillRejectsDamagedIdentityMeasuresAndGeometry(string change)
    {
        using var output = CopyPackage("cinnabar");
        TestAssets.EditJson(output.Path, "content/generated/sprites/pokemon/cinnabar/manifest.json", document =>
        {
            var entry = document["dex"]!.AsArray().Single(row => row!["sourceSpeciesId"]!.GetValue<int>() == 78)!;
            switch (change)
            {
                case "species": entry["sourceSpeciesId"] = 79; break;
                case "height": entry["heightDecimeters"] = entry["heightDecimeters"]!.GetValue<int>() + 1; break;
                case "weight": entry["weightHectograms"] = entry["weightHectograms"]!.GetValue<int>() + 1; break;
                case "geometry": document["sprites"]![0]!["width"] = 63; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Load("cinnabar", output.Path));
        Assert.AreEqual(change switch
        {
            "species" => "Cinnabar Dex identity differs.",
            "geometry" => "Cinnabar sprite identity differs.",
            _ => "Cinnabar Dex differs from full authenticated source."
        }, error.Message);
    }

    private static JsonNode Entry(JsonNode document, int species) => document["entries"]!.AsArray()
        .Single(entry => entry!["sourceSpeciesId"]!.GetValue<int>() == species)!;

    private static TestAssets.TemporaryDirectory CopyPackage(string region)
    {
        var output = TestAssets.TemporaryOutput();
        string directory = "content/generated/sprites/pokemon/" + region;
        TestAssets.CopyFiles(output.Path, Directory.EnumerateFiles(Path.Combine(TestAssets.Root, directory),
            "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(TestAssets.Root, path)).Append(PokedexPath));
        return output;
    }

    private static (IReadOnlyDictionary<int, PokedexMenuEntry> Dex, IReadOnlyDictionary<int, CaptureDexEntry> Capture)
        Load(string region, string root)
    {
        switch (region)
        {
            case "eevee": var eevee = PretEeveeArt.Load(root); return (eevee.DexEntries, eevee.CaptureDexEntries);
            case "snorlax": var snorlax = PretSnorlaxArt.Load(root); return (snorlax.DexEntries, snorlax.CaptureDexEntries);
            case "silph": var silph = PretSilphArt.Load(root); return (silph.DexEntries, silph.CaptureDexEntries);
            case "saffron-gym": var saffron = PretSaffronGymArt.Load(root); return (saffron.DexEntries, saffron.CaptureDexEntries);
            case "cinnabar": var cinnabar = PretCinnabarArt.Load(root); return (cinnabar.DexEntries, cinnabar.CaptureDexEntries);
            case "surf": var surf = PretSurfArt.Load(root); return (surf.DexEntries, surf.CaptureDexEntries);
            case "safari": var safari = PretSafariArt.Load(root); return (safari.DexEntries, safari.CaptureDexEntries);
            case "fuchsia-route": var fuchsia = PretFuchsiaRouteArt.Load(root); return (fuchsia.DexEntries, fuchsia.CaptureDexEntries);
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }
}
