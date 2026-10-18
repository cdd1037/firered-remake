namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class ExpulsionAnimationImportTests
{
    [TestMethod]
    public Task ExpulsionCallbacksReuseSourceMaterialsAndTypedAffinePrograms()=>PythonResourceCheck.Run("scripts/checks/test-expulsion-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
