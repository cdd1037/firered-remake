namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class EvolutionSceneImportTests
{
    [TestMethod]
    public Task PackedEvolutionAndHatchPixelsRebuildAndRejectTampering() =>
        PythonResourceCheck.Run("scripts/checks/test-evolution-scene-import.py", timeout: TimeSpan.FromMinutes(1));
}
