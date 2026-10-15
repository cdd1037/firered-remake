using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Only the original TM19 reward item is added; every prior item and shop is unchanged.</summary>
public static class CeladonGymItemRules
{
    public const string Profile = "firered-celadon-gym-items-v1";
    public static ItemId Tm19Id { get; } = new("pret:ITEM_TM19");
    private static bool IsSupportedItem(QuestItemDefinition? item) => item is not null &&
        item.Id == Tm19Id && item.SourceItemSymbol == "ITEM_TM19" && item.SourceItemId == 307 &&
        item.Name == "TM19" && item.Price == 3000 && item.Importance == 0 && item.Registrability == 0 &&
        item.Pocket == "POCKET_TM_CASE" && item.Type == "ITEM_TYPE_PARTY_MENU" &&
        item.HoldEffect == "HOLD_EFFECT_NONE" && item.HoldEffectParam == 0 && item.FieldUseFunction == "NULL" &&
        item.BattleUsage == 0 && item.BattleUseFunction == "NULL" && item.SecondaryId == 0 &&
        item.CanToss && item.CanSell && item.CanGive;

    public static void ValidateMachine(MachineItem machine)
    {
        if (machine is null || !IsSupportedItem(machine.Item) || string.IsNullOrWhiteSpace(machine.Description))
            throw new NotSupportedException("Exact source TM19 metadata required.");
    }

    public static bool IsBerry(ItemId item) => LavenderItemRules.IsBerry(item);
    public static string? ValidateBerries(InventoryState state) => LavenderItemRules.ValidateBerries(state);
}
