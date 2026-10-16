using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Items;

namespace FireRed.Tests.Application.Engineering;

[TestClass, TestCategory("Game")]
public sealed class CampaignSaveBoundaryTests
{
    [TestMethod]
    [DataRow("machines")]
    [DataRow("keys")]
    [DataRow("bag")]
    [DataRow("pc")]
    [DataRow("berries")]
    [DataRow("balls")]
    public void ChecksumValidNullStackIsRejectedBeforeConsumersAndValidBackupLoads(string pocket)
    {
        var c = CurrentCampaignFixture.Catalog;
        var validator = CampaignSession.CreateCurrent(c);
        var good = CurrentCampaignFixture.Checkpoint.State;
        var malformed = ImmutableArray.Create<InventoryStack>((InventoryStack)null!);
        var inventory = pocket switch
        {
            "machines" => good.Inventory! with { Machines = malformed },
            "keys" => good.Inventory! with { KeyItems = malformed },
            "bag" => good.Inventory! with { BagItems = malformed },
            "pc" => good.Inventory! with { PcItems = malformed },
            "berries" => good.Inventory! with { Berries = malformed },
            "balls" => good.Inventory! with { Balls = malformed },
            _ => throw new InvalidOperationException()
        };
        var bad = good with { Inventory = inventory };
        Assert.AreEqual("inventory_stack_missing", validator.ValidatePlayerSave(bad));
        // Re-encoding deliberately gives this structurally invalid payload a valid checksum.
        // This exercises untrusted data admission, not an ordinary bit flip or a bypassed checksum.
        var primary = SaveCodec.Encode(bad, 2, validator.RulesVersion, c.ContentVersion);
        var backup = SaveCodec.Encode(good, 1, validator.RulesVersion, c.ContentVersion);
        var storage = new ReadOnlyStorage(primary, backup);
        var loaded = PlayerSaveOperations.Load(() => new SaveService(storage).Load<CampaignState>("slot",
            validator.RulesVersion, c.ContentVersion, validator.ValidatePlayerSave));
        Assert.IsTrue(loaded.Result.Succeeded, loaded.Result.DiagnosticDetail);
        Assert.IsTrue(loaded.Result.FromBackup);
        Assert.AreEqual(CanonicalJson.Hash(good), CanonicalJson.Hash(loaded.Saved!.State));
        CollectionAssert.AreEqual(new[] { "slot", "slot.bak" }, storage.Reads);
        CollectionAssert.AreEqual(primary, storage.Primary);
        CollectionAssert.AreEqual(backup, storage.Backup!);

        var missingBackup = new ReadOnlyStorage(primary, null);
        var rejected = PlayerSaveOperations.Load(() => new SaveService(missingBackup).Load<CampaignState>("slot",
            validator.RulesVersion, c.ContentVersion, validator.ValidatePlayerSave));
        Assert.AreEqual(SaveOperationStatus.InvalidData, rejected.Result.Status);
        Assert.IsNull(rejected.Saved);
    }

    private sealed class ReadOnlyStorage(byte[] primary, byte[]? backup) : ISaveStorage
    {
        public byte[] Primary { get; } = primary;
        public byte[]? Backup { get; } = backup;
        public List<string> Reads { get; } = [];
        public byte[]? Read(string slot) { Reads.Add(slot); return slot == "slot" ? Primary : slot == "slot.bak" ? Backup : null; }
        public void WriteStaged(string slot, ReadOnlySpan<byte> bytes) => throw new AssertFailedException("Loading must not write.");
        public void Commit(string slot, bool preservePrimary) => throw new AssertFailedException("Loading must not commit.");
    }
}
