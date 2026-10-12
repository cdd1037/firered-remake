using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Items;

/// <summary>The authenticated Saffron successor admits MAX_REPEL's 250 source steps.
/// Existing EarlyRepelRules retains its historical 100/200-step limits.</summary>
public static class SaffronGymRepelRules
{
    public const string RulesVersion = "firered-saffron-gym-repel-v1";
    public const ushort MaxDuration = 250;

    public static void ValidateSteps(SaffronGymShopCatalog shop, ushort steps)
    {
        if (shop is null || !ReferenceEquals(SaffronGymShopCatalog.For(shop.Catalog), shop))
            throw new NotSupportedException("saffron_gym_repel_catalog");
        if (steps > MaxDuration) throw new NotSupportedException("saffron_gym_repel_steps");
    }

    public static EarlyRepelUseResult Use(SaffronGymShopCatalog shop, InventoryState inventory,
        ushort steps, PotionUseContext context, ItemId? item = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ValidateSteps(shop, steps);
        EarlyRepelUseResult Fail(string error) => new(inventory, steps, false, 0, error);
        if (EarlyShopInventoryRules.Validate(shop.Catalog, inventory) is { } error) return Fail(error);
        if (context != PotionUseContext.Field) return Fail("repel_field_only");
        var selected = item ?? EarlyRepelRules.Item;
        if (selected != EarlyRepelRules.Item && selected != LavenderItemRules.SuperRepelId &&
            selected != SaffronGymShopCatalog.MaxRepelId) return Fail("repel_item_unsupported");
        var definition = shop.Catalog.Items.Single(i => i.Id == selected);
        int count = OpeningInventoryRules.Quantity(inventory.BagItems, selected);
        if (count == 0) return Fail("repel_not_in_bag");
        if (steps != 0) return Fail("repel_effects_lingered");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1),
            checked((ushort)definition.HoldEffectParam), true, 1, null);
    }

    /// <summary>Preserves script/warp preemption and forced-movement semantics. On the
    /// final step, WoreOff preempts encounter generation before any encounter RNG.</summary>
    public static EarlyRepelStepResult AdvanceStep(SaffronGymShopCatalog shop, ushort steps,
        EncounterCheckTrigger trigger, bool forcedMovement = false, bool forcedMovementTile = false,
        bool inUnionRoom = false, bool questLogPlayback = false)
    {
        ValidateSteps(shop, steps);
        if (!Enum.IsDefined(trigger)) throw new NotSupportedException("Unknown field step trigger.");
        if (trigger != EncounterCheckTrigger.WalkingStepCompleted || forcedMovement || forcedMovementTile ||
            inUnionRoom || questLogPlayback || steps == 0) return new(steps, false);
        ushort remaining = (ushort)(steps - 1);
        return new(remaining, remaining == 0);
    }

    public static bool IsWildLevelAllowed(SaffronGymShopCatalog shop, ushort steps, int wildLevel,
        ImmutableArray<EarlyRepelPartyMember> party)
    {
        ValidateSteps(shop, steps);
        if (wildLevel is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(wildLevel));
        ValidateParty(party);
        if (steps == 0) return true;
        foreach (var member in party)
            if (member.CurrentHp != 0 && !member.IsEgg) return wildLevel >= member.Level;
        return false;
    }

    /// <summary>The caller supplies the decremented counter; source slot/level RNG
    /// precedes repel suppression. Earlier returns and rate-miss state are preserved.</summary>
    public static EarlyLandEncounterResult CheckLandEncounter(SaffronGymShopCatalog shop,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        EarlyLandEncounterProfile profile, ImmutableArray<EarlyRepelPartyMember> party, RngState globalRng)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateSteps(shop, profile.RepelSteps);
        ValidateParty(party);
        var result = EarlyLandEncounterRules.Check(state, table, check, profile with { RepelSteps = 0 }, globalRng);
        if (profile.RepelSteps == 0) return result;
        if (result.Outcome == EncounterCheckOutcome.RateMiss)
            return result with { State = result.State with { EncounterRateBuff = 0 } };
        if (result.Candidate is null || IsWildLevelAllowed(shop, profile.RepelSteps, result.Candidate.Level, party))
            return result;
        return result with
        {
            State = result.State with { Pending = null, EncounterRateBuff = 0, PreviousMetatileBehavior = check.MetatileBehavior },
            Outcome = EncounterCheckOutcome.RepelSuppressed,
            Candidate = null
        };
    }

    private static void ValidateParty(ImmutableArray<EarlyRepelPartyMember> party)
    {
        if (party.IsDefault || party.Length > 6 || party.Any(p => p is null ||
            p.CurrentHp is < 0 or > ushort.MaxValue || p.Level is < 1 or > 100))
            throw new ArgumentException("Repel requires an ordered party of at most six valid HP/level/egg projections.");
    }
}
