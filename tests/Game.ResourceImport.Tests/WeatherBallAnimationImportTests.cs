namespace FireRed.Tests.ResourceImport;
[TestClass,TestCategory("ResourceImport")]
public sealed class WeatherBallAnimationImportTests
{
    [TestMethod]
    public Task WeatherBallRetainsNativeBranchesControllerBitsAndCanonicalMaterials()=>PythonResourceCheck.Run("scripts/checks/test-weather-ball-animation-import.py",timeout:TimeSpan.FromMinutes(2));
}
