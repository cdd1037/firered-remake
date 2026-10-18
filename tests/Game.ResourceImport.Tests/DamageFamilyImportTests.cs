namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class DamageFamilyImportTests
{
    [TestMethod]
    public Task SourceDamageRootsCallbacksAndIndexedMaterialsMatch()=>
        PythonResourceCheck.Run("scripts/checks/test-damage-family-import.py",timeout:TimeSpan.FromMinutes(2));
}
