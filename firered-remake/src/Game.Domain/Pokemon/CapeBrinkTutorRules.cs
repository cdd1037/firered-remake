using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public sealed record CapeBrinkTutorDefinition(int Index, SpeciesId Species, MoveId Move, string Flag);
public sealed record CapeBrinkTutorOffer(int LeadIndex, long InstanceId, CapeBrinkTutorDefinition? Tutor,
    int MovesKnown, string? Error)
{
    public bool Allowed => Error is null;
}

/// <summary>Authenticated original Cape Brink special semantics; actual script flags remain owned by the campaign.</summary>
public sealed class CapeBrinkTutorCatalog
{
    public const string AllMovesFlag = "FLAG_LEARNED_ALL_MOVES_AT_CAPE_BRINK";
    public const string TemporaryFlag = "FLAG_TEMP_2";
    public PokemonCatalog Pokemon { get; }
    public ImmutableArray<CapeBrinkTutorDefinition> Definitions { get; }
    private CapeBrinkTutorCatalog(PokemonCatalog pokemon)
    {
        Pokemon = pokemon;
        Definitions = [new(15, new("pret:SPECIES_VENUSAUR"), new("pret:MOVE_FRENZY_PLANT"), "FLAG_TUTOR_FRENZY_PLANT"),
            new(16, new("pret:SPECIES_CHARIZARD"), new("pret:MOVE_BLAST_BURN"), "FLAG_TUTOR_BLAST_BURN"),
            new(17, new("pret:SPECIES_BLASTOISE"), new("pret:MOVE_HYDRO_CANNON"), "FLAG_TUTOR_HYDRO_CANNON")];
    }
    public static CapeBrinkTutorCatalog Authenticate(PokemonCatalog owned)
    {
        PokemonFactory.ValidateCatalog(owned);
        var result = new CapeBrinkTutorCatalog(owned);
        foreach (var tutor in result.Definitions)
            if (!owned.Species.ContainsKey(tutor.Species) || !owned.Moves.TryGetValue(tutor.Move, out var move) ||
                move.Pp != 5 || !PokemonMoveRules.IsLearningSupported(owned, tutor.Move))
                throw new ArgumentException("cape_brink_owned_move_scope");
        return result;
    }
}

public static class CapeBrinkTutorRules
{
    /// <summary>The source skips eggs, never fainted Pokémon. A party containing only eggs yields index 0.</summary>
    public static int LeadIndex(ImmutableArray<PartyPokemon> party)
    {
        if (party.IsDefaultOrEmpty || party.Length > 6 || party.Any(p => p is null || p.InstanceId <= 0) ||
            party.Select(p => p.InstanceId).Distinct().Count() != party.Length) throw new ArgumentException("cape_brink_party_shape");
        for (int i = 0; i < party.Length; i++) if (!party[i].IsEgg) return i;
        return 0;
    }

    /// <summary>Only the native offer special: it intentionally does not reject already-known moves.
    /// The source script owns the prior all-moves and temporary-flag dialogue guards.</summary>
    public static CapeBrinkTutorOffer Offer(CapeBrinkTutorCatalog catalog, ImmutableArray<PartyPokemon> party,
        IReadOnlySet<string> flags)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(flags);
        int index = LeadIndex(party); var mon = party[index];
        var tutor = mon.IsEgg ? null : catalog.Definitions.SingleOrDefault(t => t.Species == mon.Pokemon.Species);
        if (tutor is null || mon.Pokemon.Friendship != 255) return new(index, mon.InstanceId, null, 0, "cape_brink_ineligible_lead");
        if (flags.Contains(tutor.Flag)) return new(index, mon.InstanceId, tutor, 0, "cape_brink_move_already_taught");
        return new(index, mon.InstanceId, tutor, mon.Pokemon.Moves.Length, null);
    }

    /// <summary>Pure projection of the native post-success flag special. Caller commits this with the learned move.</summary>
    public static ImmutableHashSet<string> FlagsAfterSuccess(CapeBrinkTutorCatalog catalog, IReadOnlySet<string> flags, int tutorIndex)
    {
        var tutor = catalog.Definitions.SingleOrDefault(t => t.Index == tutorIndex)
            ?? throw new ArgumentException("cape_brink_tutor_index");
        return flags.ToImmutableHashSet(StringComparer.Ordinal).Add(tutor.Flag);
    }
    public static bool AllMovesTaught(CapeBrinkTutorCatalog catalog, IReadOnlySet<string> flags) =>
        catalog.Definitions.All(t => flags.Contains(t.Flag));
}

/// <summary>Extend the authenticated ordinary tutor catalog with the three source Cape Brink definitions.
/// Every teaching mutation still goes through the existing MoveTutorRules.Teach.</summary>
public sealed partial class MoveTutorCatalog
{
    public CapeBrinkTutorCatalog? CapeBrink { get; private init; }
    public MoveTutorCatalog WithCapeBrink(CapeBrinkTutorCatalog cape)
    {
        ArgumentNullException.ThrowIfNull(cape);
        if (!ReferenceEquals(Pokemon, cape.Pokemon) || Definitions.Count != 15 ||
            !Definitions.Keys.ToHashSet().SetEquals(Enumerable.Range(0, 15)))
            throw new ArgumentException("cape_brink_tutor_predecessor");
        var definitions = Definitions.ToBuilder();
        foreach (var tutor in cape.Definitions)
        {
            var source = Pokemon.Moves[tutor.Move];
            definitions.Add(tutor.Index, new(tutor.Index, tutor.Move, source.SourceMoveId, source.Pp,
                Pokemon.Species.Keys.ToImmutableDictionary(id => id, id => id == tutor.Species)));
        }
        return new(Pokemon, definitions.ToImmutable(), Texts) { CapeBrink = cape };
    }
}
