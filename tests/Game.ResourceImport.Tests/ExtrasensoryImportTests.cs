namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class ExtrasensoryImportTests
{
    [TestMethod]
    public Task ExtrasensorySourceClosureUsesSharedBackgroundAndNoDuplicateMaterial()=>PythonResourceCheck.Run("scripts/checks/test-extrasensory-import.py",timeout:TimeSpan.FromMinutes(2));
}
