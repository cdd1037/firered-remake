using Game.Content;

/// <summary>Read-only cry selection and the detail footer's existing keyboard/mouse affordances.</summary>
internal static class PokedexDetailPresentation
{
    internal enum Action { None, Previous, Next, Cry, Back }
    internal readonly record struct FooterButton(Action Action, int X, int Width, string Label);
    internal static IReadOnlyList<FooterButton> Footer { get; } = Array.AsReadOnly(new[]
    {
        new FooterButton(Action.Previous, 0, 48, "PREV"),
        new FooterButton(Action.Next, 48, 48, "NEXT"),
        new FooterButton(Action.Cry, 96, 80, "Tab CRY"),
        new FooterButton(Action.Back, 176, 64, "BACK")
    });

    // A seen, uncaught entry may cry. Keep the source species identity; National numbers
    // diverge from it after Johto and must never be used as an audio catalog key.
    internal static int? CrySpecies(PokedexMenuList list, PokedexMenuCursor cursor)
    {
        int index = cursor.SelectedIndex;
        return index >= 0 && index < list.Rows.Length && list.Rows[index].CanOpen
            ? list.Rows[index].SourceSpeciesId : null;
    }

    internal static Action HitTest(double x, double y)
    {
        if (y < 144 || y >= 160) return Action.None;
        foreach (var button in Footer)
            if (x >= button.X && x < button.X + button.Width) return button.Action;
        return Action.None;
    }
}
