namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class SourceAnimationBackgroundImportTests
{
    [TestMethod]
    public Task IndexedBattleBackgroundsMatchSource() => PythonResourceCheck.Run("scripts/checks/test-animation-background-import.py", timeout: TimeSpan.FromMinutes(2));
}
