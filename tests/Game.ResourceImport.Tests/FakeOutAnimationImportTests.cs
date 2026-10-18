namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class FakeOutAnimationImportTests
{
    [TestMethod]
    public Task FakeOutUsesCanonicalSharedWindowAndExistingMaterialOwners()=>PythonResourceCheck.Run("scripts/checks/test-fake-out-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
