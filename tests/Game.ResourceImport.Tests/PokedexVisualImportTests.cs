namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class PokedexVisualImportTests
{
    [TestMethod]
    public Task NativePokedexAtlasMatchesPinnedSourcePixels() =>
        PythonResourceCheck.Run("scripts/checks/test-pokedex-visual-import.py", timeout: TimeSpan.FromMinutes(1));
}
