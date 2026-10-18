namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string WhirlwindLineTemplate="gWhirlwindLineSpriteTemplate";
    private bool ValidateExpulsionCommand(SourceAnimationCommand c)
    {
        var a=c.Args;if(a.Length==0)return false;
        if(c.Op=="loadspritegfx"&&a[0]=="ANIM_TAG_WHIRLWIND_LINES")
        {if(a.Length!=1)Fail(c,"Whirlwind graphics operands differ.");return true;}
        if(c.Op=="playsewithpan"&&a[0]=="SE_M_TELEPORT")
        {if(a.Length!=2||SoundPan(a[1]) is < -64 or >63||!_script.SoundEffects.ContainsKey(a[0]))Fail(c,"Teleport sound operands differ.");return true;}
        if(c.Op=="createsprite"&&a[0]==WhirlwindLineTemplate)
        {
            if(a.Length!=8||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Take(2).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >short.MaxValue||Number(a[7]) is <0 or >4)Fail(c,"Whirlwind line operands differ.");
            if(!_script.SpriteRecipes.TryGetValue(a[0],out var r)||r.Callback!="AnimWhirlwindLine"||r.Asset!="whirlwind-lines"||r.Width!=32||r.Height!=16||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new(0,1,false,false),new(8,1,false,false),new(16,1,false,false),new(8,1,true,false),new(0,1,true,false)])||!r.LoopStarts.SequenceEqual([-1])||r.FrameLoops.Any(l=>l.Length!=0))Fail(c,"Whirlwind line source frame program differs.");
            return true;
        }
        if(c.Op!="createvisualtask")return false;
        if(a[0]=="AnimTask_SlideOffScreen")
        {if(a.Length!=4||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >3||Number(a[3]) is <1 or >short.MaxValue)Fail(c,"Slide-off-screen task operands differ.");if(_validatingInvokedProgram&&Number(a[2])<2&&SourceAnimBattlerSlot(Number(a[2])) is null)Fail(c,"Slide-off-screen requires a present source battler sprite.");return true;}
        if(a[0]!="AnimTask_Teleport")return false;
        if(a.Length!=2||Number(a[1]) is <0 or >255||!_script.TaskAffinePrograms.TryGetValue("sAffineAnim_Teleport",out var program)||!program.SequenceEqual([new SourceTaskAffineCommand("frame",64,-4,0,20),new SourceTaskAffineCommand("frame",0,0,0,200),new SourceTaskAffineCommand("end")]))Fail(c,"Teleport source affine program differs.");
        if(_validatingInvokedProgram&&SourceAnimBattlerSlot(0) is null)Fail(c,"Teleport requires a present source attacker sprite.");
        return true;
    }
    private bool TryCreateExpulsionSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=WhirlwindLineTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_WHIRLWIND_LINES"))Fail(c,"Whirlwind graphics have not been loaded.");
        _sprites.Add(new WhirlwindLineSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private bool TryCreateExpulsionTask(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(a[0]=="AnimTask_SlideOffScreen")
        {if(SourceAnimBattlerSlot(Number(a[2])) is {} slot)_tasks.Add(new SlideOffScreenTask(slot,_cue.Target.IsPlayer?-Number(a[3]):Number(a[3])));return true;}
        if(a[0]!="AnimTask_Teleport")return false;
        if(SourceAnimBattlerSlot(0) is {} actor)_tasks.Add(new TeleportTask(this,actor));return true;
    }
    private sealed class SlideOffScreenTask(int slot,int speed):VisualTask
    {
        public override void Step(SourceBattleAnimation o)
        {
            var p=o._offsets[slot];o._offsets[slot]=new(unchecked((short)(p.X+speed)),p.Y);
            int x=o.BattlerPosition(slot).X+o._offsets[slot].X;if(x< -32||x>272)Finished=true;
        }
    }
    private sealed class TeleportTask:VisualTask
    {
        private readonly int _slot;private readonly SourceTaskAffineProgram _affine;private int _phase,_ticks,_lifts;
        public TeleportTask(SourceBattleAnimation o,int slot)
        {_slot=slot;_lifts=(slot&1)==0?8:4;_affine=new(o._script.TaskAffinePrograms["sAffineAnim_Teleport"]);o.PrepareTaskAffine(slot,_affine);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0){o.RunTaskAffine(_slot,_affine);if(++_ticks>19)_phase=1;return;}
            if(_lifts!=0){var p=o._offsets[_slot];o._offsets[_slot]=new(p.X,unchecked((short)(p.Y-8)));_lifts--;return;}
            o.SetSourceBattlerSpriteInvisible(_slot,true);o.SetBattlerPosition(_slot,new(272,o.BattlerPosition(_slot).Y));o.ResetBattlerRotScale(_slot);Finished=true;
        }
    }
    private sealed class WhirlwindLineSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private PixelPoint _offset;private short _phase,_remaining;private bool _restart;
        public WhirlwindLineSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;int selected=a[2]==0?o._cue.Actor.Value:o._cue.Target.Value;var p=o._battlers[selected].Base(o._doubles);
            _position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0]+((selected&1)==0?8:0)-32)),unchecked((short)(p.Y+a[1])));
            _phase=(short)a[4];_offset=new(unchecked((short)(12*(ushort)a[4])),0);_remaining=(short)a[3];
            _frames=new(recipe);_frames.SeekFrame(a[4]);_frames.Step(); // Seek clears animBeginning, so creation advances ContinueAnim once.
        }
        public override void Step(SourceBattleAnimation o)
        {
            _offset=new(unchecked((short)(_offset.X+12)),_offset.Y);
            _phase=unchecked((short)(_phase+1));if(_phase==6){_phase=0;_offset=new(0,_offset.Y);_frames=new(_recipe);_restart=true;}
            _remaining=unchecked((short)(_remaining-1));if(_remaining==-1)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_restart)_restart=false;else _frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=16,VFlip=_frames.Frame.VFlip};
    }
}
