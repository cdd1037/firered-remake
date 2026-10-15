using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedBreedingMetadataTests
{
    // Frozen before the JSON migration: complete ordered gameplay rows, including every machine move.
    [TestMethod]
    public void SharedMachineProjectionPreservesEveryBreedingRow()
    {
        var root = TestAssets.Root;
        var original = GeneratedBreedingMetadata.Read<BreedingMetadataDocument>(root, "breeding-species/metadata.json");
        var ordinary = GeneratedBreedingMetadata.Read<OrdinaryEvolutionMetadataDocument>(root, "ordinary-evolution-species/metadata.json");
        var gift = GeneratedBreedingMetadata.Read<GiftEggMetadataDocument>(root, "gift-egg-species/metadata.json");
        Assert.AreEqual(58, original.AllMachineMoves.Length);
        Assert.AreEqual("pret:MOVE_DIVE", original.AllMachineMoves[57].Value);
        Check(GeneratedBreedingMetadata.Resolve(root, original.Species, original.AllMachineMoves), 182,
            "def8c33edd5d8dd349886eac2e04c3e56b8f061d277094e0354f7a49b75a4ebb");
        Check(GeneratedBreedingMetadata.Resolve(root, ordinary.Additions), 4,
            "2de512d18c69df67ae1488ea673b979611da0c092f0d89c239124d885daee1e3");
        Check(GeneratedBreedingMetadata.Resolve(root, gift.Additions), 2,
            "9c38206d3399abb2271429172f92d7e5c0f3238362bac2eb3908e8c1b1ca0745");
    }

    [TestMethod]
    public void CompactRowsRejectLegacyDuplicateAndMissingFields()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/breeding-species/metadata.json")))!;
        var row = node["species"]![0]!;
        Assert.IsNull(row["machineMoves"]);
        row["machineMoves"] = new JsonArray();
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(node.ToJsonString()));
        row.AsObject().Remove("machineMoves");
        row.AsObject().Remove("eggMoves");
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(node.ToJsonString()));
        var json = File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/breeding-species/metadata.json"));
        Assert.ThrowsExactly<ContentValidationException>(() => Parse(json.Replace("\"speciesCount\":182", "\"speciesCount\":182,\"speciesCount\":182", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MachineProjectionRejectsMissingSlotsAndUnknownSpecies()
    {
        var document = GeneratedBreedingMetadata.Read<BreedingMetadataDocument>(TestAssets.Root, "breeding-species/metadata.json");
        Assert.ThrowsExactly<InvalidDataException>(() => GeneratedBreedingMetadata.Resolve(TestAssets.Root,
            document.Species, document.AllMachineMoves.RemoveAt(57)));
        var row = document.Species[0];
        Assert.ThrowsExactly<InvalidDataException>(() => GeneratedBreedingMetadata.Resolve(TestAssets.Root,
            [row with { Breeding = row.Breeding with { Species = new("pret:SPECIES_UNKNOWN") } }], document.AllMachineMoves));
    }

    private static BreedingMetadataDocument Parse(string json) =>
        ContentJson.ParseStrict<BreedingMetadataDocument>(json, ContentJson.CreateOptions());

    private static void Check(ImmutableArray<SourceEggSpeciesRow> rows, int count, string expected)
    {
        Assert.AreEqual(count, rows.Length);
        var text = string.Join('\n', rows.Select(row => string.Join('|', row.Breeding.Species.Value,
            row.Breeding.EggGroup1.ToString(CultureInfo.InvariantCulture), row.Breeding.EggGroup2.ToString(CultureInfo.InvariantCulture),
            row.Breeding.EggCycles.ToString(CultureInfo.InvariantCulture), row.BaseEggSpecies.Value,
            string.Join(',', row.EggMoves.Select(move => move.Value)),
            string.Join(',', row.MachineMoves.Select(move => move.Value))))) + "\n";
        Assert.AreEqual(expected, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }
}
