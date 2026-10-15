using Game.Application;

namespace FireRed.Tests.Application.Engineering;

[TestClass, TestCategory("Game")]
public sealed class SaveOperationTests
{
    private const string Rules = "feedback-r1", Content = "feedback-c1";

    [TestMethod]
    [DataRow("permission", SaveOperationStatus.AccessDenied)]
    [DataRow("disk", SaveOperationStatus.StorageFailure)]
    [DataRow("oversized", SaveOperationStatus.TooLarge)]
    [DataRow("changed", SaveOperationStatus.FilesChanged)]
    public void Failed_staging_reports_reason_and_preserves_primary_and_backup(string fault, SaveOperationStatus expected)
    {
        using var storage = Seeded();
        var primary = File.ReadAllBytes(storage.Slot);
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        var injected = new FaultStorage(storage) { BeforeStage = () => throw Error(fault) };
        var result = Save(injected, storage.Slot);
        Assert.AreEqual(expected, result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, result.Files);
        Assert.IsFalse(result.Succeeded);
        CollectionAssert.AreEqual(primary, File.ReadAllBytes(storage.Slot));
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
        Assert.IsTrue(Save(storage, storage.Slot).Succeeded, "Fixing the fault must allow the same save to be retried.");
    }

    [TestMethod]
    public void Stage_substitution_keeps_both_old_files()
    {
        using var storage = Seeded();
        var primary = File.ReadAllBytes(storage.Slot);
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        storage.SubstituteStage = SaveCodec.Encode(new Counter(99), 99, Rules, Content);
        var result = Save(storage, storage.Slot);
        Assert.AreEqual(SaveOperationStatus.FilesChanged, result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, result.Files);
        CollectionAssert.AreEqual(primary, File.ReadAllBytes(storage.Slot));
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
    }

    [TestMethod]
    public void Primary_read_failure_aborts_commit_and_preserves_old_files()
    {
        using var storage = Seeded();
        var primary = File.ReadAllBytes(storage.Slot);
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        var injected = new FaultStorage(storage) { BeforeRead = path => { if (path == storage.Slot) throw new IOException("read_failed"); } };
        var result = Save(injected, storage.Slot);
        Assert.AreEqual(SaveOperationStatus.StorageFailure, result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, result.Files);
        CollectionAssert.AreEqual(primary, File.ReadAllBytes(storage.Slot));
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Commit_failure_does_not_claim_old_files_are_unchanged(bool afterReplacement)
    {
        using var storage = Seeded();
        var injected = new FaultStorage(storage)
        {
            BeforeCommit = afterReplacement ? null : () => throw new IOException("rename_failed"),
            AfterCommit = afterReplacement ? () => throw new IOException("adapter_failed_after_replace") : null
        };
        var result = Save(injected, storage.Slot);
        Assert.AreEqual(SaveOperationStatus.StorageFailure, result.Status);
        Assert.AreEqual(SaveFileOutcome.Unconfirmed, result.Files);
        Assert.AreEqual(afterReplacement ? 3 : 2,
            new SaveService(storage).Load<Counter>(storage.Slot, Rules, Content).State.Value);
    }

    [TestMethod]
    public void Permission_denied_primary_can_load_verified_backup_without_writing()
    {
        using var storage = Seeded();
        var primary = File.ReadAllBytes(storage.Slot);
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        var injected = new FaultStorage(storage)
        { BeforeRead = path => { if (path == storage.Slot) throw new UnauthorizedAccessException("primary_denied"); } };
        var attempt = Load(injected, storage.Slot);
        Assert.IsTrue(attempt.Result.Succeeded);
        Assert.IsTrue(attempt.Result.FromBackup);
        Assert.AreEqual(1, attempt.Saved!.State.Value);
        CollectionAssert.AreEqual(primary, File.ReadAllBytes(storage.Slot));
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
    }

    [TestMethod]
    [DataRow("permission", SaveOperationStatus.AccessDenied)]
    [DataRow("disk", SaveOperationStatus.StorageFailure)]
    [DataRow("oversized", SaveOperationStatus.TooLarge)]
    [DataRow("version", SaveOperationStatus.IncompatibleVersion)]
    [DataRow("damaged", SaveOperationStatus.InvalidData)]
    public void Load_keeps_candidate_failure_types_and_diagnostic_detail(string fault, SaveOperationStatus expected)
    {
        using var storage = Seeded();
        var injected = new FaultStorage(storage) { BeforeRead = _ => throw Error(fault) };
        var attempt = Load(injected, storage.Slot);
        Assert.AreEqual(expected, attempt.Result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, attempt.Result.Files);
        Assert.IsNull(attempt.Saved);
        Assert.IsTrue(attempt.Result.DiagnosticDetail!.Contains(Error(fault).Message, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Missing_save_is_distinct_from_damage_and_wrong_version()
    {
        using var storage = new IsolatedFileStorage();
        Assert.AreEqual(SaveOperationStatus.Missing, Load(storage, storage.Slot).Result.Status);
        File.WriteAllBytes(storage.Slot, SaveCodec.Encode(new Counter(1), 1, "other-rules", Content));
        Assert.AreEqual(SaveOperationStatus.IncompatibleVersion, Load(storage, storage.Slot).Result.Status);
        File.WriteAllText(storage.Slot, "broken JSON");
        Assert.AreEqual(SaveOperationStatus.InvalidData, Load(storage, storage.Slot).Result.Status);
    }

    [TestMethod]
    public void Saving_after_corrupt_primary_keeps_good_backup_and_checksum_validation()
    {
        using var storage = Seeded();
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        File.WriteAllText(storage.Slot, "corrupt");
        Assert.IsTrue(Save(storage, storage.Slot).Succeeded);
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
        Assert.AreEqual(3, Load(storage, storage.Slot).Saved!.State.Value);
        var json = File.ReadAllText(storage.Slot).Replace("\"Value\":3", "\"Value\":9", StringComparison.Ordinal);
        File.WriteAllText(storage.Slot, json);
        var recovered = Load(storage, storage.Slot);
        Assert.IsTrue(recovered.Result.FromBackup, "A changed payload with its old checksum must not load.");
        Assert.AreEqual(1, recovered.Saved!.State.Value);
    }

    [TestMethod]
    [DataRow("permission")]
    [DataRow("oversized")]
    [DataRow("disk")]
    public void Entry_candidate_verification_recovers_without_scanning_unreadable_primary(string fault)
    {
        using var storage = Seeded();
        var reads = new List<string>();
        var backup = File.ReadAllBytes(storage.Slot + ".bak");
        var injected = new FaultStorage(storage)
        {
            BeforeRead = path => { reads.Add(path); if (path == storage.Slot) throw Error(fault); }
        };
        var attempt = LoadProtected(injected, storage.Slot);
        Assert.IsTrue(attempt.Result.Succeeded);
        Assert.IsTrue(attempt.Result.FromBackup);
        CollectionAssert.AreEqual(new[] { storage.Slot, storage.Slot + ".bak", storage.Slot + ".bak" }, reads);
        CollectionAssert.AreEqual(backup, File.ReadAllBytes(storage.Slot + ".bak"));
    }

    [TestMethod]
    public void Entry_with_valid_primary_never_reads_unselected_backup()
    {
        using var storage = Seeded();
        var reads = new List<string>();
        var injected = new FaultStorage(storage)
        {
            BeforeRead = path =>
            {
                reads.Add(path);
                if (path.EndsWith(".bak", StringComparison.Ordinal)) throw new InvalidOperationException("backup_must_not_be_scanned");
            }
        };
        var attempt = LoadProtected(injected, storage.Slot);
        Assert.IsTrue(attempt.Result.Succeeded);
        Assert.IsFalse(attempt.Result.FromBackup);
        CollectionAssert.AreEqual(new[] { storage.Slot, storage.Slot }, reads);
    }

    [TestMethod]
    public void Entry_rejects_candidate_changed_between_decode_and_readback()
    {
        using var storage = Seeded();
        int reads = 0;
        var injected = new FaultStorage(storage)
        {
            BeforeRead = path =>
            {
                if (++reads == 2) File.WriteAllBytes(path, SaveCodec.Encode(new Counter(99), 99, Rules, Content));
            }
        };
        var attempt = LoadProtected(injected, storage.Slot);
        Assert.AreEqual(SaveOperationStatus.FilesChanged, attempt.Result.Status);
        Assert.IsNull(attempt.Saved, "Changed bytes must not publish a player session.");
        Assert.AreEqual(SaveFileOutcome.Preserved, attempt.Result.Files);
    }

    [TestMethod]
    public void Unexpected_programming_fault_is_not_swallowed_as_storage_error()
    {
        using var storage = Seeded();
        var injected = new FaultStorage(storage) { BeforeStage = () => throw new InvalidOperationException("programming_fault") };
        Assert.ThrowsExactly<InvalidOperationException>(() => Save(injected, storage.Slot));
        Assert.ThrowsExactly<InvalidOperationException>(() => PlayerSaveOperations.Load<Counter>(() => throw new InvalidOperationException("programming_fault")));
    }

    private static SaveOperationResult Save(ISaveStorage storage, string slot) =>
        PlayerSaveOperations.Save(storage, slot, new Counter(3), 3, Rules, Content);
    private static SaveLoadResult<Counter> Load(ISaveStorage storage, string slot) =>
        PlayerSaveOperations.Load(() => new SaveService(storage).Load<Counter>(slot, Rules, Content));
    private static SaveLoadResult<Counter> LoadProtected(ISaveStorage storage, string slot) =>
        PlayerSaveOperations.Load(() => PlayerSaveOperations.LoadVerifiedCandidate<Counter>(storage, slot, Rules, Content).Saved);
    private static IsolatedFileStorage Seeded()
    {
        var storage = new IsolatedFileStorage();
        var service = new SaveService(storage);
        service.Save(storage.Slot, new Counter(1), 1, Rules, Content);
        service.Save(storage.Slot, new Counter(2), 2, Rules, Content);
        return storage;
    }
    private static Exception Error(string fault) => fault switch
    {
        "permission" => new UnauthorizedAccessException("private/path/access_denied"),
        "disk" => new IOException("disk_full_or_unavailable"),
        "oversized" => new InvalidDataException("save_file_too_large"),
        "changed" => new IOException("save_file_changed_before_commit"),
        "version" => new InvalidDataException("save_version_mismatch"),
        _ => new InvalidDataException("save_checksum_mismatch")
    };
    private sealed class FaultStorage(ISaveStorage storage) : ISaveStorage
    {
        public Action<string>? BeforeRead { get; init; }
        public Action? BeforeStage { get; init; }
        public Action? BeforeCommit { get; init; }
        public Action? AfterCommit { get; init; }
        public byte[]? Read(string slot) { BeforeRead?.Invoke(slot); return storage.Read(slot); }
        public void WriteStaged(string slot, byte[] bytes) { BeforeStage?.Invoke(); storage.WriteStaged(slot, bytes); }
        public void Commit(string slot, bool preservePrimary)
        { BeforeCommit?.Invoke(); storage.Commit(slot, preservePrimary); AfterCommit?.Invoke(); }
    }
}
