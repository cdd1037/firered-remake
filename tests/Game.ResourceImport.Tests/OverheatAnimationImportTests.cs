namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class OverheatAnimationImportTests
{
    [TestMethod]
    public Task OverheatRetainsSourcePaletteOwnershipAndExistingFlameMaterials()=>PythonResourceCheck.Run("scripts/checks/test-overheat-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
