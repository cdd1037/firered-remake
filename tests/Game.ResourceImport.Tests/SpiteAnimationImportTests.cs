namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class SpiteAnimationImportTests
{
    [TestMethod]
    public Task SpiteSourceRootPaletteOwnershipAndUnchangedMaterials()=>
        PythonResourceCheck.Run("scripts/checks/test-spite-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
