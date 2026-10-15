using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The original Fuchsia service, bound to one authenticated owned-Pokemon catalog.</summary>
public sealed class MoveDeleterCatalog
{
    public PokemonCatalog Pokemon { get; }
    public ImmutableDictionary<string, string> Texts { get; }

    private MoveDeleterCatalog(PokemonCatalog pokemon, ImmutableDictionary<string, string> texts)
    { Pokemon = pokemon; Texts = texts; }

    public static MoveDeleterCatalog Authenticate(PokemonCatalog owned, ImmutableDictionary<string, string> texts)
    {
        if (OwnedBattlePokemonCatalog.For(owned) is null || owned.SourceCommit != PokemonFactory.SourceCommit)
            throw new ArgumentException("move_deleter_source_authentication");
        foreach (string symbol in new[] { "gText_ChoosePokemon", "gText_PokeSum_PageName_KnownMoves",
            "gText_PokeSum_Controls_PickDelete", "gFameCheckerText_Cancel" })
        {
            if (!texts.TryGetValue(symbol, out var text) || string.IsNullOrEmpty(text))
                throw new ArgumentException("move_deleter_text_missing: " + symbol);
        }
        return new(owned, texts);
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
