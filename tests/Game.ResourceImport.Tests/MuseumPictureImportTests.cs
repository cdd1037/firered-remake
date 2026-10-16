namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class MuseumPictureImportTests
{
    [TestMethod]
    public Task OriginalFossilImagesRegenerateWithExactPixelsAndFailClosed() =>
        PythonResourceCheck.Run("scripts/checks/test-museum-pictures.py", timeout: TimeSpan.FromMinutes(1));
}
