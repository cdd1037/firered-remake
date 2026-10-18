namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class SurfAnimationImportTests
{
    [TestMethod]
    public Task SurfSourceRootsAndIndexed512PixelOverlayMaterialsMatch()=>PythonResourceCheck.Run("scripts/checks/test-surf-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
