namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class HelpImportTests
{
    [TestMethod]
    public Task OriginalHelpTextTablesAndPredicatesMatchGeneratedCatalog() =>
        PythonResourceCheck.Run("scripts/checks/test-help-system-import.py", timeout: TimeSpan.FromMinutes(1));
}
