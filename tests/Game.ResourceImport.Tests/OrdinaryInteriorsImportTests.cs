namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class OrdinaryInteriorsImportTests
{
    [TestMethod]
    public Task HouseMapsAndFreshRenderedPixelsMatchPinnedSource() =>
        PythonResourceCheck.Run("scripts/checks/test-ordinary-interiors-source.py", timeout: TimeSpan.FromMinutes(1));
}
