using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The original Fuchsia service, bound to one authenticated owned-Pokemon catalog.</summary>
public sealed class MoveDeleterCatalog
{
    public const string SpecialSourceSha256 = "72faa4aa41f1b8f4798a509250fe2180511809d429a47f2439d94520ce5561e5";
    public const string SummarySourceSha256 = "855a17fb555f5497dbb47f90a8c1b8cf620532d3bff96e8196d0790515508583";
    public const string ScriptSha256 = "2c0bc780afd36a7d9ac791176fe6c6dfaa5c03e691cdb1b95e56ac47dbb1ba9f";
    public const string StringsSha256 = "08e3799f20dd90ae937808be25721d28c3465b4f2ebaba2e71321e52f72465c1";
    public PokemonCatalog Pokemon { get; }
    public ImmutableDictionary<string, string> Texts { get; }

    private MoveDeleterCatalog(PokemonCatalog pokemon, ImmutableDictionary<string, string> texts)
    { Pokemon = pokemon; Texts = texts; }

    public static MoveDeleterCatalog Authenticate(PokemonCatalog owned, ReadOnlySpan<byte> specialSource,
        ReadOnlySpan<byte> summarySource, ReadOnlySpan<byte> script, ReadOnlySpan<byte> strings)
    {
        if (OwnedBattlePokemonCatalog.For(owned) is null || owned.SourceCommit != PokemonFactory.SourceCommit ||
            Convert.ToHexStringLower(SHA256.HashData(specialSource)) != SpecialSourceSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(summarySource)) != SummarySourceSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(script)) != ScriptSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(strings)) != StringsSha256)
            throw new ArgumentException("move_deleter_source_authentication");
        var text = new UTF8Encoding(false, true).GetString(strings);
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (string symbol in new[] { "gText_ChoosePokemon", "gText_PokeSum_PageName_KnownMoves",
            "gText_PokeSum_Controls_PickDelete", "gFameCheckerText_Cancel" })
        {
            var match = Regex.Match(text, "const u8 " + symbol + "\\[\\] = _\\(\"([^\"]*)\"\\);", RegexOptions.CultureInvariant);
            if (!match.Success) throw new ArgumentException("move_deleter_text_missing: " + symbol);
            texts.Add(symbol, match.Groups[1].Value);
        }
        return new(owned, texts.ToImmutable());
    }
}

public sealed record MoveDeleterResult(PartyPokemon Pokemon, MoveId? ForgottenMove, int DeletedSlot, string? Error = null)
{
    public bool Succeeded => Error is null;
}

public static class MoveDeleterRules
{
    /// <summary>Original script refuses eggs and the last move. Fainting, status, HM moves and OT do not bar deletion.</summary>
    public static string? ValidateSelection(MoveDeleterCatalog catalog, PartyPokemon mon)
    {
        if (OwnedBattlePokemonCatalog.For(catalog.Pokemon) is null) return "move_deleter_pokemon_catalog";
        if (mon.IsEgg) return "move_deleter_egg";
        // A zero-move or otherwise malformed state is not a normal script refusal; reject the invalid state.
        if (PokemonStateValidator.Validate(catalog.Pokemon, mon.Pokemon) is { } error) return error;
        return mon.Pokemon.Moves.Length == 1 ? "move_deleter_only_move" : null;
    }

    /// <summary>Called only at the confirmed MoveDeleterForgetMove special. Cancellation never calls this.
    /// Remove the selected slot and shift remaining move/PP/PP-Up tuples together, without healing or charging.</summary>
    public static MoveDeleterResult Forget(MoveDeleterCatalog catalog, PokemonCatalog owned, PartyPokemon mon, int slot)
    {
        MoveDeleterResult Reject(string error) => new(mon, null, -1, error);
        if (!ReferenceEquals(catalog.Pokemon, owned)) return Reject("move_deleter_pokemon_catalog");
        if (ValidateSelection(catalog, mon) is { } error) return Reject(error);
        if (slot < 0 || slot >= mon.Pokemon.Moves.Length) return Reject("move_deleter_invalid_slot");
        var previous = mon.Pokemon.Moves;
        // Ordinary Replace deliberately forbids HM deletion and teaches a replacement. This service does neither.
        var next = mon with { Pokemon = mon.Pokemon with { Moves = previous.RemoveAt(slot) } };
        return new(next, previous[slot].Move, slot);
    }
}
