using Game.Content;

internal enum StorageMoveHelpKind { Menu, Empty, Cancel }

/// <summary>Physical cursor/held state selects the hint; source menu labels never identify slots.</summary>
internal static class StorageMoveHelpPresentation
{
    internal static StorageMoveHelpKind SelectSlot(bool partyPanelOpen, int partyCursor, bool holding, bool occupied)
        => partyPanelOpen && partyCursor == 6 ? StorageMoveHelpKind.Cancel
            : !holding && !occupied ? StorageMoveHelpKind.Empty : StorageMoveHelpKind.Menu;

    internal static string Text(StorageMoveHelpKind kind, PresentationTextCatalog text, string cancelLabel) => kind switch
    {
        StorageMoveHelpKind.Menu => text.Get("help.enter-menu"),
        StorageMoveHelpKind.Empty => text.Get("help.enter-empty"),
        StorageMoveHelpKind.Cancel => text.Get("help.enter", ("action", cancelLabel)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
