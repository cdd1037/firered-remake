namespace Game.Application;

public sealed record SaveLoadResult<T>(SaveOperationResult Result, LoadedSave<T>? Saved);

/// <summary>Expected storage failures become explicit outcomes; programming faults still escape.
/// The codec, semantic validation, staged bytes and verified-primary backup rules are unchanged.</summary>
public static class PlayerSaveOperations
{
    public static SaveOperationResult Save<T>(ISaveStorage storage, string slot, T state, long generation,
        string rules, string content, Func<T, string?>? validate = null)
    {
        var observed = new CommitObservedStorage(storage);
        try
        {
            new SaveService(observed).Save(slot, state, generation, rules, content, validate);
            return new(SaveOperation.Save, SaveOperationStatus.Succeeded, SaveFileOutcome.Committed);
        }
        catch (Exception error) when (SaveOperationResult.IsExpectedFailure(error))
        {
            // Once Commit starts, an adapter may have copied a backup or replaced the
            // primary before throwing. Never claim unchanged files without evidence.
            return SaveOperationResult.Failure(SaveOperation.Save, error,
                observed.CommitStarted ? SaveFileOutcome.Unconfirmed : SaveFileOutcome.Preserved);
        }
    }

    public static SaveLoadResult<T> Load<T>(Func<LoadedSave<T>> load)
    {
        try
        {
            var saved = load();
            return new(new(SaveOperation.Load, SaveOperationStatus.Succeeded,
                FromBackup: saved.Source.EndsWith(".bak", StringComparison.Ordinal)), saved);
        }
        catch (Exception error) when (SaveOperationResult.IsExpectedFailure(error))
        {
            return new(SaveOperationResult.Failure(SaveOperation.Load, error), null);
        }
    }

    /// <summary>Only the chosen candidate is read back. An unreadable or oversized
    /// companion cannot block recovery or cause an unbounded fingerprint scan.</summary>
    public static (LoadedSave<T> Saved, string Fingerprint) LoadVerifiedCandidate<T>(ISaveStorage storage, string slot,
        string rules, string content, Func<T, string?>? validate = null)
    {
        var observed = new CandidateReadStorage(storage);
        var saved = new SaveService(observed).Load<T>(slot, rules, content, validate);
        if (observed.Path != saved.Source || observed.Bytes is null)
            throw new InvalidOperationException("save_candidate_observation_missing");
        var readback = storage.Read(saved.Source);
        if (readback is null || !readback.AsSpan().SequenceEqual(observed.Bytes))
            throw new IOException("save_file_changed_during_load");
        string fingerprint = observed.Bytes.Length + ":" +
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(observed.Bytes));
        return (saved, fingerprint);
    }

    private sealed class CandidateReadStorage(ISaveStorage storage) : ISaveStorage
    {
        public string? Path { get; private set; }
        public byte[]? Bytes { get; private set; }
        public byte[]? Read(string slot)
        {
            var bytes = storage.Read(slot);
            Path = slot; Bytes = bytes;
            return bytes;
        }
        public void WriteStaged(string slot, ReadOnlySpan<byte> bytes) => throw new NotSupportedException("load_is_read_only");
        public void Commit(string slot, bool preservePrimary) => throw new NotSupportedException("load_is_read_only");
    }

    private sealed class CommitObservedStorage(ISaveStorage storage) : ISaveStorage
    {
        public bool CommitStarted { get; private set; }
        public byte[]? Read(string slot) => storage.Read(slot);
        public void WriteStaged(string slot, ReadOnlySpan<byte> bytes) => storage.WriteStaged(slot, bytes);
        public void Commit(string slot, bool preservePrimary)
        {
            CommitStarted = true;
            storage.Commit(slot, preservePrimary);
        }
    }
}
