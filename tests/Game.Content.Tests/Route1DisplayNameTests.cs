using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class Route1DisplayNameTests
{
    private const string RoutePath = "content/generated/data/pokemon/route1/catalog.json";
    private const string TutorialPath = "content/generated/data/services/old-man-tutorial.json";
    private static PretCanonicalPokemonCatalog Canonical => TestAssets.Cached(() =>
        PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));

    [TestMethod]
    [DataRow("Mixed case")]
    [DataRow("改名された表示")]
    [DataRow("A shared display label longer than twelve characters")]
    public void OldManCompositionAcceptsEditedRouteSpeciesAndMoveNames(string name)
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, [RoutePath, TutorialPath]);
        var original = Load(output.Path);
        TestAssets.EditJson(output.Path, RoutePath, document =>
        {
            Rename(document["speciesNames"]!.AsArray(), name);
            Rename(document["moveNames"]!.AsArray(), name);
        });
        // Overlapping moves still have one consistent display label in both input packages.
        TestAssets.EditJson(output.Path, TutorialPath, document => Rename(document["moveNames"]!.AsArray(), name));

        var edited = Load(output.Path);
        var route = PretRoute1PokemonCatalog.LoadContent(Canonical, File.ReadAllText(Path.Combine(output.Path, RoutePath)));
        Assert.AreEqual(17, edited.Catalog.Species.Count);
        foreach (var (id, species) in route.Catalog.Species)
        {
            Assert.AreEqual(name, edited.SpeciesNames[species.SourceSpeciesId]);
            Assert.AreEqual(original.Catalog.Species[id].SourceSpeciesId, edited.Catalog.Species[id].SourceSpeciesId);
            Assert.AreSame(original.Catalog.Species[id].BaseStats, edited.Catalog.Species[id].BaseStats);
            CollectionAssert.AreEqual(original.Catalog.Species[id].Evolutions.ToArray(), edited.Catalog.Species[id].Evolutions.ToArray());
        }
        foreach (var (id, move) in edited.Catalog.Moves)
        {
            Assert.AreEqual(name, edited.MoveNames[id]);
            Assert.AreSame(original.Catalog.Moves[id], move);
        }
        Assert.AreEqual(original.SpeciesNames[13], edited.SpeciesNames[13]);
        Assert.AreEqual(original.TeachyTv, edited.TeachyTv);
        CollectionAssert.AreEqual(original.OldManThrowFrames.ToArray(), edited.OldManThrowFrames.ToArray());
    }

    [TestMethod]
    [DataRow("speciesNames", "empty")]
    [DataRow("speciesNames", "blank")]
    [DataRow("speciesNames", "identity")]
    [DataRow("speciesNames", "duplicate")]
    [DataRow("speciesNames", "missing")]
    [DataRow("speciesNames", "symbol")]
    [DataRow("moveNames", "empty")]
    [DataRow("moveNames", "blank")]
    [DataRow("moveNames", "identity")]
    [DataRow("moveNames", "duplicate")]
    [DataRow("moveNames", "missing")]
    public void OldManCompositionRetainsNameCoverageAndStableReferences(string collection, string change)
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, [RoutePath, TutorialPath]);
        TestAssets.EditJson(output.Path, RoutePath, document =>
        {
            var rows = document[collection]!.AsArray();
            switch (change)
            {
                case "empty": rows[0]!["name"] = ""; break;
                case "blank": rows[0]!["name"] = " \t\n"; break;
                case "identity": rows[0]!["id"]!["value"] = collection == "speciesNames" ? "pret:SPECIES_UNKNOWN" : "pret:MOVE_UNKNOWN"; break;
                case "duplicate": rows.Add(rows[0]!.DeepClone()); break;
                case "missing": rows.RemoveAt(0); break;
                case "symbol": rows[0]!["source"]!["symbol"] = "SPECIES_CHARMANDER"; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        });
        var error = Assert.ThrowsExactly<ContentValidationException>(() => Load(output.Path));
        string code = change == "missing" ? "names_missing" : change == "symbol" ? "species_name_symbol"
            : collection == "speciesNames" ? "species_name" : "move_name";
        Assert.AreEqual("route1_pokemon_" + code, error.Diagnostics.Single().Code);
    }

    [TestMethod]
    public void OldManCompositionStillRejectsConflictingSharedMoveLabels()
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, [RoutePath, TutorialPath]);
        TestAssets.EditJson(output.Path, RoutePath, document =>
            document["moveNames"]!.AsArray().Single(row => row!["id"]!["value"]!.GetValue<string>() == "pret:MOVE_AGILITY")!["name"] = "Different display label");
        var error = Assert.ThrowsExactly<ContentValidationException>(() => Load(output.Path));
        Assert.AreEqual("old_man_tutorial_name_collision", error.Diagnostics.Single().Code);
    }

    private static PretOldManTutorialContent Load(string root) => PretOldManTutorial.LoadContent(Canonical,
        File.ReadAllText(Path.Combine(root, RoutePath)), File.ReadAllText(Path.Combine(root, TutorialPath)));

    private static void Rename(JsonArray rows, string name)
    {
        foreach (var row in rows) row!["name"] = name;
    }
}
