using Game.Application;

namespace FireRed.Tests.Application.Campaign;

[TestClass]
public sealed class GameStartupSelectionTests
{
    [TestMethod]
    public void OrdinaryLaunchUsesHostResourceRootWithoutEnvironment()
    {
        string root=Path.GetFullPath("standalone-game");
        var startup=GameStartupSelection.Resolve([],new Dictionary<string,string?>(),root);
        Assert.AreEqual(GameStartupMode.Current,startup.Mode);
        Assert.AreEqual(root,startup.ProjectRoot);
        Assert.AreEqual("",startup.Checkpoint);
    }

    [TestMethod]
    public void ExplicitDevelopmentOverrideWins()
    {
        string root=Path.GetFullPath("development-game");
        var startup=GameStartupSelection.Resolve([],new Dictionary<string,string?>{{"FIRERED_PROJECT_ROOT",root}},"unused");
        Assert.AreEqual(root,startup.ProjectRoot);
    }

    [TestMethod]
    public void DefaultRootDoesNotSelectEngineeringModes()
    {
        foreach(var (argument,mode) in new[]{("--probe",GameStartupMode.Probe),("--harness",GameStartupMode.Harness)})
            Assert.AreEqual(mode,GameStartupSelection.Resolve([argument],new Dictionary<string,string?>()).Mode);
    }
}
