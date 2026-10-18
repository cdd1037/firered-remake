namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class BatonPassAnimationImportTests
{
    [TestMethod]
    public Task BatonPassKeepsSourceCommandsIndexedBallAndSharedPalette()=>
        PythonResourceCheck.Run("scripts/checks/test-baton-pass-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
