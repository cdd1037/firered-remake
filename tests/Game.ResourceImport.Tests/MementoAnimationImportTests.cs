namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class MementoAnimationImportTests
{
    [TestMethod]
    public Task SourceRootHasExactCallbacksAndNoNewArt()=>PythonResourceCheck.Run("scripts/checks/test-memento-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
