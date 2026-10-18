namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string TrickBag="gTrickBagSpriteTemplate",RecycleArrow="gRecycleSpriteTemplate";
    private bool ValidateItemWindCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";if(c.Op!="createsprite"||n is not(TrickBag or RecycleArrow))return false;
        bool bag=n==TrickBag;
        if(a.Length!=(bag?5:3)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||bag&&(Number(a[3]) is < -256 or >256||Number(a[4]) is <0 or >255))Fail(c,"Item sprite position, phase or operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Item sprite recipe missing.");return true;}
        if(r.Callback!=(bag?"AnimTrickBag":"AnimRecycle")||r.Asset!=(bag?"item-bag":"recycle")||r.Width!=(bag?32:64)||r.Height!=r.Width||r.Blend==bag||r.AffineDoubleSize||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0)||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,bag?30:1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{bag?-1:0})||r.AffineAnimations.Length!=(bag?4:1))Fail(c,"Item sprite source frame, shape or affine tables differ.");
        SourceSpriteAffineCommand F(int y,int rotation,int duration)=>new("frame",0,y,rotation&255,duration,0);
        SourceSpriteAffineCommand End()=>new("end",0,0,0,0,0);
        SourceSpriteAffineCommand[][] programs=bag?[[F(0,0,3),End()],[F(-10,0,3),F(-6,0,3),F(-2,0,3),F(0,0,3),F(2,0,3),F(6,0,3),F(10,0,3),End()],[F(0,-4,10),F(0,4,20),F(0,-4,10),End()],[F(0,-1,2),F(0,1,4),F(0,-1,4),F(0,1,4),F(0,-1,4),F(0,1,2),End()]]:[[F(0,-4,64),new("jump",0,0,0,0,0)]];
        for(int i=0;i<programs.Length;i++)if(!r.AffineAnimations[i].SequenceEqual(programs[i]))Fail(c,"Item source affine program differs.");
        if(bag&&_script.SineTable.Length!=320)Fail(c,"Trick sine table missing.");
        if(_validatingInvokedProgram){if(!bag)ValidateReactionGeometry(c,_battlers[_cue.Actor.Value]);ValidateItemWindCapacity(c);}
        return true;
    }
    private void ValidateItemWindCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();bool recycle=c.Args[0]==RecycleArrow,bag=c.Args[0]==TrickBag;
        bool Sprite(string n)=>recycle?n==RecycleArrow:bag?n==TrickBag:n is "gSilverWindBigSparkSpriteTemplate" or "gSilverWindMediumSparkSpriteTemplate" or "gSilverWindSmallSparkSpriteTemplate";
        bool Task(string n)=>recycle?n=="AnimTask_BlendMonInAndOut":bag?n is "AnimTask_StretchAttackerUp" or "AnimTask_StretchTargetUp" or "AnimTask_ShakeMon":n is "AnimTask_BlendBattleAnimPalExclude" or "AnimTask_GetTargetSide" or "AnimTask_StartSlidingBg" or "AnimTask_BlendBattleAnimPal";
        if(commands.Any(x=>x.Op=="createsprite"&&!Sprite(x.Args[0])||x.Op=="createvisualtask"&&!Task(x.Args[0]))||recycle&&commands.Count(x=>x.Op=="createsprite")!=1)Fail(c,"Item/wind companion owners exceed their source closure.");
        // Acyclic programs are validated centrally. Count every branch and every
        // birth without credit for destruction, including both exclusive BG paths.
        int births=commands.Count(x=>x.Op=="createsprite"),retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length));
        if(22+births+retained>64||4+births+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32)Fail(c,"Item/wind closure exceeds source OAM or matrix capacity.");
        int tasks=1+commands.Count(x=>x.Op=="createvisualtask"||x.Op is "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone")+commands.Count(x=>x.Op=="monbg")*(_doubles?2:1)+commands.Count(x=>x.Op=="clearmonbg");
        if(tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Item/wind closure exceeds source task capacity.");
        if(recycle&&commands.Any(x=>x.Op is "fadetobg" or "restorebg" or "changebg" or "monbg_static"||x.Op=="setalpha"&&!x.Args.SequenceEqual(new[]{"0","16"})))Fail(c,"Recycle requires its single source alpha owner.");
    }
    private bool TryCreateItemWindSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(TrickBag or RecycleArrow))return false;var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains(c.Args[0]==TrickBag?"ANIM_TAG_ITEM_BAG":"ANIM_TAG_RECYCLE"))Fail(c,"Item graphics not loaded.");
        _sprites.Add(new ItemWindSprite(slot,priority,r,this));return true;
    }
    private sealed class ItemWindSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram _affine;
        private readonly short[] _d=new short[8];private PixelPoint _position,_offset;private int _phase,_subpriority;private bool _invisible,_restart;
        private static readonly (int Speed,int Duration,int Direction)[] Coordinates=[(5,24,1),(0,4,0),(8,16,-1),(0,2,0),(8,16,1),(0,2,0),(8,16,1),(0,2,0),(8,16,1),(0,16,0),(0,0,127)];
        public ItemWindSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);_affine=new(r.AffineAnimations[0]);_subpriority=priority;
            if(r.Template==TrickBag){_d[1]=o._animationArgs[1];_d[2]=o._animationArgs[0];_d[4]=20;_position=new(120,_d[2]);UpdateWave(o);}
            else{var b=o._battlers[o._cue.Actor.Value];var p=b.Center(o._doubles,true);_position=new(p.X,Math.Max(16,p.Y-b.SourceHeight/2));_d[7]=16;o._alphaA=0;o._alphaB=16;}
        }
        private static short S(int x)=>unchecked((short)x);
        private void UpdateWave(SourceBattleAnimation o){_offset=new(o._script.SineTable[_d[1]+64]*60>>8,o._script.SineTable[_d[1]]*20>>8);_subpriority=_d[1]>0&&_d[1]<192?31:29;}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();if(_restart)_restart=false;else _affine.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==RecycleArrow){Recycle(o);return;}
            if(_phase==0)
            {
                if(_d[3]==0){if(_d[2]>78){_d[3]=1;_affine=new(_recipe.AffineAnimations[1]);_restart=true;}else{_d[2]=S(_d[2]+_d[4]/10);_d[4]=S(_d[4]+3);_position=new(_position.X,_d[2]);}}
                else if(_affine.Ended){_d[0]=_d[2]=0;_phase=1;}return;
            }
            if(_phase==1)
            {
                var row=Coordinates[_d[0]];if(_d[2]==row.Duration){if(row.Direction==127){_d[0]=0;_phase=2;}_d[2]=0;_d[0]++;}
                else{_d[2]++;_d[1]=S((row.Speed*row.Direction+_d[1])&255);UpdateWave(o);}return;
            }
            if(_d[0]>20)Finished=true;_invisible=(_d[0]&1)!=0;_d[0]++;
        }
        private void Recycle(SourceBattleAnimation o)
        {
            switch(_d[2])
            {
                case 0:case 2:
                    if(++_d[0]>1){_d[0]=0;bool fadeIn=_d[2]==0;if((_d[1]&1)==0){if(fadeIn?_d[6]<16:_d[6]!=0)_d[6]+=(short)(fadeIn?1:-1);}else if(fadeIn?_d[7]!=0:_d[7]<16)_d[7]+=(short)(fadeIn?-1:1);_d[1]++;o._alphaA=_d[6];o._alphaB=_d[7];if(_d[7]==(fadeIn?0:16))_d[2]++;}break;
                case 1:if(++_d[0]==10){_d[0]=_d[1]=0;_d[2]++;}break;
                case 3:Finished=true;break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,_recipe.Blend,256,_recipe.Asset,_subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,OamPriority=2,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
