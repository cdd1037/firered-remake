using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Items;

/// <summary>The ordered party fields read by IsWildLevelAllowedByRepel; empty source slots are omitted.</summary>
public sealed record EarlyRepelPartyMember(int CurrentHp, int Level, bool IsEgg = false);
public sealed record EarlyRepelUseResult(InventoryState Inventory, ushort Steps, bool Succeeded,
    int ItemsConsumed, string? Error);
public sealed record EarlyRepelStepResult(ushort Steps, bool WoreOff);

/// <summary>Explicit Repel extension; Super Repel requires the Celadon approach opt-in.
/// Old encounter/item profiles retain their original 100-step contracts.</summary>
public static class EarlyRepelRules
{
    public const string SourceCommit = EarlyLandEncounterRules.SourceCommit;
    public const string RulesVersion = "firered-early-kanto-repel-v1";
    public const ushort Duration = 100;
    public const ushort SuperDuration = 200;
    public static ItemId Item { get; } = new("pret:ITEM_REPEL");

    /// <summary>Commit the returned inventory and counter together, after the source use sound completes.
    /// An active effect does not refresh, stack, or consume an item. No battle action is admitted.</summary>
    public static EarlyRepelUseResult Use(EarlyShopCatalog catalog, InventoryState inventory,
        ushort steps, PotionUseContext context, ItemId? item = null, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false)
    {
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        ArgumentNullException.ThrowIfNull(inventory);
        ValidateSteps(steps, celadonApproachRules || celadonGymRules);
        EarlyRepelUseResult Fail(string error) => new(inventory, steps, false, 0, error);
        var error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        bool towerCatalog = catalog.Profile is PokemonTowerItemRules.Profile or FuchsiaGymItemRules.Profile or SafariShopCatalog.Profile or SilphShopCatalog.Profile or SaffronGymShopCatalog.Profile;
        if (pokemonTowerRules ? !towerCatalog : rocketHideoutRules ? catalog.Profile != RocketHideoutItemRules.Profile : celadonGymRules ? catalog.Profile != CeladonGymItemRules.Profile : celadonApproachRules && catalog.Profile != LavenderItemRules.Profile)
            throw new NotSupportedException("Super Repel rules require the authenticated Lavender item profile.");
        if (context != PotionUseContext.Field) return Fail("repel_field_only");
        var selected = item ?? Item;
        if (selected != Item && !((pokemonTowerRules && towerCatalog || rocketHideoutRules && catalog.Profile == RocketHideoutItemRules.Profile || celadonGymRules && catalog.Profile == CeladonGymItemRules.Profile || celadonApproachRules && catalog.Profile == LavenderItemRules.Profile) &&
            selected == LavenderItemRules.SuperRepelId)) return Fail("repel_item_unsupported");
        var definition = catalog.Items.Single(i => i.Id == selected);
        int count = OpeningInventoryRules.Quantity(inventory.BagItems, selected);
        if (count == 0) return Fail("repel_not_in_bag");
        if (steps != 0) return Fail("repel_effects_lingered");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1),
            checked((ushort)definition.HoldEffectParam), true, 1, null);
    }

    /// <summary>Call only after trainer/on-frame, coordinate and step-warp scripts, friendship,
    /// poison and other earlier step scripts. Mark any such preemption PreemptedByScript.
    /// Call before walk-into-signpost and encounter checks. WoreOff starts the source message
    /// and preempts the entire encounter check, including cooldown/RNG, on that final step.</summary>
    public static EarlyRepelStepResult AdvanceStep(ushort steps, EncounterCheckTrigger trigger,
        bool forcedMovement = false, bool forcedMovementTile = false,
        bool inUnionRoom = false, bool questLogPlayback = false, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false)
    {
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        ValidateSteps(steps, celadonApproachRules || celadonGymRules);
        if (!Enum.IsDefined(trigger)) throw new NotSupportedException("Unknown field step trigger.");
        if (trigger != EncounterCheckTrigger.WalkingStepCompleted || forcedMovement || forcedMovementTile ||
            inUnionRoom || questLogPlayback || steps == 0)
            return new(steps, false);
        ushort remaining = (ushort)(steps - 1);
        return new(remaining, remaining == 0);
    }

    /// <summary>Source order is party order, skipping zero HP and eggs; equality is permitted.
    /// This projection does not enable egg gameplay or change the authoritative party validator.</summary>
    public static bool IsWildLevelAllowed(ushort steps, int wildLevel, ImmutableArray<EarlyRepelPartyMember> party,
        bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false)
    {
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        ValidateSteps(steps, celadonApproachRules || celadonGymRules);
        if (wildLevel is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(wildLevel));
        ValidateParty(party);
        if (steps == 0) return true;
        foreach (var member in party)
            if (member.CurrentHp != 0 && !member.IsEgg) return wildLevel >= member.Level;
        return false;
    }

    /// <summary>The caller supplies the already-decremented counter. All existing walking checks
    /// remain authoritative. Repel rejects after slot/level draws and before Pokémon generation;
    /// it clears the rate buff on rate miss or rejected candidate, but not on an earlier return.</summary>
    public static EarlyLandEncounterResult CheckLandEncounter(EarlyLandEncounterState state,
        EarlyLandEncounterTable table, EarlyLandEncounterCheck check, EarlyLandEncounterProfile profile,
        ImmutableArray<EarlyRepelPartyMember> party, RngState globalRng)
    {
        ArgumentNullException.ThrowIfNull(profile);
        bool celadonApproachRules = profile.RulesVersion is EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion or EarlyLandEncounterRules.RocketHideoutRulesVersion or EarlyLandEncounterRules.PokemonTowerRulesVersion or EarlyLandEncounterRules.SnorlaxRulesVersion or EarlyLandEncounterRules.FuchsiaRulesVersion or EarlyLandEncounterRules.SafariRulesVersion;
        ValidateSteps(profile.RepelSteps, celadonApproachRules);
        ValidateParty(party);
        var result = EarlyLandEncounterRules.Check(state, table, check, profile with { RepelSteps = 0 }, globalRng);
        if (profile.RepelSteps == 0) return result;
        if (result.Outcome == EncounterCheckOutcome.RateMiss)
            return result with { State = result.State with { EncounterRateBuff = 0 } };
        if (result.Candidate is null || IsWildLevelAllowed(profile.RepelSteps, result.Candidate.Level, party, celadonApproachRules))
            return result;
        return result with
        {
            State = result.State with { Pending = null, EncounterRateBuff = 0, PreviousMetatileBehavior = check.MetatileBehavior },
            Outcome = EncounterCheckOutcome.RepelSuppressed,
            Candidate = null
        };
    }

    public static void ValidateSteps(ushort steps, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false)
    {
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        if (steps > (celadonApproachRules || celadonGymRules ? SuperDuration : Duration))
            throw new NotSupportedException(celadonApproachRules
                ? "Only REPEL and the 200-step SUPER REPEL are admitted by this profile."
                : "Only the 100-step REPEL is admitted by this profile.");
    }

    private static void ValidateParty(ImmutableArray<EarlyRepelPartyMember> party)
    {
        if (party.IsDefault || party.Length > 6 || party.Any(p => p is null || p.CurrentHp is < 0 or > ushort.MaxValue || p.Level is < 1 or > 100))
            throw new ArgumentException("Repel requires an ordered party of at most six valid HP/level/egg projections.");
    }
}
