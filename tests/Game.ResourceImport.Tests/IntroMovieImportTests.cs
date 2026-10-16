namespace FireRed.Tests.ResourceImport;
[TestClass, TestCategory("ResourceImport")]
public sealed class IntroMovieImportTests
{
    [TestMethod]
    public Task IntroLayersAndSpriteStripsRegenerateExactly() => PythonResourceCheck.Run("scripts/imports/presentation/import-intro-movie.py", ["--check"], timeout: TimeSpan.FromMinutes(1));
    [TestMethod]
    public Task GrayscaleInputsMatchIndependentRawIndexReferences() => PythonResourceCheck.Run("scripts/checks/test-opening-grayscale-import.py", [], timeout: TimeSpan.FromMinutes(1));
}
