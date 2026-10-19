using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class FieldCaveArtImportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task CaveSourcePixelsPreviewEligibilityAndImportFailures() =>
        PythonResourceCheck.Run(Path.Combine("scripts", "checks", "test-field-cave-art.py"), [], context: TestContext);
}
