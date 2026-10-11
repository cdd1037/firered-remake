using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// Immutable source item identities from the pinned items.json and constants/items.h.
// The source contract test authenticates their complete item records against Git blobs;
// admission is bounded by CanonicalSourceSingles, independent of campaign inventory.
internal sealed record SourceHeldOrderEffect(int SourceItemId, string HoldEffect, int Parameter);
internal static class SourceBattleHeldItems
{
    internal static readonly SourceHeldOrderEffect FocusBand = new(196, "HOLD_EFFECT_FOCUS_BAND", 10);
    internal static readonly SourceHeldOrderEffect ChoiceBand = new(186, "HOLD_EFFECT_CHOICE_BAND", 0);

    // pokemon.c CalculateBaseDamage stores Attack in u16 before subsequent modifiers.
    internal static int PhysicalAttack(int attack, string item) => item == "ITEM_CHOICE_BAND"
        ? unchecked((ushort)(150 * unchecked((ushort)attack) / 100)) : attack;

    internal static bool ChoiceAllows(WildBattleCombatant mon, MoveId move) =>
        mon.HeldItem != "ITEM_CHOICE_BAND" || mon.SourceChoiceMove is null ||
        mon.SourceChoiceMove.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" || mon.SourceChoiceMove == move;

    internal static WildBattleCombatant ReconcileChoice(WildBattleCombatant mon) =>
        mon.SourceChoiceMove is { } chosen && !mon.Pokemon.Moves.Any(m => m.Move == chosen)
            ? mon with { SourceChoiceMove = null } : mon;

    // MOVEEND_CHOICE_MOVE precedes changed-items and Snatch register restoration.
    internal static WildBattleCombatant FinishChoice(WildBattleCombatant mon, MoveId chosen, bool obeys, bool failed)
    {
        if (obeys && mon.HeldItem == "ITEM_CHOICE_BAND" && chosen != WildBattleRules.StruggleMove &&
            (mon.SourceChoiceMove is null || mon.SourceChoiceMove.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE"))
        {
            if (chosen.Value == "pret:MOVE_BATON_PASS" && !failed) return mon;
            mon = mon with { SourceChoiceMove = chosen };
        }
        return ReconcileChoice(mon);
    }

    private static readonly SourceHeldOrderEffect MachoBrace = new(181, "HOLD_EFFECT_MACHO_BRACE", 0);
    private static readonly SourceHeldOrderEffect QuickClaw = new(183, "HOLD_EFFECT_QUICK_CLAW", 20);
    internal static SourceHeldOrderEffect? OrderEffect(string item) => item switch
    {
        "ITEM_MACHO_BRACE" => MachoBrace,
        "ITEM_QUICK_CLAW" => QuickClaw,
        _ => null
    };
}
