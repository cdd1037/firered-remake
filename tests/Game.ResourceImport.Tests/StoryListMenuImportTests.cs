namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class StoryListMenuImportTests
{
    [TestMethod]
    public Task SourceMenuLabelsAndNativeListGeometryMatchPinnedInput() =>
        PythonResourceCheck.Run("scripts/checks/test-story-menus.py", timeout: TimeSpan.FromMinutes(1));
}
