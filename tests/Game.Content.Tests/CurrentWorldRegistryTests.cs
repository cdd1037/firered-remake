using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CurrentWorldRegistryTests
{
    private static PretCurrentWorld World => TestAssets.Cached(() => PretCurrentWorld.Load(TestAssets.Root));

    [TestMethod]
    public void EveryDeclaredSymbolResolvesToItsRegistryIdentity()
    {
        var registry = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, PretCurrentWorld.IndexPath)))!["mapSymbols"]!.AsObject();
        Assert.IsTrue(registry.Count > 0, "The registry must not be empty.");
        foreach (var (symbol, id) in registry)
            Assert.AreEqual(new MapId(id!.GetValue<string>()), World.Resolve(symbol), symbol);
    }

    [TestMethod]
    [DataRow("MAP_UNKNOWN_WORLD")]
    [DataRow("MAP_SILPH_CO_99F")]
    [DataRow("MAP_SAFFRON_CITY_INVENTED_HOUSE")]
    public void UnknownSymbolsCannotBeInferredFromTheirNames(string symbol)
        => Assert.ThrowsExactly<InvalidDataException>(() => World.Resolve(symbol));

    [TestMethod]
    [DataRow("MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F", "map.viridian-city-pokemon-center-2f")]
    [DataRow("MAP_PEWTER_CITY_POKEMON_CENTER_2F", "map.pewter-city-pokemon-center-2f")]
    [DataRow("MAP_VERMILION_CITY_POKEMON_CENTER_2F", "map.vermilion-city-pokemon-center-2f")]
    [DataRow("MAP_SAFFRON_CITY_POKEMON_CENTER_2F", "map.saffron-city-pokemon-center-2f")]
    public void KnownUnselectedCentersResolveWithoutBecomingPlayable(string symbol, string stableId)
    {
        var id = World.Resolve(symbol);
        Assert.AreEqual(new MapId(stableId), id);
        Assert.IsFalse(World.Maps.ContainsKey(id));
        Assert.IsFalse(World.RuntimeMaps.Maps.ContainsKey(id));
    }

    [TestMethod]
    [DataRow("missing-registry")]
    [DataRow("empty-registry")]
    [DataRow("index-symbol-mismatch")]
    [DataRow("duplicate-ordinary-id")]
    [DataRow("duplicate-path")]
    [DataRow("duplicate-variant")]
    public void InvalidRegistryAndSelectionFailAtWorldAdmission(string mutation)
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, CurrentWorldTestFixture.RelativeFiles().Select(p => "content/generated/" + p));
        TestAssets.EditJson(output.Path, PretCurrentWorld.IndexPath, document =>
        {
            var maps = document["maps"]!.AsArray();
            var ordinary = maps.First(m => m!["variant"] is null)!;
            var variant = maps.First(m => m!["variant"] is not null)!;
            switch (mutation)
            {
                case "missing-registry": document.AsObject().Remove("mapSymbols"); break;
                case "empty-registry": document["mapSymbols"] = new JsonObject(); break;
                case "index-symbol-mismatch": document["mapSymbols"]![ordinary["sourceMapSymbol"]!.GetValue<string>()] = "map.mismatched-registry"; break;
                case "duplicate-ordinary-id": maps.Add(ordinary.DeepClone()); break;
                case "duplicate-path":
                    variant["path"] = maps.Single(m => m!["variant"] is null &&
                        m["stableId"]!.GetValue<string>() == variant["stableId"]!.GetValue<string>())!["path"]!.GetValue<string>();
                    break;
                case "duplicate-variant": maps.Add(variant.DeepClone()); break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => PretCurrentWorld.Load(output.Path));
    }

    [TestMethod]
    public void LayoutVariantsKeepMapAndEventIdentityButNotGeometry()
    {
        var variants = World.RuntimeMaps.Entries.Where(e => e.Variant is not null).ToArray();
        Assert.IsTrue(variants.Length > 0, "No layout variants were checked.");
        foreach (var entry in variants)
        {
            var variant = World.GetMapByPath(entry.Path);
            var ordinary = World.Maps[new MapId(entry.StableId)];
            Assert.AreEqual(ordinary.Id, variant.Id, entry.Path);
            Assert.AreEqual(ordinary.SourceMapSymbol, variant.SourceMapSymbol, entry.Path);
            Assert.AreEqual(ordinary.SourceMapName, variant.SourceMapName, entry.Path);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(ordinary.Events.GetRawText()), JsonNode.Parse(variant.Events.GetRawText())), entry.Path);
            Assert.IsFalse(ordinary.Cells.SequenceEqual(variant.Cells), entry.Path);
            Assert.AreNotSame(ordinary, variant, entry.Path);
        }
    }

    [TestMethod]
    public void IndependentGeneratedWorldsKeepTheirResolversAndRegionalMapInstances()
    {
        using var firstRoot = TestAssets.TemporaryOutput();
        using var secondRoot = TestAssets.TemporaryOutput();
        const string symbol = "MAP_SAFFRON_CITY_POKEMON_CENTER_2F";
        var firstId = new MapId("map.first-world-unselected-center");
        var secondId = new MapId("map.second-world-unselected-center");
        Prepare(firstRoot.Path, firstId);
        Prepare(secondRoot.Path, secondId);
        var first = PretCurrentWorld.Load(firstRoot.Path);
        var second = PretCurrentWorld.Load(secondRoot.Path);
        CheckResolvers();

        // Interleave worlds and reverse feature order to expose static resolver/map caches.
        CheckMaps(first, second, PretCeladonGymRegion.Load(first).NewMaps);
        CheckMaps(second, first, PretSafariRegion.Load(second).NewMaps);
        CheckMaps(first, second, PretVermilionGymRegion.Load(first).NewMaps);
        CheckMaps(second, first, PretVermilionGymRegion.Load(second).NewMaps);
        CheckMaps(first, second, PretSafariRegion.Load(first).NewMaps);
        CheckMaps(second, first, PretCeladonGymRegion.Load(second).NewMaps);
        CheckResolvers();

        static void Prepare(string root, MapId id)
        {
            var generated = Path.Combine(TestAssets.Root, "content/generated");
            TestAssets.CopyFiles(root, Directory.EnumerateFiles(generated, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(TestAssets.Root, path)));
            TestAssets.EditJson(root, PretCurrentWorld.IndexPath, document =>
                document["mapSymbols"]![symbol] = id.Value);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
        }

        void CheckResolvers()
        {
            Assert.AreEqual(firstId, first.Resolve(symbol));
            Assert.AreEqual(secondId, second.Resolve(symbol));
            var downstairs = new MapId("map.saffron-city-pokemon-center-1f");
            Assert.AreEqual(firstId, first.Maps[downstairs].FieldWarps[3].DestinationMapId);
            Assert.AreEqual(secondId, second.Maps[downstairs].FieldWarps[3].DestinationMapId);
            foreach (var world in new[] { first, second })
                foreach (var id in new[] { firstId, secondId })
                {
                    Assert.IsFalse(world.Maps.ContainsKey(id));
                    Assert.IsFalse(world.RuntimeMaps.Maps.ContainsKey(id));
                }
        }

        static void CheckMaps(PretCurrentWorld owner, PretCurrentWorld other,
            IReadOnlyDictionary<MapId, PretWorldMap> maps)
        {
            Assert.IsTrue(maps.Count > 0, "A real feature region must expose maps.");
            foreach (var (id, map) in maps)
            {
                Assert.AreSame(owner.Maps[id], map, id.Value);
                Assert.AreNotSame(other.Maps[id], map, id.Value);
            }
        }
    }
}
