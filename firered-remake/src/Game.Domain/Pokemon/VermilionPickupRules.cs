using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Pokemon;

public sealed record VermilionPickupAward(int PartyIndex,long InstanceId,string Item);
public sealed record VermilionPickupResult(ImmutableArray<PartyPokemon> Party,RngState Rng,
    ImmutableArray<RandomTrace> Trace,ImmutableArray<VermilionPickupAward> Awards);

/// <summary>Cmd_pickup, pinned battle_script_commands.c772–792,9261–9287. Only local victory invokes this command.</summary>
public static class VermilionPickupRules
{
    public static ImmutableArray<string> Items { get; } = ["ITEM_ORAN_BERRY","ITEM_CHERI_BERRY","ITEM_CHESTO_BERRY",
        "ITEM_PECHA_BERRY","ITEM_RAWST_BERRY","ITEM_ASPEAR_BERRY","ITEM_PERSIM_BERRY","ITEM_TM10","ITEM_PP_UP",
        "ITEM_RARE_CANDY","ITEM_NUGGET","ITEM_SPELON_BERRY","ITEM_PAMTRE_BERRY","ITEM_WATMEL_BERRY","ITEM_DURIN_BERRY","ITEM_BELUE_BERRY"];
    public static bool IsKnownHeldItem(string item) => VermilionBattleScope.SupportsHeldItem(item);
    private static readonly int[] thresholds=[15,25,35,45,55,65,75,80,85,90,95,96,97,98,99];
    public static string ItemForRoll(int roll)
    {
        if(roll is <0 or >99)throw new ArgumentOutOfRangeException(nameof(roll));
        int i=0;while(i<thresholds.Length && thresholds[i]<=roll)i++;return Items[i];
    }
    public static VermilionPickupResult Apply(PokemonCatalog catalog,ImmutableArray<PartyPokemon> party,RngState rng,WildBattleOutcome outcome, bool ssAnneRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false, bool snorlaxRules = false, bool fuchsiaRules = false)
    {
        if (fuchsiaRules) PokemonFactory.ValidateCatalog(catalog);
        else if ((Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102) != snorlaxRules) throw new NotSupportedException("Explicit Snorlax catalog profile required.");
        pokemonTowerRules |= snorlaxRules;
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        route11Rules |= rockTunnelRules;
        if(catalog.SourceCommit!=WildBattleRules.SourceCommit || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog)!=(fuchsiaRules ? 107 : snorlaxRules ? 102 : pokemonTowerRules ? 101 : rocketHideoutRules ? 99 : celadonGymRules ? 98 : celadonApproachRules ? 94 : rockTunnelRules ? 93 : route11Rules ? 79 : ssAnneRules ? 73 : 70))
            throw new NotSupportedException("Pickup requires the complete explicitly selected catalog.");
        return ApplyCore(catalog, party, rng, outcome);
    }
    public static VermilionPickupResult ApplySafariOwned(SafariOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, RngState rng, WildBattleOutcome outcome) =>
        ApplySafariOwned((OwnedBattlePokemonCatalog)owned, party, rng, outcome);

    public static VermilionPickupResult ApplySafariOwned(OwnedBattlePokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, RngState rng, WildBattleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(owned);
        return ApplyCore(owned.Catalog, party, rng, outcome);
    }
    private static VermilionPickupResult ApplyCore(PokemonCatalog catalog,
        ImmutableArray<PartyPokemon> party, RngState rng, WildBattleOutcome outcome)
    {
        if(party.IsDefault || party.Length is <1 or >6 || party.Any(p=>p is null) || party.Select(p=>p.InstanceId).Distinct().Count()!=party.Length)
            throw new ArgumentException("Invalid Pickup party.");
        if(!Enum.IsDefined(outcome) || outcome==WildBattleOutcome.Ongoing)throw new ArgumentException("Pickup requires a terminal outcome.");
        if(rng.AlgorithmId!=Gen3Rng.AlgorithmId)throw new NotSupportedException("Unknown Pickup RNG.");
        foreach(var p in party)
            if(PokemonStateValidator.Validate(catalog,p.Pokemon) is not null || string.IsNullOrEmpty(p.HeldItem) || p.InstanceId<=0)
                throw new ArgumentException("Invalid Pickup party member.");
        if(outcome!=WildBattleOutcome.Won)return new(party,rng,[],[]);
        var trace=ImmutableArray.CreateBuilder<RandomTrace>();var awards=ImmutableArray.CreateBuilder<VermilionPickupAward>();
        for(int i=0;i<party.Length;i++)
        {
            var p=party[i];
            // Source neither requires participation nor checks HP. Existing held items and eggs short-circuit RNG.
            if(p.IsEgg || p.Pokemon.AbilityId!="ABILITY_PICKUP" || p.HeldItem!="ITEM_NONE")continue;
            var chance=Gen3Rng.Next(rng,$"vermilion.pickup.party-{i}.chance",10);rng=chance.State;trace.Add(chance.Trace);
            if(chance.Value%10!=0)continue;
            var item=Gen3Rng.Next(rng,$"vermilion.pickup.party-{i}.item",100);rng=item.State;trace.Add(item.Trace);
            string symbol=ItemForRoll(item.Value%100);party=party.SetItem(i,p with{HeldItem=symbol});awards.Add(new(i,p.InstanceId,symbol));
        }
        return new(party,rng,trace.ToImmutable(),awards.ToImmutable());
    }
}
