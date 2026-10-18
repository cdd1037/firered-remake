namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class NamedGeneralAnimationImportTests
{
    [TestMethod]
    public Task ExistingGeneralProgramsRetainTheirImportContract()=>PythonResourceCheck.Run("scripts/checks/test-general-animation-import.py",timeout:TimeSpan.FromMinutes(2));
    [TestMethod]
    public Task NamedGeneralRootsRetainSourceAndCanonicalDependencies()=>PythonResourceCheck.Run("scripts/checks/test-named-general-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
