namespace Game.Domain.Pokemon;

public static class KantoPokedexRules
{
    /// <summary>HasAllKantoMons checks caught National Dex flags 1 through 150 inclusive.
    /// Mew, seen flags, and caught entries from other regions cannot replace a missing entry.</summary>
    public static bool HasAllKantoMons(IEnumerable<int> caughtNationalDexIds)
    {
        ArgumentNullException.ThrowIfNull(caughtNationalDexIds);
        var caught = caughtNationalDexIds.ToHashSet();
        for (int dex = 1; dex <= 150; dex++)
            if (!caught.Contains(dex)) return false;
        return true;
    }
}
