namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class OpeningPresentationImportTests
{
    [TestMethod]
    public Task PinnedOpeningTextKeyboardAndPixelsRegenerateExactly() =>
        PythonResourceCheck.Run("scripts/imports/presentation/import-opening-presentation.py", ["--check"], timeout: TimeSpan.FromMinutes(1));
    [TestMethod]
    public Task NamingLayersTemplatesAndPcIconMatchSourceOracleAndRejectInvalidInput() =>
        PythonResourceCheck.Run("scripts/checks/test-opening-naming-import.py", timeout: TimeSpan.FromMinutes(1));
}
