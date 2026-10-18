namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class ChargedRootAnimationImportTests
{
    [TestMethod]
    public Task SkyAttackAndSpitUpPreserveCompleteSourceBranchesAndMaterials()=>
        PythonResourceCheck.Run("scripts/checks/test-charged-root-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
