namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class SquashWaterFireImportTests
{
    [TestMethod]
    public Task SourceSquashRootsAndSharedMaterialsMatch()=>
        PythonResourceCheck.Run("scripts/checks/test-squash-water-fire-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
