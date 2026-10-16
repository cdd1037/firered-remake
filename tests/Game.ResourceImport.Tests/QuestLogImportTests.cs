namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class QuestLogImportTests
{
    [TestMethod]
    public Task OriginalQuestLogDataRegeneratesWithoutWriting()=>
        PythonResourceCheck.Run("scripts/imports/presentation/import-quest-log.py",["--check"],timeout:TimeSpan.FromMinutes(1));
    [TestMethod]
    public Task QuestLogParserChecksIndependentVectorsAndMissingCaptionFailure()=>
        PythonResourceCheck.Run("scripts/checks/quest-log-import-check.py",[],timeout:TimeSpan.FromMinutes(1));
}
