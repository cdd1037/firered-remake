using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceSoundPlayerPolicyTests
{
    [TestMethod]
    public void NamedSeWrapperRequiresItsActualSourceSongPlayer()
    {
        var ordinary=new SourceAnimationSound("source-song",0,1);
        ordinary.ValidateSourcePlayer(1);ordinary.ValidateSourcePlayer(2);
        foreach(int player in new[]{1,2})
        {
            var explicitPlayer=ordinary with{PanningPlayer=player};explicitPlayer.ValidateSourcePlayer(player);
            Assert.ThrowsExactly<InvalidOperationException>(()=>explicitPlayer.ValidateSourcePlayer(player==1?2:1));
        }
        foreach(int invalid in new[]{0,3,4})Assert.ThrowsExactly<InvalidOperationException>(()=>ordinary.ValidateSourcePlayer(invalid));
    }
}
