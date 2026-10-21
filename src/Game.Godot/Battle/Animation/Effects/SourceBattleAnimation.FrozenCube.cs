namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string FrozenCubeTemplate="sFrozenIceCubeSpriteTemplate";
    private bool ValidateFrozenCubeCommand(SourceAnimationCommand command)
    {
        if(command.Op!="createvisualtask"||command.Args.FirstOrDefault()!="AnimTask_FrozenIceCube")return false;
        var a=command.Args;if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(command,"Frozen cube source task takes only a byte priority.");
        if(!_script.SpriteDependencies.Contains(FrozenCubeTemplate)||!_script.SpriteRecipes.TryGetValue(FrozenCubeTemplate,out var recipe)){Fail(command,"Frozen cube raw child recipe is missing.");return true;}
        SourceSpriteSubsprite[] pieces=[new(-16,-16,64,64,0,2),new(-16,48,64,32,64,2),new(48,-16,32,64,96,2),new(48,48,32,32,128,2)];
        if(recipe.Asset!="ice-cube"||recipe.Callback!="SpriteCallbackDummy"||recipe.Width!=64||recipe.Height!=64||!recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||recipe.Animations.Length!=1||!recipe.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!recipe.LoopStarts.SequenceEqual(new[]{0})||recipe.FrameLoops.Any(x=>x.Length!=0)||!recipe.Subsprites.SequenceEqual(pieces))Fail(command,"Frozen cube static frame or subsprite table differs.");
        RequireOverlaySpritePalette(command,"ANIM_TAG_ICE_CUBE");
        if(!_validatingInvokedProgram)return true;
        var closure=OverlayInvocationCommands();
        if(closure.Count(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_FrozenIceCube")!=1||closure.Any(c=>c.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim" or "createsprite" or "createsoundtask"||c.Op=="createvisualtask"&&c.Args[0]!="AnimTask_FrozenIceCube"))Fail(command,"Frozen cube requires one bounded raw-child and palette owner.");
        int taskAt=Array.FindIndex(closure,c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_FrozenIceCube");
        if(closure.Take(taskAt).LastOrDefault(c=>(c.Op is "loadspritegfx" or "unloadspritegfx")&&c.Args[0]=="ANIM_TAG_ICE_CUBE")?.Op!="loadspritegfx"||closure.Skip(taskAt+1).Any(c=>c.Op is "loadspritegfx" or "unloadspritegfx" or "setalpha"))Fail(command,"Frozen cube graphics and alpha lifetime differ.");
        int waitAt=Array.FindIndex(closure,taskAt+1,c=>c.Op=="waitforvisualfinish");
        if(waitAt<0||closure.Skip(taskAt+1).Take(waitAt-taskAt-1).Any(c=>c.Op is not("delay" or "playsewithpan" or "waitplaysewithpan")))Fail(command,"Frozen cube register owner must complete before cleanup or another register write.");
        // Reserve the shared conservative persistent-field budget separately for
        // source logical cells (64) and expanded hardware OAM entries (128).
        if(22+1+_spriteScene.Count>64||_sprites.FindFreeSlot(IsReservedSpriteSlot)==64||22+4+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>128)Fail(command,"Frozen cube exceeds native logical sprite or OAM capacity.");
        int tasks=1+_tasks.Count(t=>!t.Finished)+_soundTasks.Count(t=>!t.Finished)+closure.Count(c=>c.Op is "createvisualtask" or "clearmonbg" or "fadetobg" or "restorebg" or "waitplaysewithpan")+closure.Count(c=>c.Op=="monbg")*(_doubles?2:1);
        if(tasks>16)Fail(command,"Frozen cube exceeds native task capacity.");
        ValidateReactionGeometry(command,_battlers[_cue.Target.Value]);return true;
    }
    private bool TryCreateFrozenCubeTask(SourceAnimationCommand command)
    {
        if(command.Args[0]!="AnimTask_FrozenIceCube")return false;
        if(!_loaded.Contains("ANIM_TAG_ICE_CUBE")||FindSpritePalette(_script.SpriteTags["ANIM_TAG_ICE_CUBE"])==255)Fail(command,"Frozen cube needs its loaded indexed source bank.");
        int slot=_sprites.FindFreeSlot(IsReservedSpriteSlot);if(slot==64)Fail(command,"Frozen cube has no raw sprite cell.");
        _tasks.Add(new FrozenCubeTask(this,slot));return true;
    }
    private sealed class FrozenCubeSprite(int slot,SourceSpriteRecipe recipe,PixelPoint position):VisualSprite(slot,4)
    {
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner){}
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,default,0,true,false,true,256,recipe.Asset,Subpriority){Width=64,Height=64,OamPriority=2,Subsprites=recipe.Subsprites};
    }
    // status_effects.c:350–449. The task's raw child has no visual count; its
    // sprite/OAM lifetime and palette slot are owned by the shared scene.
    private sealed class FrozenCubeTask:VisualTask
    {
        private readonly FrozenCubeSprite _child;private readonly int _tag;private int _phase,_timer,_paletteTick,_rotations,_cycles;
        public FrozenCubeTask(SourceBattleAnimation owner,int slot)
        {
            _tag=owner._script.SpriteTags["ANIM_TAG_ICE_CUBE"];var p=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _child=new(slot,owner._script.SpriteRecipes[FrozenCubeTemplate],new(unchecked((short)(p.X-32)),unchecked((short)(p.Y-36))));owner._sprites.Add(_child);owner.SetAlphaBlend(0,63,0,16);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:if(++_timer==10){_phase=1;_timer=0;}else owner.SetAlphaBlend(0,63,_timer,16-_timer);break;
                case 1:
                    if(_timer++>13&&++_paletteTick==3)
                    {
                        int slot=owner.FindSpritePalette(_tag);var bank=owner._spritePaletteBanks[slot];owner.WriteFadedSpritePalette(slot,13,new[]{bank[14],bank[15],bank[13]});_paletteTick=0;
                        if(++_rotations==3){_rotations=0;_timer=0;if(++_cycles==2){_timer=9;_phase=2;}}
                    }
                    break;
                case 2:if(--_timer==-1){_phase=3;_timer=0;}else owner.SetAlphaBlend(0,63,_timer,16-_timer);break;
                default:if(++_timer==37)owner._sprites.Remove(_child);else if(_timer==39){owner.ClearAlphaBlend();owner._blendSecondTargets=0;Finished=true;}break;
            }
        }
    }
}
