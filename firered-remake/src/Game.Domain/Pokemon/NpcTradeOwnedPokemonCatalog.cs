using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class NpcTradeOwnedPokemonCatalog
{
    public const string Profile = "firered-npc-trade-owned-pokemon-v1";
    public static SpeciesId MrMimeId { get; } = new("pret:SPECIES_MR_MIME");
    public static SpeciesId JynxId { get; } = new("pret:SPECIES_JYNX");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [MrMimeId, JynxId];
}
