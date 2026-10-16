namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class WaterPathHouseImportTests
{
    [TestMethod]
    public Task BothHouseMapsDoorsAndFreshRenderedPixelsMatchPinnedSource() =>
        PythonResourceCheck.Run("scripts/checks/test-water-path-house-import.py", timeout: TimeSpan.FromMinutes(1));
}
