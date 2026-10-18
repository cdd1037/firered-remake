namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class DoomDesireAnimationImportTests
{
    [TestMethod]
    public Task DoomDesirePreservesSetupAndSeparateDelayedHitBoundary()=>PythonResourceCheck.Run("scripts/checks/test-doom-desire-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
