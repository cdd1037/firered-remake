namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class SpotlightImportTests
{
    [TestMethod]
    public Task EncoreFlatterGraphsFramesAndMaterialsMatchSource()=>PythonResourceCheck.Run("scripts/checks/test-spotlight-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
