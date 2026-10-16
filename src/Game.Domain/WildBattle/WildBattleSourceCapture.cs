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
