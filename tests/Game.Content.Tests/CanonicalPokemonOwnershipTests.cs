using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CanonicalPokemonOwnershipTests
{
    private static PretCanonicalPokemonCatalog Load() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);

    [TestMethod]
    public void RegionalReferencesShareCanonicalBusinessDataAndKeepEvolutionAdmissionExplicit()
    {
        var canonical = Load();
        var id = new SpeciesId("pret:SPECIES_BULBASAUR");
        var canonicalSpecies = canonical.Catalog.Species[id];
        Assert.IsTrue(canonicalSpecies.Evolutions.IsEmpty);
        Assert.AreSame(canonicalSpecies, canonical.Project(id, []));
        var projected = canonical.Project(id, [new(16, new("pret:SPECIES_IVYSAUR"))]);
        Assert.AreSame(canonicalSpecies.BaseStats, projected.BaseStats);
        Assert.AreEqual(canonicalSpecies.LevelUpMoves, projected.LevelUpMoves);
        Assert.AreEqual(16, projected.Evolutions.Single().Level);
        Assert.IsTrue(canonicalSpecies.Evolutions.IsEmpty);
        Assert.ThrowsExactly<JsonException>(() => canonical.Project(id, [new(15, new("pret:SPECIES_IVYSAUR"))]));
        Assert.ThrowsExactly<JsonException>(() => canonical.Project(id, [new(16, new("pret:SPECIES_CHARMANDER"))]));
        Assert.ThrowsExactly<JsonException>(() => canonical.Project(id, [new(16, new("pret:SPECIES_IVYSAUR")), new(16, new("pret:SPECIES_IVYSAUR"))]));
        Assert.AreEqual(386, canonical.Catalog.Species.Count);
        // Generic definition validation does not confer current-game admission.
        PokemonFactory.ValidateDefinitions(canonical.Catalog);
    }

    [TestMethod]
    public void ReferenceSchemasRejectInlineBusinessDefinitionsAndUnknownIds()
    {
        var canonical = Load();
        var options = canonical.ReferenceOptions();
        var species = JsonSerializer.Deserialize<PokemonSpeciesDefinition>("""{"id":{"value":"pret:SPECIES_BULBASAUR"},"evolutions":[]}""", options)!;
        var move = JsonSerializer.Deserialize<PokemonMoveDefinition>("""{"value":"pret:MOVE_TACKLE"}""", options)!;
        Assert.AreSame(canonical.Catalog.Species[species.Id], species);
        Assert.AreSame(canonical.Catalog.Moves[move.Id], move);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PokemonSpeciesDefinition>(JsonSerializer.Serialize(species, ContentJson.CreateOptions()), options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PokemonMoveDefinition>(JsonSerializer.Serialize(move, ContentJson.CreateOptions()), options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PokemonMoveDefinition>("""{"value":"pret:MOVE_NOT_A_MOVE"}""", options));
    }

    [TestMethod]
    public void CanonicalMetadataPreservesRealBreedingHeldItemAndPresentationFields()
    {
        var canonical = Load();
        var bulbasaur = canonical.Metadata[new("pret:SPECIES_BULBASAUR")];
        Assert.IsTrue(bulbasaur.EggGroups.SequenceEqual(["EGG_GROUP_MONSTER", "EGG_GROUP_GRASS"]));
        Assert.AreEqual(20, bulbasaur.EggCycles);
        Assert.AreEqual("ITEM_NONE", bulbasaur.ItemCommon);
        Assert.AreEqual("BODY_COLOR_GREEN", bulbasaur.BodyColor);
        Assert.AreEqual("ITEM_METAL_POWDER", canonical.Metadata[new("pret:SPECIES_DITTO")].ItemRare);
        var json = JsonSerializer.SerializeToElement(bulbasaur, ContentJson.CreateOptions());
        Assert.IsFalse(json.TryGetProperty("originalFields", out _));
        Assert.IsFalse(json.TryGetProperty("dexSource", out _));
        Assert.IsFalse(json.TryGetProperty("learnsetPointerSource", out _));
        Assert.IsTrue(json.TryGetProperty("noFlip", out _));
        Assert.IsTrue(json.TryGetProperty("safariZoneFleeRate", out _));
    }

    [TestMethod]
    public void CompleteMoveCatalogRejectsConflictingPredecessorBusinessValues()
    {
        var canonical = Load();
        var starter = PretPokemonCatalog.Load(canonical, File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/pokemon/starters.json")));
        var expanded = canonical.MoveCatalog.Extend(starter);
        Assert.AreEqual(354, expanded.Moves.Count);
        foreach (var move in expanded.Moves.Values) Assert.AreSame(canonical.Catalog.Moves[move.Id], move);
        var tackle = new MoveId("pret:MOVE_TACKLE");
        var corrupted = starter with { Moves = starter.Moves.SetItem(tackle, starter.Moves[tackle] with { Pp = 34 }) };
        Assert.ThrowsExactly<InvalidDataException>(() => canonical.MoveCatalog.Extend(corrupted));
    }
}
