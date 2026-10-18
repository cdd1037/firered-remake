namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class ScanlineBattlerImportTests
{
    [TestMethod]
    public Task ScanlineBattlerGraphsSharedMaterialsAndSourceRegistrationsMatch()=>PythonResourceCheck.Run("scripts/checks/test-scanline-battler-import.py",timeout:TimeSpan.FromMinutes(2));
}
