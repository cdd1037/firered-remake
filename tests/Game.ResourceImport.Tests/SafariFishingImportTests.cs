namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class SafariFishingImportTests
{
    [TestMethod]
    public Task RegeneratesPinnedSafariRodDefinitions() =>
        PythonResourceCheck.Run("scripts/checks/test-safari-fishing-import.py", timeout: TimeSpan.FromMinutes(1));
}
