using FireRed.Tests;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class FieldDoorArtImportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task SharedDoorSourcePixelsAndImport() =>
        PythonResourceCheck.Run(Path.Combine("scripts", "checks", "test-field-door-art.py"), [], context: TestContext);
}
