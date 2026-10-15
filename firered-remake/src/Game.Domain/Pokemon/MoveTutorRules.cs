using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public enum MoveTutorEligibility { Able, Incompatible, AlreadyKnown, Egg }
public sealed record MoveTutorDefinition(int Index, MoveId Move, int SourceMoveId, int Pp,
    ImmutableDictionary<SpeciesId, bool> Compatibility);
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

    public static MoveTutorCatalog Authenticate(PokemonCatalog owned, ReadOnlySpan<byte> learnsets, ReadOnlySpan<byte> strings, ReadOnlySpan<byte> moveConstants)
    {
        if (OwnedBattlePokemonCatalog.For(owned) is null || owned.SourceCommit != PokemonFactory.SourceCommit)
            throw new ArgumentException("move_tutor_source_authentication");
        var source = Encoding.UTF8.GetString(learnsets);
        var rows = Regex.Matches(source, @"\[(SPECIES_[A-Z0-9_]+)\]\s*=\s*([^,]+),", RegexOptions.CultureInvariant)
            .ToDictionary(m => "pret:" + m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
        var constants = Regex.Matches(Encoding.UTF8.GetString(moveConstants), @"(?m)^#define (MOVE_[A-Z0-9_]+|MOVETUTOR_[A-Z0-9_]+)\s+(\d+)\s*$", RegexOptions.CultureInvariant)
            .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
        var definitions = ImmutableDictionary.CreateBuilder<int, MoveTutorDefinition>();
        // Both the tutor index and move identity come from the original table/constants.
        // This bounded admission list does not add any map or Pokémon species.
        foreach (Match entry in Regex.Matches(source, @"\[TUTOR_(MOVE_[A-Z0-9_]+)\]\s*=\s*(MOVE_[A-Z0-9_]+),", RegexOptions.CultureInvariant))
        {
            string symbol = entry.Groups[1].Value;
            if (symbol != entry.Groups[2].Value || !constants.TryGetValue("MOVETUTOR_" + symbol[5..], out int index))
                throw new ArgumentException("move_tutor_table_source_invalid");
            if (index is < 0 or > 14) throw new ArgumentException("move_tutor_index_invalid");
            var move = new MoveId("pret:" + symbol);
            if (!owned.Moves.TryGetValue(move, out var definition) || definition.SourceMoveId != constants[symbol] ||
                !PokemonMoveRules.IsLearningSupported(owned, move)) throw new ArgumentException("move_tutor_move_unsupported");
            var compatibility = ImmutableDictionary.CreateBuilder<SpeciesId, bool>();
            foreach (var species in owned.Species.Values)
            {
                if (!rows.TryGetValue(species.Id.Value, out var expression)) throw new ArgumentException("move_tutor_species_source_missing");
                compatibility.Add(species.Id, expression.Contains("TUTOR(" + symbol + ")", StringComparison.Ordinal));
            }
            definitions.Add(index, new(index, move, definition.SourceMoveId, definition.Pp, compatibility.ToImmutable()));
        }
        if (definitions.Count != 15) throw new ArgumentException("move_tutor_table_source_missing");
        var textSource = Encoding.UTF8.GetString(strings);
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (string symbol in new[] { "gText_TeachWhichPokemon", "gText_PkmnCantLearnMove", "gText_PkmnAlreadyKnows",
            "gText_PkmnNeedsToReplaceMove", "gText_StopLearningMove2", "gText_MoveNotLearned", "gText_WhichMoveToForget",
            "gText_12PoofForgotMove", "gText_PkmnLearnedMove3", "gText_PokeSum_HmMovesCantBeForgotten",
            "gText_Able2", "gText_NotAble", "gText_Learned" })
        {
            var match = Regex.Match(textSource, "const u8 " + symbol + "\\[\\] = _\\(\"([^\"]*)\"\\);", RegexOptions.CultureInvariant);
            if (!match.Success) throw new ArgumentException("move_tutor_text_missing");
            texts.Add(symbol, match.Groups[1].Value);
        }
        return new(owned, definitions.ToImmutable(), texts.ToImmutable());
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
        if (!ReferenceEquals(catalog.Pokemon, owned) || OwnedBattlePokemonCatalog.For(owned) is null) return Reject("move_tutor_pokemon_catalog");
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
