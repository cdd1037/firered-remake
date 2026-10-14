using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record GameCornerPrizeDefinition(int Index, string Reward, int SourceRewardId, int Coins, int Level,
    string? HeldItem, string? Move, OpeningItemSourceRef Source, OpeningItemSourceRef? GiveSource);
public sealed record GameCornerPrizeCounter(string Kind, int ActorLocalId, int X, int Y, int Elevation, string ClerkScript,
    string MenuSymbol, int MenuId, ImmutableArray<string> MenuLabels, ImmutableArray<GameCornerPrizeDefinition> Prizes,
    OpeningItemSourceRef Source, OpeningItemSourceRef MenuSource);

/// <summary>All original FireRed prize rows bound to their exact item, machine and Pokemon owners. No wallet or redemption transaction.</summary>
public sealed class GameCornerPrizeCatalog
{
    public const string SourcePath = "data/maps/CeladonCity_GameCorner_PrizeRoom/scripts.inc";
    public const string Map = "map.celadon-city-game-corner-prize-room";
    public const string CountersSourceSha256 = "2c9a3709cc44ec01ee81f6ad69b01f2f2fd83208a5e7d2f46373bcd5fb0f134f";
    public PokemonCatalog Pokemon { get; }
    public EarlyShopCatalog Items { get; }
    public PewterMachineCatalog Machines { get; }
    public ImmutableArray<GameCornerPrizeCounter> Counters { get; }
    public ImmutableArray<GameCornerPrizeDefinition> PokemonPrizes => Counters[0].Prizes;
    public ImmutableArray<GameCornerPrizeDefinition> MachinePrizes => Counters[1].Prizes;
    public ImmutableArray<GameCornerPrizeDefinition> ItemPrizes => Counters[2].Prizes;
    private GameCornerPrizeCatalog(PokemonCatalog pokemon, EarlyShopCatalog items, PewterMachineCatalog machines,
        ImmutableArray<GameCornerPrizeCounter> counters)
    { Pokemon = pokemon; Items = items; Machines = machines; Counters = counters; }
    public static GameCornerPrizeCatalog Authenticate(PokemonCatalog pokemon, EarlyShopCatalog items, PewterMachineCatalog machines,
        ImmutableArray<GameCornerPrizeCounter> counters)
    {
        ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(machines);
        if (OwnedBattlePokemonCatalog.For(pokemon) is not { } current ||
            OwnedBattlePokemonCatalog.SourceAncestor(current, GameCornerOwnedPokemonCatalog.For) is not { } owned ||
            OwnedMachineCatalogBinding.SourceAncestor(machines, GameCornerMachineCatalog.For) is not { } teaching ||
            !ReferenceEquals(OwnedMachineCatalogBinding.For(machines), current) ||
            !ReferenceEquals(teaching.Pokemon, owned) || OrdinaryItemInventoryCatalog.For(items) is not { } ordinary ||
            items.Items.Length != 137 || !OrdinaryItemInventoryCatalog.GameCornerPrizeItems.All(ordinary.IsAddition) ||
            counters.IsDefault || counters.Length != 3 || counters.Any(c => c is null || c.Prizes.IsDefault || c.Prizes.Length != 5) ||
            CampaignSourceFingerprint.Compute(counters.ToArray()) != CountersSourceSha256)
            throw new ArgumentException("game_corner_prize_owner_or_source");
        foreach (var row in counters[0].Prizes)
            if (!pokemon.Species.TryGetValue(new SpeciesId(row.Reward), out var definition) || definition.SourceSpeciesId != row.SourceRewardId)
                throw new ArgumentException("game_corner_prize_species");
        foreach (var row in counters[1].Prizes)
            if (PewterMachineRules.Definition(machines, new ItemId(row.Reward)) is not { } machine ||
                machine.Machine.Item.SourceItemId != row.SourceRewardId || machine.Move.Value != row.Move)
                throw new ArgumentException("game_corner_prize_machine");
        foreach (var row in counters[2].Prizes)
            if (EarlyShopInventoryRules.Find(items, new ItemId(row.Reward))?.SourceItemId != row.SourceRewardId)
                throw new ArgumentException("game_corner_prize_item");
        return new(pokemon, items, machines, counters);
    }
    public GameCornerPrizeCounter? CounterFor(string map, string path, string clerkScript, int actorLocalId) =>
        map == Map && path == SourcePath ? Counters.SingleOrDefault(c => c.ClerkScript == clerkScript && c.ActorLocalId == actorLocalId) : null;
}
