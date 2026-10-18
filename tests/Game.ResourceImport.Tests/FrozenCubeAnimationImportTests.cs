namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class FrozenCubeAnimationImportTests
{
    [TestMethod]
    public Task FrozenCubeSourceRootRawChildAndIndexedSubspriteMaterialsMatch()=>PythonResourceCheck.Run("scripts/checks/test-frozen-cube-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
