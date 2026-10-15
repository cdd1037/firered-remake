using Game.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.Engineering;

[TestClass, TestCategory("Game")]
public sealed class SaveStagingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IdenticalStageReusesRoundtripValidation(bool hasPrimary)
    {
        var storage = new StageStorage();
        if (hasPrimary) storage.Files["slot"] = Encode(1);
        var original = new Counter(2);
        var validated = new List<Counter>();
        new SaveService(storage).Save("slot", original, 2, "r", "c", state =>
        {
            validated.Add(state);
            return null;
        });
        Assert.AreEqual(hasPrimary ? 3 : 2, validated.Count);
        Assert.AreSame(original, validated[0]);
        Assert.AreNotSame(original, validated[1]);
        CollectionAssert.AreEqual(hasPrimary ? new[] { 2, 2, 1 } : new[] { 2, 2 }, validated.Select(x => x.Value).ToArray());
        Assert.AreEqual(hasPrimary, storage.PreservedPrimary);
        Assert.AreEqual(2, SaveCodec.Decode<Counter>(storage.Files["slot"], "r", "c").State.Value);
        if (hasPrimary) CollectionAssert.AreEqual(Encode(1), storage.Files["slot.bak"]);
    }

    [TestMethod]
    public void RoundtripSemanticRejectionStillPrecedesStaging()
    {
        var storage = new StageStorage();
        var original = new Counter(2);
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            new SaveService(storage).Save("slot", original, 2, "r", "c",
                state => ReferenceEquals(state, original) ? null : "roundtrip_rejected"));
        Assert.AreEqual("save_semantic_invalid: roundtrip_rejected", error.Message);
        Assert.AreEqual(0, storage.Files.Count);
        Assert.IsFalse(storage.Committed);
    }

    [TestMethod]
    [DataRow("substitute")]
    [DataRow("corrupt")]
    public void ChangedStageNeverPublishesOrUpdatesBackup(string fault)
    {
        var storage = WithPrimaryAndBackup();
        storage.Fault = fault;
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Save(storage));
        Assert.AreEqual("save_stage_changed", error.Message);
        AssertUnchanged(storage);
    }

    [TestMethod]
    public void MissingStageKeepsExistingFiles()
    {
        var storage = WithPrimaryAndBackup();
        storage.Fault = "missing";
        var error = Assert.ThrowsExactly<IOException>(() => Save(storage));
        Assert.AreEqual("save_stage_missing", error.Message);
        AssertUnchanged(storage);
    }

    [TestMethod]
    public void SemanticInvalidPrimaryDoesNotReplaceGoodBackup()
    {
        var storage = WithPrimaryAndBackup();
        storage.Files["slot"] = Encode(-1);
        new SaveService(storage).Save("slot", new Counter(2), 2, "r", "c", x => x.Value < 0 ? "negative" : null);
        Assert.IsTrue(storage.Committed);
        Assert.IsFalse(storage.PreservedPrimary);
        CollectionAssert.AreEqual(Encode(0), storage.Files["slot.bak"]);
        CollectionAssert.AreEqual(Encode(2), storage.Files["slot"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PrimaryReadFailureStillAbortsCommit(bool permissionDenied)
    {
        var storage = WithPrimaryAndBackup();
        storage.Fault = permissionDenied ? "permission" : "io";
        if (permissionDenied) Assert.ThrowsExactly<UnauthorizedAccessException>(() => Save(storage));
        else Assert.ThrowsExactly<IOException>(() => Save(storage));
        AssertUnchanged(storage);
    }

    private static byte[] Encode(int value) => SaveCodec.Encode(new Counter(value), value < 0 ? 1 : value, "r", "c");
    private static void Save(StageStorage storage) => new SaveService(storage).Save("slot", new Counter(2), 2, "r", "c");
    private static StageStorage WithPrimaryAndBackup()
    {
        var storage = new StageStorage();
        storage.Files["slot"] = Encode(1);
        storage.Files["slot.bak"] = Encode(0);
        return storage;
    }
    private static void AssertUnchanged(StageStorage storage)
    {
        Assert.IsFalse(storage.Committed);
        CollectionAssert.AreEqual(Encode(1), storage.Files["slot"]);
        CollectionAssert.AreEqual(Encode(0), storage.Files["slot.bak"]);
    }

    private sealed class StageStorage : ISaveStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public string? Fault { get; set; }
        public bool Committed { get; private set; }
        public bool PreservedPrimary { get; private set; }
        public byte[]? Read(string slot)
        {
            if (slot == "slot" && Fault == "io") throw new IOException("read_failure");
            if (slot == "slot" && Fault == "permission") throw new UnauthorizedAccessException("read_denied");
            return Files.GetValueOrDefault(slot);
        }
        public void WriteStaged(string slot, ReadOnlySpan<byte> bytes)
        {
            if (Fault == "missing") return;
            var staged = Fault == "substitute" ? Encode(9) : bytes.ToArray();
            if (Fault == "corrupt") staged[0] ^= 1;
            Files[slot + ".tmp"] = staged;
        }
        public void Commit(string slot, bool preservePrimary)
        {
            Committed = true;
            PreservedPrimary = preservePrimary;
            if (preservePrimary) Files[slot + ".bak"] = Files[slot];
            Files[slot] = Files[slot + ".tmp"];
            Files.Remove(slot + ".tmp");
        }
    }
}
