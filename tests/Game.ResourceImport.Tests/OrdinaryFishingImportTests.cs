namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class OrdinaryFishingImportTests
{
    [TestMethod]
    public Task RegeneratesEveryPinnedOrdinaryRodDefinition() =>
        PythonResourceCheck.Run("scripts/checks/test-fishing-import.py", timeout: TimeSpan.FromMinutes(1));
}
