namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class ParticleFamilyAnimationImportTests
{
    [TestMethod]
    public Task SmokescreenKeepsRawTagOwnershipAndIndexedSourceMaterials()=>PythonResourceCheck.Run("scripts/checks/test-smokescreen-animation-import.py",timeout:TimeSpan.FromMinutes(2));
    [TestMethod]
    public Task SuperpowerKeepsSourceProgramsAndRegisteredRockMaterials()=>PythonResourceCheck.Run("scripts/checks/test-superpower-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
