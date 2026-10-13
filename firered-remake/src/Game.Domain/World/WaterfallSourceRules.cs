using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

public static class WaterfallSourceRules
{
    public const string Move = "pret:MOVE_WATERFALL", Badge = "FLAG_BADGE07_GET";
    public const int NoPartyMember = 6, FieldUsePpCost = 0;
    public static bool Knows(PartyPokemon? member) => member is { IsEgg: false, Pokemon: not null } &&
        !member.Pokemon.Moves.IsDefault && member.Pokemon.Moves.Any(m => m is not null && m.Move.Value == Move);
    public static int FirstPartyMember(ImmutableArray<PartyPokemon> party)
    {
        if (party.IsDefault || party.Length > NoPartyMember) throw new ArgumentException("Invalid party.", nameof(party));
        for (int i = 0; i < party.Length; i++) if (Knows(party[i])) return i;
        return NoPartyMember;
    }
    // IsPlayerSurfingNorth checks movementDirection, not the displayed facing alone.
    public static bool CanUse(PartyPokemon? member, bool hasVolcanoBadge, bool surfing, Direction movementDirection, int facingBehavior) =>
        Knows(member) && hasVolcanoBadge && surfing && movementDirection == Direction.North && facingBehavior == FourIslandFieldSource.Waterfall;
}
