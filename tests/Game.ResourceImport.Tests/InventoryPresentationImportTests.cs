namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class InventoryPresentationImportTests
{
    [TestMethod]
    public Task TmCaseAndStorageHandMatchSourcePixelsAndRejectChangedInputs() =>
        PythonResourceCheck.Run("scripts/checks/test-inventory-presentation.py", timeout:TimeSpan.FromMinutes(1));
}
