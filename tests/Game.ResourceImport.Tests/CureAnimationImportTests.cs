namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class CureAnimationImportTests
{
    [TestMethod]
    public Task CureProgramsMaterialsAndPaletteGroupsMatchTheirSource()=>
        PythonResourceCheck.Run("scripts/checks/test-cure-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
