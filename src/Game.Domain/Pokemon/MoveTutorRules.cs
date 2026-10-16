using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public enum MoveTutorEligibility { Able, Incompatible, AlreadyKnown, Egg }
public sealed record MoveTutorDefinition(int Index, MoveId Move, int SourceMoveId, int Pp,
    ImmutableDictionary<SpeciesId, bool> Compatibility);
public sealed record MoveTutorCompatibility(SpeciesId Species, ImmutableArray<int> Tutors);
public sealed record MoveTutorMove(int Index, MoveId Move, int SourceMoveId);
public sealed record MoveTutorResult(PartyPokemon Pokemon, string? Error, int LearnedSlot = -1)
{
    public bool Succeeded => Error is null;
}

/// <summary>Reusable teaching catalog bound to an authenticated owned catalog. Only the
/// bounded original tutor definitions are admitted; actor bindings separately enable field NPCs.</summary>
public sealed partial class MoveTutorCatalog
{
    public PokemonCatalog Pokemon { get; }
    public ImmutableDictionary<int, MoveTutorDefinition> Definitions { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    private MoveTutorCatalog(PokemonCatalog pokemon, ImmutableDictionary<int, MoveTutorDefinition> definitions, ImmutableDictionary<string, string> texts)
    { Pokemon = pokemon; Definitions = definitions; Texts = texts; }

    public static MoveTutorCatalog Authenticate(PokemonCatalog owned, ImmutableArray<MoveTutorMove> moves,
        ImmutableArray<MoveTutorCompatibility> compatibility, ImmutableDictionary<string, string> texts)
    {
        PokemonFactory.ValidateCatalog(owned);
        if (moves.IsDefault || moves.Length != 15 ||
            !moves.Select(m => m.Index).Order().SequenceEqual(Enumerable.Range(0, 15)) ||
            moves.Select(m => m.Move).Distinct().Count() != 15 || compatibility.IsDefaultOrEmpty ||
            compatibility.Select(c => c.Species).Distinct().Count() != compatibility.Length ||
            compatibility.Any(c => c.Tutors.IsDefault || c.Tutors.Any(i => i is < 0 or > 14) ||
                c.Tutors.Distinct().Count() != c.Tutors.Length))
            throw new ArgumentException("move_tutor_table_invalid");
        var rows = compatibility.ToImmutableDictionary(c => c.Species, c => c.Tutors);
        var definitions = ImmutableDictionary.CreateBuilder<int, MoveTutorDefinition>();
        foreach (var entry in moves)
        {
            if (!owned.Moves.TryGetValue(entry.Move, out var move) || move.SourceMoveId != entry.SourceMoveId ||
                !PokemonMoveRules.IsLearningSupported(owned, entry.Move))
                throw new ArgumentException("move_tutor_move_unsupported");
            var admitted = ImmutableDictionary.CreateBuilder<SpeciesId, bool>();
            foreach (var species in owned.Species.Keys)
            {
                if (!rows.TryGetValue(species, out var tutors)) throw new ArgumentException("move_tutor_species_source_missing");
                admitted.Add(species, tutors.Contains(entry.Index));
            }
            definitions.Add(entry.Index, new(entry.Index, entry.Move, move.SourceMoveId, move.Pp, admitted.ToImmutable()));
        }
        foreach (string symbol in new[] { "gText_TeachWhichPokemon", "gText_PkmnCantLearnMove", "gText_PkmnAlreadyKnows",
            "gText_PkmnNeedsToReplaceMove", "gText_StopLearningMove2", "gText_MoveNotLearned", "gText_WhichMoveToForget",
            "gText_12PoofForgotMove", "gText_PkmnLearnedMove3", "gText_PokeSum_HmMovesCantBeForgotten",
            "gText_Able2", "gText_NotAble", "gText_Learned" })
            if (!texts.TryGetValue(symbol, out var text) || string.IsNullOrEmpty(text))
                throw new ArgumentException("move_tutor_text_missing");
        return new(owned, definitions.ToImmutable(), texts);
    }
}

/// <summary>party_menu.c tutor semantics: compatibility precedes already-known, no item,
/// no friendship adjustment or RNG, and ordinary learning protects HM slots.</summary>
public static class MoveTutorRules
{
    public static MoveTutorEligibility Eligibility(MoveTutorDefinition tutor, PartyPokemon mon)
    {
        if (mon.IsEgg) return MoveTutorEligibility.Egg;
        if (!tutor.Compatibility.GetValueOrDefault(mon.Pokemon.Species)) return MoveTutorEligibility.Incompatible;
        return mon.Pokemon.Moves.Any(m => m.Move == tutor.Move) ? MoveTutorEligibility.AlreadyKnown : MoveTutorEligibility.Able;
    }

    public static string? ValidateSelection(ImmutableArray<PartyPokemon> party, int index, long? instanceId)
    {
        if (party.IsDefault || party.Length is < 1 or > 6 || party.Any(p => p is null || p.InstanceId <= 0) ||
            party.Select(p => p.InstanceId).Distinct().Count() != party.Length) return "move_tutor_party_shape";
        if (index < 0 || index >= party.Length) return "move_tutor_party_index";
        return instanceId is null || party[index].InstanceId != instanceId ? "move_tutor_target_mismatch" : null;
    }

    public static MoveTutorResult Teach(MoveTutorCatalog catalog, PokemonCatalog owned, PartyPokemon mon, int tutorIndex, int? replacement = null)
    {
        MoveTutorResult Reject(string error) => new(mon, error);
        if (!ReferenceEquals(catalog.Pokemon, owned)) return Reject("move_tutor_pokemon_catalog");
        if (!catalog.Definitions.TryGetValue(tutorIndex, out var tutor)) return Reject("move_tutor_unsupported");
        string? error = PokemonStateValidator.Validate(owned, mon.Pokemon);
        if (error is not null) return Reject(error);
        error = Eligibility(tutor, mon) switch { MoveTutorEligibility.Egg => "move_tutor_egg", MoveTutorEligibility.Incompatible => "move_tutor_incompatible",
            MoveTutorEligibility.AlreadyKnown => "move_tutor_already_known", _ => null };
        if (error is not null) return Reject(error);
        var before = mon.Pokemon.Moves;
        if (before.Length < 4)
        {
            if (replacement is not null) return Reject("move_tutor_invalid_slot");
            var result = PokemonMoveRules.TryLearn(owned, before, tutor.Move);
            if (result.Outcome != MoveLearningOutcome.Added) return Reject("move_tutor_move_unsupported");
            return new(mon with { Pokemon = mon.Pokemon with { Moves = result.Moves } }, null, before.Length);
        }
        if (replacement is null) return Reject("move_tutor_replacement_required");
        if (replacement is < 0 or > 3) return Reject("move_tutor_invalid_slot");
        if (PokemonMoveRules.IsHiddenMachineMove(before[replacement.Value].Move)) return Reject("move_tutor_cannot_forget_hm");
        return new(mon with { Pokemon = mon.Pokemon with { Moves = PokemonMoveRules.Replace(owned, before, replacement.Value, tutor.Move) } }, null, replacement.Value);
    }
}
