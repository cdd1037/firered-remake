using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class FieldMapPreviewArtImportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task RegionPreviewPixelsNamesFlagsAndImportFailures() =>
        PythonResourceCheck.Run(Path.Combine("scripts", "checks", "test-field-map-preview-art.py"), [], context: TestContext);
}
