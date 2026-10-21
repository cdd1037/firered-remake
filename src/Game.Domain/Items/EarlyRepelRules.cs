using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>The ordered party fields read by IsWildLevelAllowedByRepel; empty source slots are omitted.</summary>
public sealed record EarlyRepelPartyMember(int CurrentHp, int Level, bool IsEgg = false);
public sealed record EarlyRepelUseResult(InventoryState Inventory, ushort Steps, bool Succeeded,
    int ItemsConsumed, string? Error);
public sealed record EarlyRepelStepResult(ushort Steps, bool WoreOff);

/// <summary>Shared Repel identifiers and the explicit nonextended encounter limit.
/// Current item consumption and 250-step behavior belong to RepelRules.</summary>
public static class EarlyRepelRules
{
    public const string SourceCommit = EarlyLandEncounterRules.SourceCommit;
    public const string RulesVersion = "firered-early-kanto-repel-v1";
    public const ushort Duration = 100;
    public const ushort SuperDuration = 200;
    public static ItemId Item { get; } = new("pret:ITEM_REPEL");

    public static void ValidateSteps(ushort steps, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false)
    {
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        if (steps > (celadonApproachRules || celadonGymRules ? SuperDuration : Duration))
            throw new NotSupportedException(celadonApproachRules
                ? "Only REPEL and the 200-step SUPER REPEL are admitted by this profile."
                : "Only the 100-step REPEL is admitted by this profile.");
    }

}
