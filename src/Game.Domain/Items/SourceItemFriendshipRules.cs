namespace Game.Domain.Items;

/// <summary>pokemon.c UPDATE_FRIENDSHIP_FROM_ITEM: scale the base change first, then add
/// Luxury Ball and current map-section bonuses. The effect owner calls this only after success.</summary>
internal static class SourceItemFriendshipRules
{
    internal static int Apply(int friendship, int delta, string heldItem, SourceItemFriendshipContext? context)
    {
        int adjustment = delta;
        if (delta > 0)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (heldItem == "ITEM_SOOTHE_BELL") adjustment = 150 * delta / 100;
            if (context.Ball == "ITEM_LUXURY_BALL") adjustment++;
            if (context.MetLocation == context.CurrentMapSection) adjustment++;
        }
        return Math.Clamp(friendship + adjustment, 0, 255);
    }
}
