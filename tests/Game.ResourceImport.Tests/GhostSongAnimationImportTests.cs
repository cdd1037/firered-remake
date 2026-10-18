namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class GhostSongAnimationImportTests
{
    [TestMethod]
    public Task GhostSongSourceRecipesIndexedMaterialsAndPartialPaletteContractsMatch()=>PythonResourceCheck.Run("scripts/checks/test-ghost-song-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
