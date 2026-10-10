using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Items;

/// <summary>The owned identity is resolved against this transaction's party, never a stale UI index.</summary>
public sealed record BattleMedicineSelection(ItemId Item, long TargetInstanceId);

internal sealed record BattleMedicinePreparation(int TargetIndex, EarlyMedicineUseResult Medicine,
    ImmutableArray<PartyPokemon> Party, bool HasPlayerReserves);

/// <summary>Shared owned-party boundary for ordinary single-battle medicine transactions.</summary>
internal static class BattleMedicineRules
{
    internal static string? Prepare(EarlyShopCatalog shop, OpeningItemsCatalog opening,
        CampaignPokemonContent content, InventoryState inventory, ImmutableArray<PartyPokemon> party,
        int activeIndex, WildBattleCombatant active, BattleMedicineSelection? selection,
        out BattleMedicinePreparation? preparation)
    {
        preparation = null;
        if (selection is null) return "battle_medicine_cancelled";
        if (party.IsDefaultOrEmpty || party.Length > 6) return "battle_medicine_party_count";
        if (activeIndex < 0 || activeIndex >= party.Length) return "battle_medicine_active_index";
        var identities = new HashSet<long>();
        int targetIndex = -1;
        for (int i = 0; i < party.Length; i++)
        {
            var error = PokemonStateValidator.ValidateParty(content, party[i]);
            if (error is not null) return error;
            if (!identities.Add(party[i].InstanceId)) return "battle_medicine_duplicate_identity";
            if (party[i].InstanceId == selection.TargetInstanceId) targetIndex = i;
        }
        if (targetIndex < 0) return "battle_medicine_target_identity";
        var ownedActive = party[activeIndex];
        if (!SamePokemon(ownedActive.Pokemon, active.Pokemon) || ownedActive.Status != active.Status ||
            ownedActive.HeldItem != (active.KnockedOffItem ?? active.HeldItem)) return "battle_medicine_active_projection";
        try
        {
            var medicine = EarlyShopMedicineRules.Use(shop, opening, content, inventory, party[targetIndex],
                selection.Item, PotionUseContext.Battle);
            if (!medicine.Succeeded) return medicine.Error ?? "battle_medicine_no_effect";
            var after = party.SetItem(targetIndex, medicine.Pokemon);
            preparation = new(targetIndex, medicine, after,
                after.Where((p, i) => i != activeIndex).Any(p => p.Pokemon.CurrentHp > 0));
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or OverflowException)
        {
            return "battle_medicine_content_unsupported";
        }
    }

    // ImmutableArray record equality compares backing arrays; independently deserialized copies
    // are allowed only when every move (including PP/PP Ups) and every other instance field agrees.
    private static bool SamePokemon(PokemonInstance a, PokemonInstance b) =>
        a with { Moves = b.Moves } == b && a.Moves.SequenceEqual(b.Moves);

    internal static ImmutableArray<PartyPokemon> Synchronize(ImmutableArray<PartyPokemon> party,
        int activeIndex, WildBattleCombatant active) => party.SetItem(activeIndex, party[activeIndex] with
        { Pokemon = active.Pokemon, Status = active.Status, HeldItem = active.KnockedOffItem ?? active.HeldItem });
}
