using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class PresentationPackageTests
{
    private string _root = null!;
    private const string DirectoryPath = "content/generated/presentation/";

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "firered-presentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, DirectoryPath));
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(_root, recursive: true);

    private void WriteManifest(int schema = 1, int width = 64, string path = "sprite.png")
    {
        File.WriteAllBytes(Path.Combine(_root, DirectoryPath, "sprite.png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(_root, DirectoryPath, "manifest.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = schema,
            outputs = new[] { new { path } },
            assets = new[] { new { texturePath = DirectoryPath + "sprite.png", width, height = 64 } }
        }));
    }

    [TestMethod]
    public void SharedManifestCoalescesOutputAndAssetWithoutDecoding()
    {
        WriteManifest();
        var package = PresentationPackage.Load(_root, DirectoryPath, false, "assets");
        Assert.AreEqual(1, package.Files.Count);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, package.Files[DirectoryPath + "sprite.png"]);
        Assert.AreEqual(64, package.Manifest.GetProperty("assets")[0].GetProperty("width").GetInt32());
        // Native upload owns the decoder and malformed PNG rejection. The BCL loader never decodes.
    }

    [TestMethod]
    public void LegacyOutputKeysRetainRelativeNames()
    {
        WriteManifest();
        var package = PresentationPackage.Load(_root, DirectoryPath, relativeOutputKeys: true);
        Assert.IsTrue(package.Files.ContainsKey("sprite.png"));
        Assert.IsFalse(package.Files.ContainsKey(DirectoryPath + "sprite.png"));
    }

    [TestMethod]
    public void UnsupportedSchemaAndInvalidDimensionsReject()
    {
        WriteManifest(schema: 2);
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationPackage.Load(_root, DirectoryPath));
        WriteManifest(width: 0);
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationPackage.Load(_root, DirectoryPath, false, "assets"));
    }

    [TestMethod]
    public void TraversalAndMissingDependenciesReject()
    {
        WriteManifest(path: "../sprite.png");
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationPackage.Load(_root, DirectoryPath));
        WriteManifest(path: "missing.png");
        Assert.ThrowsExactly<FileNotFoundException>(() => PresentationPackage.Load(_root, DirectoryPath));
    }

    private static PokedexMenuEntry Entry(string path) => new(25, 25, "PIKACHU", "MOUSE", "MOUSE", 4, 60,
        "1'04\"", "13.2 lbs.", "A mouse.", "ELECTRIC", "ELECTRIC", "FOREST",
        new(path, 64, 64), new("footprint.png", 16, 16));
    private static CaptureDexEntry Capture(PokedexMenuEntry e) => new(e.SourceSpeciesId, e.NationalNumber,
        e.Name, e.Category, e.DisplayCategory, e.HeightDecimeters, e.WeightHectograms, e.HeightDisplay,
        e.WeightDisplay, e.Description, e.Footprint!.TexturePath, e.Portrait!.TexturePath, e.HabitatTitle);

    [TestMethod]
    public void DexIndexUsesExactTextureMetadataAndDefersReading()
    {
        var index = new PresentationDexIndex(); var entry = Entry("portrait.png"); var reads = 0;
        index.Add(entry, Capture(entry), () => { reads++; return [7]; }, () => [9]);
        Assert.AreEqual(0, reads);
        Assert.AreSame(entry, index.Entry(25));
        Assert.IsNull(index.ReadTexture(entry.Portrait! with { Width = 63 }));
        CollectionAssert.AreEqual(new byte[] { 7 }, index.ReadTexture(entry.Portrait!)!);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void LaterDexBindingsWinAndClearReleasesReaders()
    {
        var index = new PresentationDexIndex(); var first = Entry("portrait.png");
        var replacement = first with { Name = "LATEST" };
        index.Add(first, Capture(first), () => [1], () => [2]);
        index.Add(replacement, Capture(replacement), () => [3], () => [4]);
        Assert.AreEqual("LATEST", index.Entry(25)!.Name);
        Assert.AreEqual("LATEST", index.CaptureEntry(25)!.Name);
        CollectionAssert.AreEqual(new byte[] { 3 }, index.ReadTexture(first.Portrait!)!);
        index.Clear();
        Assert.IsNull(index.Entry(25)); Assert.IsNull(index.CaptureEntry(25));
        Assert.IsNull(index.ReadTexture(first.Portrait!));
    }
    [TestMethod]
    public void RealPackageDexIndexMatchesThePreviousReversePriorityChain()
    {
        string[] names = ["route22", "route3", "mtmoon", "cerulean", "bill", "vermilion", "ssanne",
            "route11", "rocktunnel", "celadon-approach", "celadon-gym", "rocket-hideout"];
        var canonical = TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
        var dex = TestAssets.Cached(() => PretPokedexMenu.Load(TestAssets.Root));
        var packages = names.Select(name => PokemonPresentationPackage.Load(canonical, TestAssets.Root, name, dex)).ToArray();
        var index = new PresentationDexIndex();
        foreach (var package in packages) index.AddPackage(package);
        var sourceIds = packages.SelectMany(p => p.DexEntries.Keys).Distinct().ToArray();
        Assert.IsTrue(sourceIds.Length > 0);
        foreach (int species in sourceIds)
        {
            var winner = packages.Reverse().First(p => p.DexEntries.ContainsKey(species));
            var entry = winner.DexEntries[species];
            Assert.AreEqual(entry, index.Entry(species));
            Assert.AreEqual(winner.CaptureDexEntries[species], index.CaptureEntry(species));
            foreach (var texture in new[] { entry.Portrait!, entry.Footprint! })
                CollectionAssert.AreEqual(winner.ReadTexture(texture.TexturePath), index.ReadTexture(texture)!);
        }
        // Registering the same actual package twice also reuses exact metadata/path bindings.
        index.AddPackage(packages[^1]);
        foreach (var entry in packages[^1].DexEntries.Values)
            CollectionAssert.AreEqual(packages[^1].ReadTexture(entry.Portrait!.TexturePath), index.ReadTexture(entry.Portrait)!);
    }

    [TestMethod]
    public void EveryMaintainedPackageKeepsItsRealManifestAndFileKeyConvention()
    {
        string root = TestAssets.Root;
        Dictionary<string, byte[]>[] files =
        [
            CeladonApproachPresentationAssets.Load(root), CeladonGymPresentationAssets.Load(root),
            RockTunnelPresentationAssets.Load(root), CelioPresentationAssets.Load(root).Files,
            EventIslandPresentationAssets.Load(root).Files, FightingDojoPresentationAssets.Load(root).Files,
            FiveIslandPresentationAssets.Load(root).Files, FourIslandPresentationAssets.Load(root).Files,
            FuchsiaGymPresentationAssets.Load(root).Files, LeagueRematchPresentationAssets.Load(root).Files,
            MewtwoPresentationAssets.Load(root).Files, PokemonLeaguePresentationAssets.Load(root).Files,
            PokemonTowerPresentationAssets.Load(root).Files, PokemonTowerPresentationAssets.Load(root, art: true).Files,
            PowerPlantPresentationAssets.Load(root).Files, RocketHideoutPresentationAssets.Load(root).Files,
            RocketHideoutPresentationAssets.Load(root, portraits: true).Files,
            SevenIslandPresentationAssets.Load(root).Files, SeviiRubyPresentationAssets.Load(root).Files,
            SixIslandPresentationAssets.Load(root).Files, VictoryRoadPresentationAssets.Load(root).Files,
            ViridianGymPresentationAssets.Load(root).Files
        ];
        foreach (var package in files)
        {
            Assert.IsTrue(package.Count > 0);
            Assert.IsTrue(package.Values.All(bytes => bytes.Length > 0));
        }
        Assert.IsTrue(RocketHideoutPresentationAssets.LoadTerrain(root).Length > 0);
    }

}
