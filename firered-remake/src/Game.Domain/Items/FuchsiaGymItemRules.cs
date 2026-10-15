using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Bounded TM06 reward inventory metadata; inherited items and shop stock stay unchanged.</summary>
public static class FuchsiaGymItemRules
{
    public const string Profile = "firered-fuchsia-gym-inventory-v1";
    public const int InheritedItemCount = PokemonTowerItemRules.ItemCount;
    public const int ItemCount = InheritedItemCount + 1;
    public static ItemId Tm06Id { get; } = new("pret:ITEM_TM06");
    private static bool IsSupportedItem(QuestItemDefinition? item) => item is not null &&
        item.Id == Tm06Id && item.SourceItemSymbol == "ITEM_TM06" && item.SourceItemId == 294 &&
        item.Name == "TM06" && item.Price == 3000 && item.Importance == 0 && item.Registrability == 0 &&
        item.Pocket == "POCKET_TM_CASE" && item.Type == "ITEM_TYPE_PARTY_MENU" &&
        item.HoldEffect == "HOLD_EFFECT_NONE" && item.HoldEffectParam == 0 && item.FieldUseFunction == "NULL" &&
        item.BattleUsage == 0 && item.BattleUseFunction == "NULL" && item.SecondaryId == 0 &&
        item.CanToss && item.CanSell && item.CanGive;

    public static void ValidateMachine(MachineItem machine)
    {
        if (machine is null || !IsSupportedItem(machine.Item) || string.IsNullOrWhiteSpace(machine.Description))
            throw new NotSupportedException("fuchsia_gym_tm06_source_metadata");
    }


}
