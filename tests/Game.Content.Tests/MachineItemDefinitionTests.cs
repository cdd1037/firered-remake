using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MachineItemDefinitionTests
{
    private static MachineItem AlternateText(MachineItem item) =>
        item with { Description = "Another nonempty display description." };

    [TestMethod]
    public void WrapperSerializesOnlyItemAndDisplayedDescription()
    {
        var item = SharedMachineCatalog.LoadProject(TestAssets.Root).LoadCurrent(SharedMachineCatalogTests.Pokemon()).TmCase;
        var options = SharedMachineCatalog.LoadProject(TestAssets.Root).Items.CreateJsonOptions();
        var json = JsonSerializer.Serialize(item, options);
        using var parsed = JsonDocument.Parse(json);
        CollectionAssert.AreEquivalent(new[] { "item", "description" },
            parsed.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual(item, JsonSerializer.Deserialize<MachineItem>(json, options));
        foreach (var field in new[] { "declarationDescription", "descriptionSource" })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<MachineItem>(
                json[..^1] + ",\"" + field + "\":null}", options));
    }

    [TestMethod]
    [DataRow("TM06")]
    [DataRow("TM19")]
    public void GymDefinitionsAcceptDisplayEditsButRejectUnsupportedCapabilities(string symbol)
    {
        var machine = SharedMachineCatalog.LoadProject(TestAssets.Root).Definition(new("pret:ITEM_" + symbol)).Machine;
        Action<MachineItem> validate = symbol == "TM06" ? FuchsiaGymItemRules.ValidateMachine : CeladonGymItemRules.ValidateMachine;
        validate(AlternateText(machine));
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
    public void CurrentMachineRulesValidateGameplayIndependentlyOfDisplayText()
    {
        var catalog = SharedMachineCatalog.LoadProject(TestAssets.Root).LoadCurrent(SharedMachineCatalogTests.Pokemon());
        var changed = catalog with {
            TmCase = AlternateText(catalog.TmCase),
            Machines = catalog.Machines.Select(m => m with { Machine = AlternateText(m.Machine) }).ToImmutableArray()
        };
        MachineRules.ValidateCatalog(changed);
        Assert.ThrowsExactly<ArgumentException>(() => MachineRules.ValidateCatalog(changed with {
            Machines = changed.Machines.SetItem(0, changed.Machines[0] with { MovePp = 0 }) }));
        Assert.ThrowsExactly<ArgumentException>(() => MachineRules.ValidateCatalog(changed with {
            TmCase = changed.TmCase with { Item = changed.TmCase.Item with { CanToss = true } } }));
        Assert.ThrowsExactly<ArgumentException>(() => MachineRules.ValidateCatalog(changed with {
            Machines = changed.Machines.SetItem(0, changed.Machines[0] with {
                Machine = changed.Machines[0].Machine with { Description = " " } }) }));
        var inventory = new InventoryState([], [], OpeningInventoryRules.SourceLimits) {
            Machines = [new(MachineRules.Tm39Id, 1)]
        };
        Assert.AreEqual("inventory_tm_case_missing", MachineRules.Validate(changed, inventory));
        inventory = inventory with { KeyItems = [new(MachineRules.TmCaseId, 1)] };
        Assert.IsNull(MachineRules.Validate(changed, inventory));
        Assert.AreEqual("inventory_stack_quantity", MachineRules.Validate(changed, inventory with {
            Machines = [new(MachineRules.Tm39Id, 1000)] }));
        Assert.AreEqual("inventory_machine_unsupported", MachineRules.Validate(changed, inventory with {
            Machines = [new(new("pret:ITEM_HM08"), 1)] }));
    }
}
