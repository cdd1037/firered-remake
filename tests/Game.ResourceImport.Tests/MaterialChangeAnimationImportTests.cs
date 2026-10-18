namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class MaterialChangeAnimationImportTests
{
    [TestMethod]
    public Task MaterialChangeRootsPreservePriorMaterialsAndExactSourceTaskContracts()=>PythonResourceCheck.Run("scripts/checks/test-material-change-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
