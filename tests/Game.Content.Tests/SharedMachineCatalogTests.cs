using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class SharedMachineCatalogTests
{
    internal static GamePokemonCatalog Pokemon(string? root = null)
    {
        root ??= TestAssets.Root;
        var canonical = PretCanonicalPokemonCatalog.Load(root, root);
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, root);
        return PretGamePokemonCatalog.LoadContent(File.ReadAllText(Path.Combine(root,
            PretGamePokemonCatalog.RelativePath)), canonical, mechanics);
    }

    [TestMethod]
    public void ReusedConverterOptionsPreserveStrictReferencesAndOrdinaryItems()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var options = catalog.CreateJsonOptions();
        const string valid = """{"item":"pret:ITEM_TM39","species":"pret:SPECIES_BULBASAUR"}""";
        for (var i = 0; i < 3; i++)
            Assert.AreSame(catalog.Compatibility(new("pret:ITEM_TM39"), new("pret:SPECIES_BULBASAUR")),
                JsonSerializer.Deserialize<MachineCompatibility>(valid, options));
        foreach (var invalid in new[] {
            """{"item":"pret:ITEM_TM39"}""",
            """{"item":null,"species":"pret:SPECIES_BULBASAUR"}""",
            """{"item":"pret:ITEM_TM39","species":"pret:SPECIES_BULBASAUR","extra":1}""" })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineCompatibility>(invalid, options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineDefinition>(
            """{"item":"pret:ITEM_TM39","species":[],"extra":1}""", options));
        var ordinary = catalog.LoadCurrent(Pokemon()).TmCase;
        var json = JsonSerializer.Serialize(ordinary, catalog.Items.CreateJsonOptions());
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(ordinary, JsonSerializer.Deserialize<MachineItem>(json, options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineItem>(json[..^1] + ",\"extra\":1}", options));
        var permissive = new JsonSerializerOptions(options) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip };
        Assert.AreEqual(ordinary, JsonSerializer.Deserialize<MachineItem>(json[..^1] + ",\"extra\":1}", permissive));
    }

    [TestMethod]
    public void MachineReferencesShareDefinitionsAndCompatibilityWithinComposition()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root);
        ItemId item = new("pret:ITEM_TM39");
        SpeciesId species = new("pret:SPECIES_BULBASAUR");
        var first = catalog.Project(item, [species]);
        var second = catalog.Project(item, [species]);
        Assert.AreSame(first.Machine, second.Machine);
        Assert.AreSame(first.Compatibility[0], second.Compatibility[0]);
        var reference = JsonSerializer.Deserialize<MachineDefinition>(
            "{\"item\":\"pret:ITEM_TM39\",\"species\":[\"pret:SPECIES_BULBASAUR\"]}", catalog.CreateJsonOptions())!;
        Assert.AreSame(first.Machine, reference.Machine);
        Assert.AreSame(first.Compatibility[0], reference.Compatibility[0]);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineDefinition>(
            "{\"machine\":{},\"compatibility\":[]}", catalog.CreateJsonOptions()));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineCompatibility>(
            "{\"item\":\"pret:ITEM_TM39\",\"species\":\"pret:SPECIES_BULBASAUR\",\"canLearn\":true}", catalog.CreateJsonOptions()));
    }

    [TestMethod]
    public void SharedCompatibilityRetainsHm08WithoutAdmittingATeachingDefinition()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root);
        Assert.IsFalse(catalog.Compatibility(new("pret:ITEM_HM08"), new("pret:SPECIES_EEVEE")).CanLearn);
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Project(new("pret:ITEM_TM39"), [new("pret:SPECIES_BULBASAUR"), new("pret:SPECIES_BULBASAUR")]));
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Compatibility(new("pret:ITEM_TM99"), new("pret:SPECIES_BULBASAUR")));
    }

    [TestMethod]
    public void CurrentMachineSelectionUsesOwnedRosterAndCanonicalValues()
    {
        var shared = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var pokemon = Pokemon();
        var catalog = shared.LoadCurrent(pokemon);
        Assert.AreEqual(57, catalog.Machines.Length);
        Assert.AreEqual(MachineRules.Tm39Id, catalog.Machines[0].Machine.Item.Id);
        Assert.AreEqual(new ItemId("pret:ITEM_TM48"), catalog.Machines[^1].Machine.Item.Id);
        Assert.AreEqual(58, catalog.MachineSlots);
        Assert.AreEqual(30, catalog.KeyItemSlots);
        Assert.AreEqual(999, catalog.MaximumStack);
        foreach (var definition in catalog.Machines)
        {
            Assert.AreSame(shared.Definition(definition.Machine.Item.Id).Machine, definition.Machine);
            Assert.AreEqual(pokemon.Catalog.Moves[definition.Move].Pp, definition.MovePp);
            Assert.AreEqual(pokemon.Catalog.Species.Count, definition.Compatibility.Length);
            Assert.IsTrue(definition.Compatibility.Select(row => row.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys));
            foreach (var cell in definition.Compatibility)
                Assert.AreSame(shared.Compatibility(definition.Machine.Item.Id, cell.Species), cell);
        }
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(catalog.Machines[0].Compatibility[0]));
        CollectionAssert.AreEquivalent(new[] { "Species", "SourceSpeciesId", "CanLearn" },
            serialized.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        var invalid = catalog.Machines[0] with { MovePp = 0 };
        Assert.ThrowsExactly<ArgumentException>(() => MachineRules.ValidateCatalog(catalog with {
            Machines = catalog.Machines.SetItem(0, invalid) }));
        Assert.ThrowsExactly<ArgumentException>(() => MachineRules.ValidateCatalog(catalog with {
            Machines = catalog.Machines.Add(catalog.Machines[0]) }));
    }
}
