using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class SharedMachineCatalogTests
{
    [TestMethod]
    public void MachineReferencesShareDefinitionsAndCompatibilityAcrossProjections()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root);
        ItemId item = new("pret:ITEM_TM39");
        SpeciesId species = new("pret:SPECIES_BULBASAUR");
        var first = catalog.Project(item, [species]);
        var second = catalog.Project(item, [species]);
        Assert.AreSame(first.Machine, second.Machine);
        Assert.AreSame(first.Compatibility[0], second.Compatibility[0]);
        Assert.AreSame(catalog, SharedMachineCatalog.LoadProject(TestAssets.Root));
        var reference = JsonSerializer.Deserialize<PewterAdditionalMachine>(
            "{\"item\":\"pret:ITEM_TM39\",\"species\":[\"pret:SPECIES_BULBASAUR\"]}", catalog.CreateJsonOptions())!;
        Assert.AreSame(first.Machine, reference.Machine);
        Assert.AreSame(first.Compatibility[0], reference.Compatibility[0]);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PewterAdditionalMachine>(
            "{\"machine\":{},\"compatibility\":[]}", catalog.CreateJsonOptions()));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PewterMachineCompatibility>(
            "{\"item\":\"pret:ITEM_TM39\",\"species\":\"pret:SPECIES_BULBASAUR\",\"canLearn\":true}", catalog.CreateJsonOptions()));
    }

    [TestMethod]
    public void SharedCompatibilityRetainsHm08WithoutAdmittingATeachingDefinition()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var compatibility = catalog.Compatibility(new("pret:ITEM_HM08"), new("pret:SPECIES_EEVEE"));
        Assert.IsFalse(compatibility.CanLearn);
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Definition(new("pret:ITEM_HM08")));
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Project(new("pret:ITEM_TM39"), [new("pret:SPECIES_BULBASAUR"), new("pret:SPECIES_BULBASAUR")]));
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Compatibility(new("pret:ITEM_TM99"), new("pret:SPECIES_BULBASAUR")));
    }
}
