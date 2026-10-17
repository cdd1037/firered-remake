namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class ProjectileAnimationImportTests
{
    [TestMethod]
    public Task ProjectileRecipesMaterialsAndSoundAbiMatchSource()=>PythonResourceCheck.Run("scripts/checks/test-projectile-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
