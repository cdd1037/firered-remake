namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class PsychicUtilityAnimationImportTests
{
    [TestMethod]
    public Task PsychicProgramsAndMaterialsMatchTheirSource()=>
        PythonResourceCheck.Run("scripts/checks/test-psychic-utility-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
