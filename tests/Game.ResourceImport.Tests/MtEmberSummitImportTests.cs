namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class MtEmberSummitImportTests
{
    [TestMethod]
    public Task SummitSourceMapsRocksAndMoltresPaletteRemainExact() =>
        PythonResourceCheck.Run("scripts/checks/test-mt-ember-summit-import.py", timeout: TimeSpan.FromMinutes(1));
}
