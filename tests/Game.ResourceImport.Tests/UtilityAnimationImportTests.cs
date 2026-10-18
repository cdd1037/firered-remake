namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class UtilityAnimationImportTests
{
    [TestMethod]
    public Task UtilityProgramsRawChildrenAndIndexedMaterialsMatchTheirSource()=>
        PythonResourceCheck.Run("scripts/checks/test-utility-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
