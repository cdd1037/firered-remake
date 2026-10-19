using Game.Application;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SaveFeedbackTests
{
    private static readonly Game.Content.PresentationTextCatalog Text = Game.Content.PresentationTextCatalog.English("save");
    [TestMethod]
    public void MissingHatchTotalRequestsExplicitConversionWithoutCallingSaveDamaged()
    {
        var text = CampaignSaveFeedback.Message(Text, new(SaveOperation.Load, SaveOperationStatus.MigrationRequired));
        StringAssert.Contains(text, "known hatch count");
        StringAssert.Contains(text, "Convert explicitly");
        Assert.IsFalse(text.Contains("damaged", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Every_result_fits_two_short_lines_without_diagnostic_paths()
    {
        foreach (var operation in Enum.GetValues<SaveOperation>())
        foreach (var status in Enum.GetValues<SaveOperationStatus>())
        foreach (var files in Enum.GetValues<SaveFileOutcome>())
        {
            var message = CampaignSaveFeedback.Message(Text, new(operation, status, files, "private/path/secret.json"), "ABCDEFG");
            var lines = message.Split('\n');
            Assert.IsTrue(lines.Length <= 2, message);
            Assert.IsTrue(lines.All(line => line.Length <= 34), message);
            Assert.IsFalse(message.Contains("private/path", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void IO_and_access_failures_never_blame_current_location()
    {
        var disk = CampaignSaveFeedback.Message(Text, new(SaveOperation.Save, SaveOperationStatus.StorageFailure));
        var denied = CampaignSaveFeedback.Message(Text, new(SaveOperation.Load, SaveOperationStatus.AccessDenied));
        StringAssert.Contains(disk, "I/O");
        StringAssert.Contains(disk, "Check disk; retry");
        StringAssert.Contains(denied, "permissions");
        StringAssert.Contains(denied, "Files kept");
        Assert.IsFalse(disk.Contains("event", StringComparison.Ordinal));
        Assert.IsFalse(denied.Contains("field", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Commit_failure_and_backup_recovery_are_truthful()
    {
        var failed = CampaignSaveFeedback.Message(Text, new(SaveOperation.Save, SaveOperationStatus.StorageFailure, SaveFileOutcome.Unconfirmed));
        StringAssert.Contains(failed, "Old save unconfirmed");
        Assert.IsFalse(failed.Contains("Files kept", StringComparison.Ordinal));
        var recovered = CampaignSaveFeedback.Message(Text, new(SaveOperation.Load, SaveOperationStatus.Succeeded, FromBackup: true));
        StringAssert.Contains(recovered, "verified backup");
        StringAssert.Contains(recovered, "files were not changed");
    }
    [TestMethod]
    public void LocalPackUsesTheSameTypedSaveDecisionAndLiteralPlayerName()
    {
        using var pack = FireRed.Tests.TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, "save.json"), """{"success.save":"{PlayerName}: game saved."}""");
        var text = Game.Content.PresentationTextCatalog.Load("save", pack.Path);
        Assert.AreEqual("{Action}: game saved.", CampaignSaveFeedback.Message(text,
            new(SaveOperation.Save, SaveOperationStatus.Succeeded), "{Action}"));
        Assert.AreEqual(CampaignSaveFeedback.Message(Text, new(SaveOperation.Load, SaveOperationStatus.Succeeded)),
            CampaignSaveFeedback.Message(text, new(SaveOperation.Load, SaveOperationStatus.Succeeded)));
    }

}
