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
        var changed = AlternateText(machine) with { Item = machine.Item with { Name = "Training Disc" } };
        validate(changed);
        Assert.ThrowsExactly<NotSupportedException>(() => validate(changed with { Description = "  " }));
        foreach (var name in new string?[] { null, "", " \t\n" })
            Assert.ThrowsExactly<NotSupportedException>(() => validate(changed with {
                Item = changed.Item with { Name = name! } }));
        foreach (var invalid in new[] {
            machine.Item with { Id = new("pret:ITEM_POTION") },
            machine.Item with { SourceItemSymbol = "ITEM_POTION" },
            machine.Item with { SourceItemId = machine.Item.SourceItemId + 1 },
            machine.Item with { Pocket = "POCKET_ITEMS" },
            machine.Item with { Type = "ITEM_TYPE_FIELD" },
            machine.Item with { Importance = 1 },
            machine.Item with { Registrability = 1 },
            machine.Item with { HoldEffect = "HOLD_EFFECT_LEFTOVERS" },
            machine.Item with { HoldEffectParam = 1 },
            machine.Item with { CanToss = false },
            machine.Item with { CanSell = false },
            machine.Item with { CanGive = false },
            machine.Item with { BattleUsage = 1 },
            machine.Item with { BattleUseFunction = "BattleUseFunc_PokeBallEtc" },
            machine.Item with { FieldUseFunction = "FieldUseFunc_Medicine" },
            machine.Item with { SecondaryId = 1 },
            machine.Item with { Price = 2999 } })
            Assert.ThrowsExactly<NotSupportedException>(() => validate(changed with {
                Item = invalid with { Name = changed.Item.Name } }));
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
