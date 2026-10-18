namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class EarthBackgroundImportTests
{
    [TestMethod]
    public Task EarthBackgroundGraphsSharedRocksAndIndexedFissureMatchSource()=>PythonResourceCheck.Run("scripts/checks/test-earth-background-import.py",timeout:TimeSpan.FromMinutes(2));
}
