namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class PetalPawImportTests
{
    [TestMethod]
    public Task AssistSweetScentGraphsFramesAndMaterialsMatchSource()=>PythonResourceCheck.Run("scripts/checks/test-petal-paw-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
