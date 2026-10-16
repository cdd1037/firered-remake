namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class DiplomaImportTests
{
    [TestMethod]
    public Task NativeCertificatesMatchEveryOriginalTilePixelAndRejectBadInputs() =>
        PythonResourceCheck.Run("scripts/checks/test-diploma-import.py", timeout: TimeSpan.FromMinutes(1));
}
