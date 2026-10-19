using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PresentationSpeciesIdentityTests
{
    private const string PokedexPath = "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json";
    private static PokemonCatalog SpeciesCatalog => TestAssets.Cached(() =>
        PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root)).Catalog;

    [TestMethod]
    [DataRow("stone-evolution")]
    [DataRow("ordinary-evolution")]
    [DataRow("breeding")]
    [DataRow("gift-egg")]
    [DataRow("game-corner")]
    public void EditedDisplayNamesDoNotDetermineSpeciesIdentity(string package)
    {
        using var output = TestAssets.TemporaryOutput();
        var original = Prepare(output.Path, package);
        int species = original.DexEntries.Keys.First();
        string name = "Edited species " + species;
        EditName(output.Path, package, species, name);

        var edited = Load(output.Path, package);
        Assert.AreEqual(original.DexEntries[species] with { Name = name }, edited.DexEntries[species]);
        foreach (var (id, entry) in original.DexEntries.Where(pair => pair.Key != species))
            Assert.AreEqual(entry, edited.DexEntries[id]);
        foreach (var kind in AssetKinds(package))
        foreach (var asset in edited.Manifest.GetProperty(kind).EnumerateArray())
        {
            var definition = SpeciesCatalog.Species[new(asset.GetProperty("speciesId").GetString()!)];
            Assert.AreEqual(definition.SourceSpeciesId, asset.GetProperty("sourceSpeciesId").GetInt32());
        }
    }

    [TestMethod]
    [DataRow("stone-evolution")]
    [DataRow("ordinary-evolution")]
    [DataRow("breeding")]
    [DataRow("gift-egg")]
    [DataRow("game-corner")]
    public void DisplayNamesMustStillBePresent(string package)
    {
        foreach (string name in new[] { "", " \t\n" })
        {
            using var output = TestAssets.TemporaryOutput();
            var original = Prepare(output.Path, package);
            EditName(output.Path, package, original.DexEntries.Keys.First(), name);
            var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, package));
            StringAssert.Contains(error.Message, "display name is missing.");
        }
    }

    [TestMethod]
    [DataRow("stone-evolution")]
    [DataRow("ordinary-evolution")]
    [DataRow("breeding")]
    [DataRow("gift-egg")]
    [DataRow("game-corner")]
    public void EveryAssetKindStillRequiresTheMatchingCanonicalSpecies(string package)
    {
        foreach (string kind in AssetKinds(package))
        {
            using var output = TestAssets.TemporaryOutput();
            var original = Prepare(output.Path, package);
            int species = original.DexEntries.Keys.First();
            EditName(output.Path, package, species, "Edited species " + species);
            Edit(output.Path, Prefix(package) + "manifest.json", document =>
            {
                var asset = document[kind]!.AsArray()[0]!;
                int sourceId = asset["sourceSpeciesId"]!.GetValue<int>();
                asset["speciesId"] = SpeciesCatalog.Species.Values
                    .First(definition => definition.SourceSpeciesId != sourceId).Id.Value;
            });
            var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, package));
            StringAssert.Contains(error.Message, "asset species differs.");
        }
    }

    [TestMethod]
    [DataRow("stone-evolution")]
    [DataRow("ordinary-evolution")]
    [DataRow("breeding")]
    [DataRow("gift-egg")]
    [DataRow("game-corner")]
    public void EditedNamesDoNotRelaxNumericMembershipOrGeometry(string package)
    {
        foreach (string field in new[] { "sourceSpeciesId", "x" })
        {
            using var output = TestAssets.TemporaryOutput();
            var original = Prepare(output.Path, package);
            int species = original.DexEntries.Keys.First();
            EditName(output.Path, package, species, "Edited species " + species);
            Edit(output.Path, Prefix(package) + "manifest.json", document =>
                document[AssetKinds(package)[0]]!.AsArray()[0]![field] = 9999);
            Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, package));
        }
    }

    [TestMethod]
    [DataRow("stone-evolution")]
    [DataRow("ordinary-evolution")]
    public void EvolutionPacksStillAgreeWithTheirInheritedDex(string package)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path, package);
        Edit(output.Path, Prefix(package) + "dex-entries.json", document =>
            document["entries"]!.AsArray()[0]!["name"] = "Unmatched display name");
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Load(output.Path, package));
        StringAssert.Contains(error.Message, "source dex metadata differs.");
    }

    private sealed record Snapshot(JsonElement Manifest, Dictionary<string, byte[]> Files,
        Dictionary<int, PokedexMenuEntry> DexEntries);

    private static Snapshot Load(string root, string package)
    {
        switch (package)
        {
            case "stone-evolution":
                var stone = PretStoneEvolutionPresentation.Load(root, SpeciesCatalog);
                return new(stone.Manifest, stone.Files, stone.DexEntries);
            case "ordinary-evolution":
                var ordinary = PretOrdinaryEvolutionPresentation.Load(root, SpeciesCatalog);
                return new(ordinary.Manifest, ordinary.Files, ordinary.DexEntries);
            case "breeding":
                var breeding = PretBreedingPresentation.Load(root, SpeciesCatalog);
                return new(breeding.Manifest, breeding.Files, breeding.DexEntries);
            case "gift-egg":
                var gift = PretGiftEggPresentation.Load(root, SpeciesCatalog);
                return new(gift.Manifest, gift.Files, gift.DexEntries);
            case "game-corner":
                var prizes = PretGameCornerPresentation.Load(root, SpeciesCatalog);
                return new(prizes.Manifest, prizes.Files, prizes.DexEntries);
            default: throw new ArgumentOutOfRangeException(nameof(package));
        }
    }

    private static Snapshot Prepare(string root, string package)
    {
        var original = TestAssets.Cached(() => Load(TestAssets.Root, package), "presentation-species-identity:" + package);
        foreach (var (relative, bytes) in original.Files) Write(relative, bytes);
        Copy(Prefix(package) + "manifest.json");
        if (package is "stone-evolution" or "ordinary-evolution") Copy(PokedexPath);
        if (original.Manifest.GetProperty("moveAnimationGeometry").TryGetProperty("inheritedManifest", out var inherited))
            Copy(inherited.GetString()!);
        return original;

        void Copy(string relative) => Write(relative, File.ReadAllBytes(Path.Combine(TestAssets.Root, relative)));
        void Write(string relative, byte[] bytes)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }

    private static void EditName(string root, string package, int species, string name)
    {
        void Rename(JsonNode document) => document["entries"]!.AsArray()
            .Single(entry => entry!["sourceSpeciesId"]!.GetValue<int>() == species)!["name"] = name;
        Edit(root, Prefix(package) + "dex-entries.json", Rename);
        // The two evolution packs must continue agreeing with their inherited display authority.
        if (package is "stone-evolution" or "ordinary-evolution") Edit(root, PokedexPath, Rename);
    }

    private static void Edit(string root, string relative, Action<JsonNode> change)
    {
        string path = Path.Combine(root, relative);
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        change(document);
        File.WriteAllText(path, document.ToJsonString());
    }

    private static string Prefix(string package) => "content/generated/sprites/pokemon/" + package + "/";
    private static string[] AssetKinds(string package) =>
        [package is "stone-evolution" or "ordinary-evolution" ? "inheritedSprites" : "sprites", "pokemonIcons", "footprints"];
}
