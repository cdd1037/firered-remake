using Game.Application;

/// <summary>Two-line notices fit the existing dialogue box; exception text stays in diagnostics.</summary>
internal static class CampaignSaveFeedback
{
    public static string Message(SaveOperationResult result, string playerName = "PLAYER")
    {
        string action = result.Operation == SaveOperation.Save ? "SAVE" : "LOAD";
        if (result.Succeeded)
            return result.Operation == SaveOperation.Save ? playerName + " saved the game."
                : result.FromBackup ? "Loaded the verified backup.\nSave files were not changed." : "Game loaded.";
        if (result.Status == SaveOperationStatus.OverwriteRequired) return "Replace the existing save?";
        if (result.Status == SaveOperationStatus.Blocked)
            return "Can't " + action + " during this event.\nFinish it, then try again.";
        if (result.Status == SaveOperationStatus.Missing)
            return "No saved game was found.\nUse SAVE to create one.";
        string reason = result.Status switch
        {
            SaveOperationStatus.AccessDenied => action + " denied: file permissions.",
            SaveOperationStatus.StorageFailure => action + " file I/O failed.",
            SaveOperationStatus.IncompatibleVersion => action + " needs a matching version.",
            SaveOperationStatus.TooLarge => action + " failed: save is too large.",
            SaveOperationStatus.FilesChanged => "Save files changed. Try again.",
            _ => action + " failed: damaged save data."
        };
        string recovery = result.Files == SaveFileOutcome.Unconfirmed ? "Old save unconfirmed. Retry."
            : result.Status == SaveOperationStatus.StorageFailure ? "Files kept. Check disk; retry."
            : result.Status == SaveOperationStatus.AccessDenied ? "Files kept. Fix access; retry."
            : result.Status == SaveOperationStatus.IncompatibleVersion ? "Files kept. Use matching build."
            : result.Status == SaveOperationStatus.FilesChanged ? "Files kept. Check before retry."
            : "Files kept. Check a backup.";
        return reason + "\n" + recovery;
    }
}
