using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Items;

public sealed record PokeFluteFieldResult(bool Succeeded, ImmutableArray<PartyPokemon> Party,
    ImmutableArray<int> AwakenedIndices, string? Error)
{
    public bool WokeSomeone => !AwakenedIndices.IsDefaultOrEmpty;
}

/// <summary>Pinned item_use.c FieldUseFunc_PokeFlute and battle_script_commands.c
/// VARIOUS_CHECK_POKEFLUTE. No inventory consumption or random draws.</summary>
public static class PokeFluteRules
{
    public const string Fanfare = "MUS_POKE_FLUTE";
    public const int SourceFanfareFrames = 450;
    public const string FieldPlayedMessage = "gText_PlayedPokeFlute";
    public const string FieldAwakenedMessage = "gText_PokeFluteAwakenedMon";
    public const string FieldNoEffectMessage = "gText_PlayedPokeFluteCatchy";

    public static PokeFluteFieldResult UseField(EarlyShopCatalog shop, CampaignPokemonContent content,
        InventoryState inventory, ImmutableArray<PartyPokemon> party)
    {
        var error = ValidateUse(shop, content, inventory, party);
        if (error is not null) return new(false, party, [], error);
        var changed = party;
        var awakened = ImmutableArray.CreateBuilder<int>();
        for (int i = 0; i < party.Length; i++)
        {
            // The field path applies Awakening directly; Soundproof is not consulted.
            if (!WildBattleStatus.IsSleeping(party[i].Status)) continue;
            changed = changed.SetItem(i, party[i] with { Status = party[i].Status & ~7u });
            awakened.Add(i);
        }
        return new(true, changed, awakened.ToImmutable(), null);
    }

    internal static string? ValidateUse(EarlyShopCatalog shop, CampaignPokemonContent content,
        InventoryState inventory, ImmutableArray<PartyPokemon> party)
    {
        if (shop.Profile != PokemonTowerItemRules.Profile || content.Catalog.Species.Count != 101)
            return "poke_flute_profile_not_enabled";
        try
        {
            if (EarlyShopInventoryRules.Validate(shop, inventory) is { } error) return error;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        { return "poke_flute_inventory_content"; }
        if (OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], PokemonTowerItemRules.PokeFluteId) != 1)
            return "poke_flute_item_missing";
        if (party.IsDefault || party.Length > 6) return "poke_flute_party_count";
        var identities = new HashSet<long>();
        foreach (var mon in party)
        {
            var error = PokemonStateValidator.ValidateParty(content, mon);
            if (error is not null) return error;
            if (!identities.Add(mon.InstanceId)) return "poke_flute_duplicate_identity";
        }
        return null;
    }

    internal static string? ValidateActive(ImmutableArray<PartyPokemon> party, int activeIndex,
        WildBattleCombatant active)
    {
        if (activeIndex < 0 || activeIndex >= party.Length) return "poke_flute_active_index";
        var owned = party[activeIndex];
        return SamePokemon(owned.Pokemon, active.Pokemon) && owned.Status == active.Status &&
            owned.HeldItem == (active.KnockedOffItem ?? active.HeldItem) ? null : "poke_flute_active_projection";
    }

    internal static bool SamePokemon(PokemonInstance a, PokemonInstance b) =>
        a with { Moves = b.Moves } == b && a.Moves.SequenceEqual(b.Moves);

    internal static bool BattleWakes(PartyPokemon mon) => !mon.IsEgg &&
        WildBattleStatus.IsSleeping(mon.Status) && mon.Pokemon.AbilityId != "ABILITY_SOUNDPROOF";
    internal static bool BattleWakes(WildBattleCombatant mon) =>
        WildBattleStatus.IsSleeping(mon.Status) && mon.Pokemon.AbilityId != "ABILITY_SOUNDPROOF";
    // Active battlers clear only sleep plus Nightmare, including an already awake battler.
    internal static WildBattleCombatant ClearActive(WildBattleCombatant mon) =>
        mon.Pokemon.AbilityId == "ABILITY_SOUNDPROOF" ? mon : mon with { Status = mon.Status & ~7u, Nightmare = false };
}
