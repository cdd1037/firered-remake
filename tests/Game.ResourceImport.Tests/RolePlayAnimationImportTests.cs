namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class RolePlayAnimationImportTests
{
    [TestMethod]
    public Task RolePlayPreservesRootAndExactBaseMonsterMaterials()=>PythonResourceCheck.Run("scripts/checks/test-role-play-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
