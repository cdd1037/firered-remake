namespace Game.Domain.Battle;

// Immutable source item identities from the pinned items.json and constants/items.h.
// The source contract test authenticates their complete item records against Git blobs;
// admission is bounded by CanonicalSourceSingles, independent of campaign inventory.
internal sealed record SourceHeldOrderEffect(int SourceItemId, string HoldEffect, int Parameter);
internal static class SourceBattleHeldItems
{
    private static readonly SourceHeldOrderEffect MachoBrace = new(181, "HOLD_EFFECT_MACHO_BRACE", 0);
    private static readonly SourceHeldOrderEffect QuickClaw = new(183, "HOLD_EFFECT_QUICK_CLAW", 20);
    internal static SourceHeldOrderEffect? OrderEffect(string item) => item switch
    {
        "ITEM_MACHO_BRACE" => MachoBrace,
        "ITEM_QUICK_CLAW" => QuickClaw,
        _ => null
    };
}
