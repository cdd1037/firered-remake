using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedBreedingMetadataTests
{
    private const string BreedingPath = "content/generated/data/pokemon/game/breeding.json";
    private sealed record Inputs(PretCanonicalPokemonCatalog Canonical, SourceMechanicsCatalog Mechanics,
        NpcTradePokemonCatalog Trades, SharedMachineCatalog Machines);
    private static readonly Lazy<Inputs> Source = new(() =>
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, TestAssets.Root);
        return new(canonical, mechanics, PretNpcTrades.Load(TestAssets.Root, mechanics.Catalog).Trades,
            SharedMachineCatalog.LoadProject(TestAssets.Root));
    });

    [TestMethod]
    public void CurrentBreedingRowsCoverOwnedSpeciesAndPreserveCanonicalMetadata()
    {
        var game = Load(TestAssets.Root);
        var breeding = game.Catalog.BreedingData!;
        var document = Parse(File.ReadAllText(Path.Combine(TestAssets.Root, BreedingPath)));
        Assert.IsTrue(breeding.Species.Keys.ToHashSet().SetEquals(game.Catalog.Species.Keys));
        Assert.IsTrue(breeding.MachineCompatibility.Keys.ToHashSet().SetEquals(game.Catalog.Species.Keys));
        foreach (var row in document.Species)
        {
            var actual = breeding.Species[row.Species];
            var metadata = Source.Value.Canonical.Metadata[row.Species];
            Assert.AreEqual(metadata.EggCycles, (int)actual.Breeding.EggCycles, row.Species.Value);
            Assert.AreEqual(row.BaseEggSpecies, actual.BaseEggSpecies, row.Species.Value);
            Assert.IsTrue(actual.EggMoves.SequenceEqual(row.EggMoves), row.Species.Value);
            Assert.IsTrue(game.Catalog.Species.ContainsKey(actual.BaseEggSpecies));
            Assert.IsTrue(actual.EggMoves.All(game.Catalog.Moves.ContainsKey));
        }
        Assert.AreEqual(new SourceBreedingSpecies(new("pret:SPECIES_BULBASAUR"), 1, 7, 20),
            breeding.Species[new("pret:SPECIES_BULBASAUR")].Breeding);
        Assert.AreEqual(new SourceBreedingSpecies(new("pret:SPECIES_PIKACHU"), 5, 6, 10),
            breeding.Species[new("pret:SPECIES_PIKACHU")].Breeding);
        Assert.AreEqual(15, breeding.Species[new("pret:SPECIES_PICHU")].Breeding.EggGroup1);
        Assert.AreEqual(13, breeding.Species[new("pret:SPECIES_DITTO")].Breeding.EggGroup1);
        foreach (var (species, baseSpecies) in new[] {
            ("PIKACHU", "PICHU"), ("CROBAT", "ZUBAT"), ("BLISSEY", "CHANSEY"),
            ("RHYDON", "RHYHORN"), ("TOGETIC", "TOGEPI"), ("MURKROW", "MURKROW") })
            Assert.AreEqual(new SpeciesId("pret:SPECIES_" + baseSpecies),
                breeding.Species[new("pret:SPECIES_" + species)].BaseEggSpecies);
        Assert.IsTrue(breeding.Species[new("pret:SPECIES_MURKROW")].EggMoves.Select(move => move.Value)
            .SequenceEqual(new[] { "WHIRLWIND", "DRILL_PECK", "MIRROR_MOVE", "WING_ATTACK", "SKY_ATTACK",
                "CONFUSE_RAY", "FEATHER_DANCE", "PERISH_SONG" }.Select(move => "pret:MOVE_" + move)));
    }

    [TestMethod]
    public void BreedingInheritanceUsesAllOriginalMachineSlotsIncludingUnacquiredDive()
    {
        var game = Load(TestAssets.Root);
        var breeding = game.Catalog.BreedingData!;
        Assert.AreEqual(58, breeding.AllMachineMoves.Length);
        Assert.AreEqual(58, breeding.AllMachineMoves.Distinct().Count());
        for (var slot = 0; slot < 57; slot++)
            Assert.AreEqual(Source.Value.Machines.Definition(MachineItem(slot)).Move, breeding.AllMachineMoves[slot]);
        Assert.AreEqual(new MoveId("pret:MOVE_DIVE"), breeding.AllMachineMoves[57]);
        foreach (var species in breeding.Species.Keys)
        {
            var expected = Enumerable.Range(0, 58).Where(slot =>
                Source.Value.Machines.Compatibility(MachineItem(slot), species).CanLearn)
                .Select(slot => breeding.AllMachineMoves[slot]);
            Assert.IsTrue(breeding.MachineCompatibility[species].SetEquals(expected), species.Value);
        }
        Assert.IsTrue(breeding.MachineCompatibility[new("pret:SPECIES_SQUIRTLE")].Contains(new("pret:MOVE_DIVE")));
        Assert.IsFalse(Source.Value.Machines.LoadCurrent(game).Machines.Any(machine => machine.Machine.Item.Id == MachineItem(57)));
    }

    [TestMethod]
    public void FlatBreedingParserRejectsUnknownMissingAndDuplicateProperties()
    {
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, BreedingPath));
        var node = JsonNode.Parse(json)!;
        node["species"]![0]!["machineMoves"] = new JsonArray();
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(node.ToJsonString()));
        node = JsonNode.Parse(json)!;
        node["species"]![0]!.AsObject().Remove("eggMoves");
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(node.ToJsonString()));
        node = JsonNode.Parse(json)!;
        node.AsObject().Remove("allMachineMoves");
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(node.ToJsonString()));
        Assert.ThrowsExactly<ContentValidationException>(() => Parse("{\"species\":[]," + json.TrimStart()[1..]));
    }

    [TestMethod]
    [DataRow("missing-machine-slot")]
    [DataRow("duplicate-machine-slot")]
    [DataRow("reordered-machine-slots")]
    [DataRow("missing-species")]
    [DataRow("duplicate-species")]
    [DataRow("unknown-species")]
    [DataRow("unknown-base-species")]
    [DataRow("unknown-egg-move")]
    public void CurrentLoaderRejectsInvalidBreedingReferencesAndSlotOrder(string mutation)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, BreedingPath)))!;
        var species = node["species"]!.AsArray();
        var moves = node["allMachineMoves"]!.AsArray();
        switch (mutation)
        {
            case "missing-machine-slot": moves.RemoveAt(57); break;
            case "duplicate-machine-slot": moves[57] = moves[0]!.DeepClone(); break;
            case "reordered-machine-slots":
                var first = moves[0]!.DeepClone();
                moves[0] = moves[1]!.DeepClone(); moves[1] = first; break;
            case "missing-species": species.RemoveAt(0); break;
            case "duplicate-species": species.Add(species[0]!.DeepClone()); break;
            case "unknown-species": species[0]!["species"]!["value"] = "pret:SPECIES_UNKNOWN"; break;
            case "unknown-base-species": species[0]!["baseEggSpecies"]!["value"] = "pret:SPECIES_UNKNOWN"; break;
            case "unknown-egg-move": species[0]!["eggMoves"]!.AsArray().Add(new JsonObject { ["value"] = "pret:MOVE_UNKNOWN" }); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var root = TestAssets.Output();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content/generated/data/pokemon/game"));
            File.Copy(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath), Path.Combine(root, PretGamePokemonCatalog.RelativePath));
            File.WriteAllText(Path.Combine(root, BreedingPath), node.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => Load(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static GameBreedingDocument Parse(string json) =>
        ContentJson.ParseStrict<GameBreedingDocument>(json, ContentJson.CreateOptions());
    private static GamePokemonCatalog Load(string root) => PretGamePokemonCatalog.Load(root,
        Source.Value.Canonical, Source.Value.Mechanics, Source.Value.Trades, Source.Value.Machines);
    private static ItemId MachineItem(int slot) => new(slot < 50 ? $"pret:ITEM_TM{slot + 1:00}" : $"pret:ITEM_HM{slot - 49:00}");
}
