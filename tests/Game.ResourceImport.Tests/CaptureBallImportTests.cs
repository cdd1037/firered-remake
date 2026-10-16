namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class CaptureBallImportTests
{
    [TestMethod]
    public Task NativeBallFramesAndPaletteOverlaysRegenerateExactly() =>
        PythonResourceCheck.Run("scripts/checks/test-capture-balls.py", [], timeout: TimeSpan.FromMinutes(1));
}
