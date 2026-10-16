namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class TrainerCardPhotoImportTests
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod]
    public Task OriginalPrinterAndCardAssets()=>PythonResourceCheck.Run(
        Path.Combine("scripts","checks","test-trainer-card-photo-import.py"),context:TestContext);
}
