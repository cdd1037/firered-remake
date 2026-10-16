using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>
/// External ordinary-capture facts, supplied by the authoritative world/Pokédex owner.
/// CaughtSpecies uses canonical species identities, not internal species IDs mistaken for National
/// Dex numbers. The caller maps its caught flags to species. IsUnderwater is the source map type;
/// battle terrain, fishing and a battler using Dive do not imply this flag.
/// </summary>
public sealed record WildBattleCaptureContext(bool IsUnderwater, ImmutableHashSet<SpeciesId> CaughtSpecies,
    CaptureEncounterKind Encounter = CaptureEncounterKind.OrdinaryWild);

/// <summary>
/// A capture does not generate a replacement Pokémon. These delivery values are the exact persistent
/// enemy party member after the successful throw, including its HP, PP, identity, status and held item.
/// The caller remains responsible for inventory, capture metadata, Pokédex and party/PC delivery.
/// </summary>
public sealed record WildBattleCaptureActionResult(WildBattleResult Battle, PokemonInstance? CapturedPokemon,
    uint? CapturedStatus, string? CapturedHeldItem)
{
    public bool Succeeded => Battle.Succeeded;
}
