namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class ElectricWispAnimationImportTests
{
    [TestMethod]
    public Task ElectricWispRecipesAndRawChildDependenciesMatchSource()=>
        PythonResourceCheck.Run("scripts/checks/test-electric-wisp-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
