using Game.Application;
using Game.Content;

/// <summary>Two-line notices fit the existing dialogue box; exception text stays in diagnostics.</summary>
internal static class CampaignSaveFeedback
{
    public static string Message(PresentationTextCatalog text, SaveOperationResult result, string playerName = "PLAYER")
    {
        string action = text.Get(result.Operation == SaveOperation.Save ? "action.save" : "action.load");
        if (result.Succeeded)
            return result.Operation == SaveOperation.Save ? text.Get("success.save", ("PlayerName", playerName))
                : text.Get(result.FromBackup ? "success.backup" : "success.load");
        if (result.Status == SaveOperationStatus.OverwriteRequired) return text.Get("overwrite");
        if (result.Status == SaveOperationStatus.Blocked) return text.Get("blocked", ("Action", action));
        if (result.Status == SaveOperationStatus.Missing) return text.Get("missing");
        string reason = result.Status switch
        {
            SaveOperationStatus.AccessDenied => text.Get("error.access", ("Action", action)),
            SaveOperationStatus.StorageFailure => text.Get("error.storage", ("Action", action)),
            SaveOperationStatus.MigrationRequired => text.Get("error.migration"),
            SaveOperationStatus.IncompatibleVersion => text.Get("error.version", ("Action", action)),
            SaveOperationStatus.TooLarge => text.Get("error.size", ("Action", action)),
            SaveOperationStatus.FilesChanged => text.Get("error.changed"),
            _ => text.Get("error.damaged", ("Action", action))
        };
        string recovery = result.Files == SaveFileOutcome.Unconfirmed ? "recovery.unconfirmed"
            : result.Status == SaveOperationStatus.StorageFailure ? "recovery.storage"
            : result.Status == SaveOperationStatus.AccessDenied ? "recovery.access"
            : result.Status == SaveOperationStatus.MigrationRequired ? "recovery.migration"
            : result.Status == SaveOperationStatus.IncompatibleVersion ? "recovery.version"
            : result.Status == SaveOperationStatus.FilesChanged ? "recovery.changed" : "recovery.backup";
        return reason + "\n" + text.Get(recovery);
    }
}
