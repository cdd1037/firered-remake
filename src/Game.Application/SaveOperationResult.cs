using System.Text.Json;

namespace Game.Application;

public enum SaveOperation { Save, Load }
public enum SaveOperationStatus
{
    Succeeded, OverwriteRequired, Blocked, Missing, AccessDenied, StorageFailure,
    InvalidData, IncompatibleVersion, TooLarge, FilesChanged
}
public enum SaveFileOutcome { Preserved, Committed, Unconfirmed }

/// <summary>Player-facing outcome. DiagnosticDetail is for logs, never UI text.</summary>
public sealed record SaveOperationResult(
    SaveOperation Operation,
    SaveOperationStatus Status,
    SaveFileOutcome Files = SaveFileOutcome.Preserved,
    string? DiagnosticDetail = null,
    bool FromBackup = false)
{
    public bool Succeeded => Status == SaveOperationStatus.Succeeded;
    public static bool IsExpectedFailure(Exception error) =>
        error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException;

    public static SaveOperationResult Failure(SaveOperation operation, Exception error,
        SaveFileOutcome files = SaveFileOutcome.Preserved)
    {
        var status = Classify(error);
        if (status == SaveOperationStatus.Missing && operation == SaveOperation.Save)
            status = SaveOperationStatus.StorageFailure;
        return new(operation, status, files, error.ToString());
    }

    private static SaveOperationStatus Classify(Exception error)
    {
        // Load preserves the individual candidate failures. Do not parse filesystem
        // paths or OS-localized error messages to recover their types.
        if (error.InnerException is AggregateException aggregate)
        {
            var failures = aggregate.InnerExceptions.Select(Classify).ToArray();
            foreach (var status in new[] { SaveOperationStatus.FilesChanged, SaveOperationStatus.AccessDenied,
                SaveOperationStatus.StorageFailure, SaveOperationStatus.IncompatibleVersion,
                SaveOperationStatus.TooLarge, SaveOperationStatus.InvalidData })
                if (failures.Contains(status)) return status;
            return SaveOperationStatus.Missing;
        }
        if (error is UnauthorizedAccessException) return SaveOperationStatus.AccessDenied;
        if (error is FileNotFoundException or DirectoryNotFoundException) return SaveOperationStatus.Missing;
        if (error.Message.StartsWith("save_file_changed", StringComparison.Ordinal) || error.Message == "save_stage_changed")
            return SaveOperationStatus.FilesChanged;
        if (error.Message is "save_schema_unsupported" or "save_version_mismatch") return SaveOperationStatus.IncompatibleVersion;
        if (error.Message == "save_file_too_large") return SaveOperationStatus.TooLarge;
        if (error is InvalidDataException or JsonException) return SaveOperationStatus.InvalidData;
        return SaveOperationStatus.StorageFailure;
    }
}
