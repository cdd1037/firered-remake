namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class ItemWindAnimationImportTests
{
    [TestMethod]
    public Task ItemWindSourceGraphsSharedCallbacksAndIndexedPrefixesMatch()=>
        PythonResourceCheck.Run("scripts/checks/test-item-wind-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
