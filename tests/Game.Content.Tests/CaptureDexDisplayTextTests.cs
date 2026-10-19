using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class CaptureDexDisplayTextTests
{
    private const string PokedexPath = "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json";
    private const string LegacyPath = "content/generated/sprites/pokemon/capture-dex/capture-dex.json";
    private const string ForestPath = "content/generated/sprites/pokemon/forest-capture-dex/capture-dex.json";
    private static readonly int[] Species = [10, 11, 13, 14, 16, 19, 25];
    private static readonly string[] DisplayFields =
        ["name", "category", "displayCategory", "description", "heightDisplay", "weightDisplay", "habitatTitle"];
    private static PretCanonicalPokemonCatalog Pokemon => TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
    private static CaptureDexDocument Original => TestAssets.Cached(() => PretForestCaptureDex.Load(TestAssets.Root));

    public static IEnumerable<object[]> DisplayTextCases => Species.SelectMany(species =>
        new[] { "name", "category", "displayCategory", "description" }.Select(textField => new object[] { species, textField }));

    [TestMethod, DynamicData(nameof(DisplayTextCases))]
    public void ComposedCaptureUsesCanonicalTextWithoutChangingArtOrMeasures(int species, string field)
    {
        using var output = CopyPackage();
        TestAssets.EditJson(output.Path, PokedexPath, document => CanonicalEntry(document, species)[field] =
            "A replaceable display 文本 longer than a nickname");
        var dex = PretPokedexMenu.Load(output.Path);
        var forest = PretForestPokemon.Load(Pokemon, output.Path);
        var actual = PretForestCaptureDex.Load(output.Path, dex, forest);

        foreach (int id in Species) AssertProjection(Original.Entries[id], dex.Entries[id], actual.Entries[id]);
        Assert.AreEqual(JsonSerializer.Serialize(Original.Layout), JsonSerializer.Serialize(actual.Layout));
        CollectionAssert.AreEqual(Original.Assets.ToArray(), actual.Assets.ToArray());
    }

    [TestMethod]
    [DataRow("legacy", 16)]
    [DataRow("extension", 25)]
    [DataRow("composed", 19)]
    public void StandaloneLoadersUseAllCanonicalDisplayFields(string loader, int species)
    {
        using var output = CopyPackage();
        TestAssets.EditJson(output.Path, PokedexPath, document =>
        {
            var entry = CanonicalEntry(document, species);
            foreach (string field in DisplayFields) entry[field] = "Canonical " + field;
        });
        // Duplicate artifact strings cannot become another display authority.
        TestAssets.EditJson(output.Path, CapturePath(species), document =>
        {
            var entry = document["entries"]![species.ToString()]!;
            foreach (string field in DisplayFields) entry[field] = "Stale local " + field;
        });
        var dex = PretPokedexMenu.Load(output.Path);
        var actual = loader switch
        {
            "legacy" => PretCaptureDex.Load(File.ReadAllText(Path.Combine(output.Path, LegacyPath)), dex),
            "extension" => PretForestCaptureDex.LoadExtension(output.Path),
            "composed" => PretForestCaptureDex.Load(output.Path),
            _ => throw new ArgumentOutOfRangeException(nameof(loader))
        };
        AssertProjection(Original.Entries[species], dex.Entries[species], actual.Entries[species]);
        Assert.AreEqual(loader == "legacy" ? 2 : loader == "extension" ? 5 : 7, actual.Entries.Count);
    }

    public static IEnumerable<object[]> InvalidEntryCases => new[] { 16, 25 }.SelectMany(species =>
        new[] { "sourceSpeciesId", "nationalNumber", "heightDecimeters", "weightHectograms", "frontSpritePath", "footprintPath" }
            .Select(textField => new object[] { species, textField }));

    [TestMethod, DynamicData(nameof(InvalidEntryCases))]
    public void CaptureStillRejectsDamagedIdentityMeasuresAndArt(int species, string field)
    {
        using var output = CopyPackage();
        TestAssets.EditJson(output.Path, CapturePath(species), document =>
        {
            var entry = document["entries"]![species.ToString()]!;
            entry[field] = field is "frontSpritePath" or "footprintPath"
                ? document["entries"]![(species == 16 ? 19 : 10).ToString()]![field]!.DeepClone()
                : JsonValue.Create(entry[field]!.GetValue<int>() + 1);
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretForestCaptureDex.Load(output.Path));
        Assert.AreEqual(field switch
        {
            "sourceSpeciesId" or "nationalNumber" => "Capture dex species identity differs from the shared Pokédex.",
            "heightDecimeters" or "weightHectograms" => "Capture dex measures differ from the shared Pokédex.",
            _ => "Capture dex art references differ from the shared Pokédex."
        }, error.Message);
    }

    [TestMethod]
    [DataRow("width")]
    [DataRow("x")]
    public void ForestFootprintGeometryStillRejects(string field)
    {
        using var output = CopyPackage();
        TestAssets.EditJson(output.Path, ForestPath, document =>
        {
            var asset = document["assets"]![0]!;
            asset[field] = asset[field]!.GetValue<int>() + 1;
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretForestCaptureDex.Load(output.Path));
        Assert.AreEqual("Forest capture dex footprint identity/pixels differ.", error.Message);
    }

    [TestMethod]
    public void ExtensionStillRequiresLegacyLayout()
    {
        using var output = CopyPackage();
        TestAssets.EditJson(output.Path, ForestPath, document => document["layout"]!["nameX"] = 45);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretForestCaptureDex.Load(output.Path));
        Assert.AreEqual("Forest capture dex changed the legacy layout.", error.Message);
    }

    [TestMethod]
    [DataRow("pidgey-footprint")]
    [DataRow("pikachu-footprint")]
    public void TextureReaderStillRejectsForgedMetadata(string assetId)
    {
        var asset = Original.Assets.Single(asset => asset.Id == assetId);
        Assert.IsTrue(PretForestCaptureDex.ReadTexture(TestAssets.Root, asset).Length > 0);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PretForestCaptureDex.ReadTexture(TestAssets.Root, asset with { Width = asset.Width + 1 }));
    }

    [TestMethod]
    [DataRow("dex")]
    [DataRow("forest")]
    public void InjectedDependenciesMustBelongToTheSameRoot(string dependency)
    {
        using var output = CopyPackage();
        var dex = PretPokedexMenu.Load(dependency == "dex" ? TestAssets.Root : output.Path);
        var forest = PretForestPokemon.Load(Pokemon, dependency == "forest" ? TestAssets.Root : output.Path);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretForestCaptureDex.Load(output.Path, dex, forest));
        Assert.AreEqual(dependency == "dex" ? "Pokédex catalog belongs to a different project root." :
            "Forest Pokémon art belongs to a different project root.", error.Message);
    }

    private static void AssertProjection(CaptureDexEntry original, PokedexMenuEntry canonical, CaptureDexEntry actual)
        => Assert.AreEqual(original with
        {
            Name = canonical.Name,
            Category = canonical.Category,
            DisplayCategory = canonical.DisplayCategory,
            Description = canonical.Description,
            HeightDisplay = canonical.HeightDisplay,
            WeightDisplay = canonical.WeightDisplay,
            HabitatTitle = canonical.HabitatTitle
        }, actual);

    private static JsonNode CanonicalEntry(JsonNode document, int species) => document["entries"]!.AsArray()
        .Single(entry => entry!["sourceSpeciesId"]!.GetValue<int>() == species)!;

    private static string CapturePath(int species) => species is 16 or 19 ? LegacyPath : ForestPath;

    private static TestAssets.TemporaryDirectory CopyPackage()
    {
        var output = TestAssets.TemporaryOutput();
        var files = new[] { "capture-dex", "forest-capture-dex", "forest" }.SelectMany(package =>
            Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated/sprites/pokemon", package),
                "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(TestAssets.Root, path)));
        TestAssets.CopyFiles(output.Path, files.Concat([PokedexPath, PretCanonicalPokemonCatalog.CatalogRelativePath,
            PretMetronomeMoveCatalog.CatalogRelativePath]));
        return output;
    }
}
