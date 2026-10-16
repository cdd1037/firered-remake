using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

/// <summary>The three ordinary browse pages; move-detail selection has its own input mode.</summary>
public enum PokemonSummaryPage { Info, Skills, Moves }

public enum PokemonSummaryMoveOutcome { Rejected, Unchanged, Swapped }

/// <summary>Rejection and same-slot selection retain the exact input Pokemon reference.</summary>
public sealed record PokemonSummaryMoveResult(PokemonSummaryMoveOutcome Outcome,
    PartyPokemon? Pokemon, string? Error = null)
{
    public bool Succeeded => Outcome != PokemonSummaryMoveOutcome.Rejected;
}

/// <summary>Owned, single-player Summary rules from pret 037335f. Callers retain the ownership
/// container and enforce modal/instance/revision guards; these rules create no session or history.</summary>
public static class PokemonSummaryRules
{
    /// <summary>Swap occupied slots as complete values, including current/max PP and PP Ups.
    /// The original selection cursor skips MOVE_NONE; compact slots never gain empty holes.</summary>
    public static PokemonSummaryMoveResult SwapMoveSlots(CampaignPokemonContent? content,
        PartyPokemon? pokemon, int firstSlot, int secondSlot)
    {
        if (PokemonStateValidator.ValidateParty(content, pokemon) is { } invalid)
            return new(PokemonSummaryMoveOutcome.Rejected, pokemon, invalid);
        if (pokemon!.IsEgg)
            return new(PokemonSummaryMoveOutcome.Rejected, pokemon, "summary_egg");
        var moves = pokemon.Pokemon.Moves;
        if (firstSlot < 0 || firstSlot >= moves.Length || secondSlot < 0 || secondSlot >= moves.Length)
            return new(PokemonSummaryMoveOutcome.Rejected, pokemon, "summary_move_slot");
        if (firstSlot == secondSlot) return new(PokemonSummaryMoveOutcome.Unchanged, pokemon);
        return new(PokemonSummaryMoveOutcome.Swapped, pokemon with
        {
            Pokemon = pokemon.Pokemon with
            {
                Moves = moves.SetItem(firstSlot, moves[secondSlot]).SetItem(secondSlot, moves[firstSlot])
            }
        });
    }

    /// <summary>Seek within an already validated compact party, without wrapping. Info includes
    /// Eggs; Skills/Moves skip them. A held creature is a singleton party and has no next index.
    /// Invalid shape/page/direction/current selection and no candidate all return null.</summary>
    public static int? SeekPartyIndex(ImmutableArray<PartyPokemon> party, int currentIndex,
        PokemonSummaryPage page, int direction)
    {
        if (party.IsDefaultOrEmpty || party.Length > 6 || party.Any(p => p is null) ||
            currentIndex < 0 || currentIndex >= party.Length || !ValidInput(page, direction) ||
            !Visible(party[currentIndex], page)) return null;
        for (int index = currentIndex + direction; index >= 0 && index < party.Length; index += direction)
            if (Visible(party[index], page)) return index;
        return null;
    }

    /// <summary>Seek in one already validated fixed box, skipping empty slots and never crossing
    /// its edges. Info includes Eggs; Skills/Moves skip them. Returns the original box slot index.</summary>
    public static int? SeekBoxSlot(PokemonStorageBox? box, int currentIndex,
        PokemonSummaryPage page, int direction)
    {
        if (box is null || box.Slots.IsDefault || box.Slots.Length != PokemonStorageState.SlotsPerBox ||
            currentIndex < 0 || currentIndex >= box.Slots.Length || !ValidInput(page, direction) ||
            !Visible(box.Slots[currentIndex], page)) return null;
        for (int index = currentIndex + direction; index >= 0 && index < box.Slots.Length; index += direction)
            if (Visible(box.Slots[index], page)) return index;
        return null;
    }

    private static bool ValidInput(PokemonSummaryPage page, int direction) =>
        page is PokemonSummaryPage.Info or PokemonSummaryPage.Skills or PokemonSummaryPage.Moves &&
        direction is -1 or 1;

    private static bool Visible(PartyPokemon? pokemon, PokemonSummaryPage page) =>
        pokemon is not null && (page == PokemonSummaryPage.Info || !pokemon.IsEgg);
}
