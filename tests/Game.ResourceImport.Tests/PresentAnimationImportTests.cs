namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class PresentAnimationImportTests
{
    [TestMethod]
    public Task PresentKeepsSourceBranchesFiniteProgramsAndSharedMaterials()=>PythonResourceCheck.Run("scripts/checks/test-present-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
