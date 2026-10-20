namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class TrainerCardAtlasImportTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task BoundedCardPagesPreservePixelsAndLogicalIdentities() => PythonResourceCheck.Run(
        Path.Combine("scripts", "checks", "test-trainer-card-atlas.py"), context: TestContext);
}
