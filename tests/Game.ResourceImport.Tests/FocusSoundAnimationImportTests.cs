namespace FireRed.Tests.ResourceImport;

[TestClass,TestCategory("ResourceImport")]
public sealed class FocusSoundAnimationImportTests
{
    [TestMethod]
    public Task FocusSoundRecipesIndexedMaterialsAndCallbackSoundsMatchSource()=>
        PythonResourceCheck.Run("scripts/checks/test-focus-sound-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
