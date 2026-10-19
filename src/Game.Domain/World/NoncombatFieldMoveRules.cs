using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

public sealed record FieldHealingResult(ImmutableArray<PartyPokemon> Party, int DonatedHp, int RestoredHp, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>Party-menu field actions. Source field use neither checks nor consumes move PP.</summary>
public static class NoncombatFieldMoveRules
{
    public const string Dig = "pret:MOVE_DIG", Teleport = "pret:MOVE_TELEPORT", SweetScent = "pret:MOVE_SWEET_SCENT",
        SoftBoiled = "pret:MOVE_SOFT_BOILED", MilkDrink = "pret:MOVE_MILK_DRINK";
    public static bool IsSupported(string move) => move is Dig or Teleport or SweetScent or SoftBoiled or MilkDrink;
    public static bool IsHealing(string move) => move is SoftBoiled or MilkDrink;
    public static bool Knows(PartyPokemon member, string move) =>
        IsSupported(move) && !member.IsEgg && member.Pokemon.Moves.Any(slot => slot.Move.Value == move);
    public static bool CanDonate(PartyPokemon member, string move) => IsHealing(move) && Knows(member, move) &&
        member.Pokemon.CurrentHp > member.Pokemon.Stats.MaxHp / 5;

    public static FieldHealingResult Heal(ImmutableArray<PartyPokemon> party, int donor, int recipient, string move)
    {
        FieldHealingResult No(string error) => new(party, 0, 0, error);
        if (!IsHealing(move)) return No("field_heal_move_invalid");
        if (party.IsDefaultOrEmpty || donor < 0 || donor >= party.Length || recipient < 0 || recipient >= party.Length)
            return No("field_heal_party_index_invalid");
        var source = party[donor]; var target = party[recipient];
        if (!Knows(source, move)) return No("field_heal_move_missing");
        if (!CanDonate(source, move)) return No("field_heal_not_enough_hp");
        if (donor == recipient || target.IsEgg || target.Pokemon.CurrentHp == 0 || target.Pokemon.CurrentHp >= target.Pokemon.Stats.MaxHp)
            return No("field_heal_target_invalid");
        int donated = source.Pokemon.Stats.MaxHp / 5;
        int restored = Math.Min(donated, target.Pokemon.Stats.MaxHp - target.Pokemon.CurrentHp);
        return new(party.SetItem(donor, source with { Pokemon = source.Pokemon with { CurrentHp = source.Pokemon.CurrentHp - donated } })
            .SetItem(recipient, target with { Pokemon = target.Pokemon with { CurrentHp = target.Pokemon.CurrentHp + restored } }), donated, restored, null);
    }
}
