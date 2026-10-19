using Game.Application;
using Game.Content;

/// <summary>Two-line notices fit the existing dialogue box; exception text stays in diagnostics.</summary>
internal static class CampaignSaveFeedback
{
    private static readonly PresentationTextCatalog Text = PresentationTextCatalog.English("save");

    public static string Message(SaveOperationResult result, string playerName = "PLAYER")
    {
        string action = Text.Get(result.Operation == SaveOperation.Save ? "action.save" : "action.load");
        if (result.Succeeded)
            return result.Operation == SaveOperation.Save ? Text.Get("success.save", ("PlayerName", playerName))
                : Text.Get(result.FromBackup ? "success.backup" : "success.load");
        if (result.Status == SaveOperationStatus.OverwriteRequired) return Text.Get("overwrite");
        if (result.Status == SaveOperationStatus.Blocked) return Text.Get("blocked", ("Action", action));
        if (result.Status == SaveOperationStatus.Missing) return Text.Get("missing");
        string reason = result.Status switch
        {
            SaveOperationStatus.AccessDenied => Text.Get("error.access", ("Action", action)),
            SaveOperationStatus.StorageFailure => Text.Get("error.storage", ("Action", action)),
            SaveOperationStatus.MigrationRequired => Text.Get("error.migration"),
            SaveOperationStatus.IncompatibleVersion => Text.Get("error.version", ("Action", action)),
            SaveOperationStatus.TooLarge => Text.Get("error.size", ("Action", action)),
            SaveOperationStatus.FilesChanged => Text.Get("error.changed"),
            _ => Text.Get("error.damaged", ("Action", action))
        };
        string recovery = result.Files == SaveFileOutcome.Unconfirmed ? "recovery.unconfirmed"
            : result.Status == SaveOperationStatus.StorageFailure ? "recovery.storage"
            : result.Status == SaveOperationStatus.AccessDenied ? "recovery.access"
            : result.Status == SaveOperationStatus.MigrationRequired ? "recovery.migration"
            : result.Status == SaveOperationStatus.IncompatibleVersion ? "recovery.version"
            : result.Status == SaveOperationStatus.FilesChanged ? "recovery.changed" : "recovery.backup";
        return reason + "\n" + Text.Get(recovery);
    }
}
