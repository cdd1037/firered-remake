using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Items;

internal sealed record SourceCanonicalItemPreparation(SourceBattleFrame Frame, InventoryState Inventory,
    ImmutableArray<PartyPokemon> Party, SourceBattleItemDefinition Item, PartyMemberKey Target, long TargetInstanceId);

internal static partial class SourceBattleItemRules
{
    internal static string? ValidateParty(SourceMechanicsCatalog mechanics, SourceBattleFrame frame, ImmutableArray<PartyPokemon> party)
    {
        var actual = frame.Party(BattleSide.Player).Members;
        if (party.IsDefaultOrEmpty || party.Length != actual.Length) return "source_item_party_context";
        var identities = new HashSet<long>();
        var ownership = frame.Campaign;
        for (int i = 0; i < party.Length; i++)
        {
            var owned = party[i]; var member = actual[i];
            if (owned?.Pokemon is null || !WildBattleSourcePartyEffects.SamePokemon(owned.Pokemon, member.Pokemon) ||
                owned.Status != member.Status || owned.IsEgg != member.IsEgg ||
                owned.HeldItem != WildBattleSourcePartyEffects.PersistentHeldItem(member)) return "source_item_party_projection";
            if (owned.IsEgg)
            {
                if (ownership is null || PokemonStateValidator.ValidateOwnedForBattle(ownership, owned) is not null ||
                    SourcePokemonValidator.ValidatePartyMember(mechanics.Catalog, member, ownership) is not null)
                    return "source_item_egg_reserve";
                if (owned.InstanceId <= 0 || !identities.Add(owned.InstanceId)) return "source_item_duplicate_identity";
                continue;
            }
            if (SourcePokemonValidator.ValidatePermanent(mechanics, owned.Pokemon) is { } error) return error;
            if (owned.InstanceId <= 0 || !identities.Add(owned.InstanceId)) return "source_item_duplicate_identity";
            bool Name(string value, int max) => !string.IsNullOrEmpty(value) && value.Length <= max &&
                value.Any(c => c != ' ') && value.All(PokemonStateValidator.NicknameAlphabet.Contains);
            bool Symbol(string value, string prefix) => value is not null && value.StartsWith(prefix, StringComparison.Ordinal) &&
                value.Length > prefix.Length && value[prefix.Length..].All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
            if (!PokemonNicknameRules.IsValidCustomNickname(owned.CustomNickname) || !Name(owned.OriginalTrainerName,7) || owned.OriginalTrainerGender is not (0 or 1) ||
                owned.Language is not (1 or 2 or 3 or 4 or 5 or 7) || PokemonStateValidator.ValidateOriginMetadata(mechanics.Catalog,owned) is not null ||
                owned.MetLevel < 0 || owned.MetLevel > owned.Pokemon.Level || owned.MetGame is not (1 or 2 or 3 or 4 or 5 or 7 or 8 or 10 or 11 or 12 or 15) ||
                owned.Ball is not ("ITEM_MASTER_BALL" or "ITEM_ULTRA_BALL" or "ITEM_GREAT_BALL" or "ITEM_POKE_BALL" or "ITEM_SAFARI_BALL" or "ITEM_NET_BALL" or "ITEM_DIVE_BALL" or "ITEM_NEST_BALL" or "ITEM_REPEAT_BALL" or "ITEM_TIMER_BALL" or "ITEM_LUXURY_BALL" or "ITEM_PREMIER_BALL") ||
                !Symbol(owned.HeldItem,"ITEM_") || SourceMailRules.ValidateAttachmentReference(owned) is not null || !SourcePartyStatusRules.IsSupported(owned.Status,true,true,true))
                return "source_item_owned_metadata";
        }
        return null;
    }
    internal static string? Prepare(SourceBattleItemCatalog items, SourceMechanicsCatalog mechanics,
        SourceBattleFrame frame, OccupantToken actor, InventoryState inventory, ImmutableArray<PartyPokemon> party,
        SourceBattleItemSelection? selection, string? currentMapSection, out SourceCanonicalItemPreparation? preparation)
    {
        preparation = null;
        if (selection is null) return "battle_medicine_cancelled";
        if (ValidateParty(mechanics,frame,party) is { } invalid) return invalid;
        if (SourceBattleInventoryRules.Validate(items,inventory) is { } bagError) return bagError;
        if (items.Find(selection.Item) is not { } item) return "source_item_effect_unsupported";
        if (!(item.IsFixedEffect || item.IsPokeFlute || item.IsEscapeItem)) return item.Symbol == "ITEM_ENIGMA_BERRY" ? "source_item_enigma_data_required" : "source_item_effect_unsupported";
        if (item.IsEscapeItem && frame.BattleKind != WildBattleCalledBattleKind.Wild) return "source_item_escape_trainer";
        if (item.IsEscapeItem && frame.WildEncounter is not { ScriptedSnorlax: false, TowerBattleKind: null or PokemonTowerBattleKind.OrdinaryWild }) return "source_item_escape_special_scope";
        if (SourceBattleInventoryRules.Quantity(inventory,item) == 0) return "source_item_not_in_bag";
        int index = Enumerable.Range(0,party.Length).FirstOrDefault(i => party[i].InstanceId == selection.TargetInstanceId,-1);
        if (index < 0) return "battle_medicine_target_identity";
        var target = new PartyMemberKey(BattleSide.Player,index);
        if ((item.IsPokeFlute || item.IsEscapeItem) && (target != actor.Member || selection.MoveSlot is not null)) return "source_item_flute_target";
        var owned = party[index];
        if (item.IsStatBooster && string.IsNullOrWhiteSpace(currentMapSection)) return "source_item_friendship_context";
        var friendship = currentMapSection is null ? null : new SourceItemFriendshipContext(owned.Ball,owned.MetLocation,currentMapSection);
        var used = item.IsPokeFlute || item.IsEscapeItem ? new SourceBattleItemEffectResult(frame,false,null) :
            SourceBattleItemEffects.Apply(items,frame,actor,target,item.Id,selection.MoveSlot,friendship);
        if (!used.Succeeded) return used.Fault;
        preparation = new(used.State,SourceBattleInventoryRules.Consume(inventory,item),Synchronize(party,used.State),item,target,owned.InstanceId);
        return null;
    }
    internal static ImmutableArray<PartyPokemon> Synchronize(ImmutableArray<PartyPokemon> owned, SourceBattleFrame frame) =>
        owned.Select((p,i) => p with { Pokemon = frame.Parties[0].Members[i].Pokemon, Status = frame.Parties[0].Members[i].Status,
            HeldItem = WildBattleSourcePartyEffects.PersistentHeldItem(frame.Parties[0].Members[i]) }).ToImmutableArray();
}
