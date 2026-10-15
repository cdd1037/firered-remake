using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MachineItemDefinitionTests
{
    private static readonly OpeningItemSourceRef OtherLocation = new("relocated/items", "description", 1, 2);
    private static PewterMachineItem AlternateText(PewterMachineItem item) =>
        item with { Description = "Another nonempty display description.", Item = item.Item with { Source = OtherLocation } };

    [TestMethod]
    public void WrapperSerializesOnlyItemAndDisplayedDescription()
    {
        var item = PretPewterMachines.LoadProject(TestAssets.Root).TmCase;
        var options = ContentJson.CreateOptions();
        var json = JsonSerializer.Serialize(item, options);
        using var parsed = JsonDocument.Parse(json);
        CollectionAssert.AreEquivalent(new[] { "item", "description" },
            parsed.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual(item, JsonSerializer.Deserialize<PewterMachineItem>(json, options));
        foreach (var field in new[] { "declarationDescription", "descriptionSource" })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<PewterMachineItem>(
                json[..^1] + ",\"" + field + "\":null}", options));
    }

    [TestMethod]
    [DataRow("TM06")]
    [DataRow("TM19")]
    public void GymDefinitionsAcceptDisplayEditsAndRelocatedSourceButRejectUnsupportedCapabilities(string symbol)
    {
        var machine = SharedMachineCatalog.LoadProject(TestAssets.Root).Definition(new("pret:ITEM_" + symbol)).Machine;
        Action<PewterMachineItem> validate = symbol == "TM06" ? FuchsiaGymItemRules.ValidateMachine : CeladonGymItemRules.ValidateMachine;
        validate(AlternateText(machine));
        validate(machine with { Item = machine.Item with { Source = null! } });
        Assert.ThrowsExactly<NotSupportedException>(() => validate(machine with { Description = "  " }));
        foreach (var invalid in new[] {
            machine.Item with { Pocket = "POCKET_ITEMS" },
            machine.Item with { HoldEffect = "HOLD_EFFECT_LEFTOVERS" },
            machine.Item with { CanToss = false },
            machine.Item with { BattleUsage = 1 },
            machine.Item with { FieldUseFunction = "FieldUseFunc_Medicine" },
            machine.Item with { Price = -1 } })
            Assert.ThrowsExactly<NotSupportedException>(() => validate(machine with { Item = invalid }));
    }

    [TestMethod]
    [DataRow("pewter")]
    [DataRow("ssanne")]
    [DataRow("route11")]
    [DataRow("rocktunnel")]
    public void RegionalMachineRulesValidateGameplayIndependentlyOfDisplayText(string region)
    {
        var catalog = region switch {
            "ssanne" => PretSsAnneMachines.LoadCatalogProject(TestAssets.Root),
            "route11" => PretRoute11Machines.LoadCatalogProject(TestAssets.Root),
            "rocktunnel" => PretRockTunnelMachines.LoadCatalogProject(TestAssets.Root),
            _ => PretPewterMachines.LoadProject(TestAssets.Root)
        };
        var changed = catalog with {
            Tm39 = AlternateText(catalog.Tm39), TmCase = AlternateText(catalog.TmCase),
            AdditionalMachines = catalog.AdditionalMachines?.Select(m => m with { Machine = AlternateText(m.Machine) }).ToImmutableArray()
        };
        PewterMachineRules.ValidateCatalog(changed);
        Assert.ThrowsExactly<NotSupportedException>(() => PewterMachineRules.ValidateCatalog(changed with { MovePp = 1 }));
        Assert.ThrowsExactly<NotSupportedException>(() => PewterMachineRules.ValidateCatalog(changed with {
            TmCase = changed.TmCase with { Item = changed.TmCase.Item with { CanToss = true } } }));
        Assert.ThrowsExactly<NotSupportedException>(() => PewterMachineRules.ValidateCatalog(changed with {
            Tm39 = changed.Tm39 with { Description = " " } }));
        var inventory = new InventoryState([], [], OpeningInventoryRules.SourceLimits) {
            Machines = [new(PewterMachineRules.Tm39Id, 1)]
        };
        Assert.AreEqual("inventory_tm_case_missing", PewterMachineRules.Validate(changed, inventory));
        inventory = inventory with { KeyItems = [new(PewterMachineRules.TmCaseId, 1)] };
        Assert.IsNull(PewterMachineRules.Validate(changed, inventory));
        Assert.AreEqual("inventory_stack_quantity", PewterMachineRules.Validate(changed, inventory with {
            Machines = [new(PewterMachineRules.Tm39Id, 1000)] }));
    }
}
