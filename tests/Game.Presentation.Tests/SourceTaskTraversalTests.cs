using Game.Presentation.BattleAnimations;

namespace Game.Presentation.BattleAnimations
{
    public sealed partial class SourceBattleAnimation
    {
        private sealed class TraversalProbe(Action<SourceBattleAnimation,TraversalProbe> step):VisualTask
        {
            public void Retire()=>Finished=true;
            public override void Step(SourceBattleAnimation owner)=>step(owner,this);
        }
        internal string[] ProbeTaskInsertion(bool destroyBeforeCreation)
        {
            var trace=new List<string>();
            TraversalProbe Child(string name)=>new((_,self)=>{trace.Add(name);self.Retire();});
            AddSourceVisualTask(new TraversalProbe((owner,self)=>
            {
                trace.Add("parent");
                if(destroyBeforeCreation)self.Retire();
                owner.AddSourceVisualTask(Child("earlier"),2);
                owner.AddSourceVisualTask(Child("equal"),5);
                owner.AddSourceVisualTask(Child("later"),80);
                self.Retire();
            }),5);
            AddSourceVisualTask(Child("last"),100);
            RunSourceTasks();trace.Add("next-pass");RunSourceTasks();
            return trace.ToArray();
        }
    }
}

namespace FireRed.Tests.Presentation
{
    [TestClass,TestCategory("Game")]
    public sealed class SourceTaskTraversalTests
    {
        private sealed class Audio:ISourceAnimationAudio
        {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
        private static SourceBattleAnimation Runtime()=>new(
            new SourceAnimationScript(1,"task-order",[new("end",[],1)]),
            new("task-order",1,1,new(0),new(1)),
            [new(new(0),"actor",0,0,64,64),new(new(1),"target",0,0,64,64)],false,new Audio());
        [TestMethod]
        public void NewlyInsertedSuccessorsRunInTheSamePassAndEarlierTasksWait()
        {
            var runtime=Runtime();
            CollectionAssert.AreEqual(new[]{"parent","equal","later","last","next-pass","earlier"},runtime.ProbeTaskInsertion(false));
            runtime.Cancel();
        }
        [TestMethod]
        public void CreationAfterCurrentTaskDestructionFailsClosed()
        {
            var runtime=Runtime();
            Assert.ThrowsExactly<InvalidOperationException>(()=>runtime.ProbeTaskInsertion(true));
            runtime.Cancel();
        }
    }
}
