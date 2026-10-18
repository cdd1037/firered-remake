using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass]
public sealed class ProgramCapacityTests
{
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static int Birth(SourceAnimationCommand c)=>c.Op=="createsprite"?1:0;
    [TestMethod]
    public void AlternatePathsUseMaximumAndRepeatedHelpersContributeEachTime()
    {
        var s=new SourceAnimationScript(1,"capacity",[C("jumpargeq","7","0","short"),C("call","two"),C("call","two"),C("goto","done"),C("createsprite"),C("end")]){
            Labels=new Dictionary<string,int>{{"short",4},{"done",5}},Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"two",[C("createsprite"),C("createsprite"),C("return")]}}};
        Assert.AreEqual(4,SourceAnimationProgramDependencies.MaximumCost(s,Birth));
        Assert.AreEqual(11,SourceAnimationProgramDependencies.MaximumCost(s,_=>1));
        var twoTurns=s with{Commands=[C("choosetwoturnanim","a","b"),C("createsprite"),C("end"),C("createsprite"),C("createsprite"),C("end")],Labels=new Dictionary<string,int>{{"a",1},{"b",3}}};
        Assert.AreEqual(2,SourceAnimationProgramDependencies.MaximumCost(twoTurns,Birth));
    }
    [TestMethod]
    public void SharedSuccessorsRetainScopedBranchAndFallthroughSemantics()
    {
        var labels=new Dictionary<string,int>{{"end",4}};
        CollectionAssert.AreEqual(new[]{4,2},SourceAnimationProgramDependencies.Successors(C("jumpifmoveturn","1","end"),1,labels).ToArray());
        CollectionAssert.AreEqual(new[]{4},SourceAnimationProgramDependencies.Successors(C("goto","end"),1,labels).ToArray());
        Assert.AreEqual(0,SourceAnimationProgramDependencies.Successors(C("return"),1,labels).Count());
    }
    [TestMethod]
    public void InvalidUnboundedNegativeAndOverflowingCostsFailClosed()
    {
        var s=new SourceAnimationScript(1,"capacity",[C("end")]);
        Assert.ThrowsExactly<InvalidDataException>(()=>SourceAnimationProgramDependencies.MaximumCost(s,_=>-1));
        Assert.ThrowsExactly<InvalidDataException>(()=>SourceAnimationProgramDependencies.MaximumCost(s with{Commands=[C("goto","loop")],Labels=new Dictionary<string,int>{{"loop",0}}},Birth));
        Assert.ThrowsExactly<InvalidDataException>(()=>SourceAnimationProgramDependencies.MaximumCost(s with{Commands=[C("delay","1")]},Birth));
        Assert.ThrowsExactly<InvalidDataException>(()=>SourceAnimationProgramDependencies.MaximumCost(s with{Commands=[C("call","a"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"a",[C("call","a"),C("return")]}}},Birth));
        Assert.ThrowsExactly<OverflowException>(()=>SourceAnimationProgramDependencies.MaximumCost(s with{Commands=[C("delay","1"),C("end")]},_=>int.MaxValue));
    }
}
