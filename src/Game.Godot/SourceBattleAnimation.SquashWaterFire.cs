namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string EruptionLaunch="AnimTask_EruptionLaunchRocks",EruptionRock="gEruptionLaunchRockSpriteTemplate",EruptionFallingRock="gEruptionFallingRockSpriteTemplate";
    private const string WaterSpoutLaunch="AnimTask_WaterSpoutLaunch",WaterSpoutRain="AnimTask_WaterSpoutRain",SmallWaterOrb="gSmallWaterOrbSpriteTemplate",WaterSpoutHit="gWaterHitSplatSpriteTemplate";
    private static bool SquashWaterFireTask(string n)=>n is EruptionLaunch or WaterSpoutLaunch or WaterSpoutRain;
    private bool ValidateSquashWaterFireCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";bool task=c.Op=="createvisualtask"&&SquashWaterFireTask(n),sprite=c.Op=="createsprite"&&n==EruptionFallingRock;
        if(!task&&!sprite)return false;
        if(a.Length!=(task?2:8)||Number(a[task?1:2]) is <0 or >255||sprite&&(Number(a[1]) is <0 or >1||Number(a[2])>127)||a.Skip(task?2:3).Any(x=>AnimationArgument(x) is <short.MinValue or >short.MaxValue))Fail(c,"Squash callback operand width differs.");
        if(sprite&&(Number(a[3]) is <-32 or >272||Number(a[4]) is <-128 or >120||Number(a[5]) is <0 or >32767||Number(a[6])<Number(a[4])||Number(a[6])>120||Number(a[7]) is <0 or >3))Fail(c,"Falling rock bounded coordinates, delay or tile variant differ.");
        if(_script.SineTable.Length!=320)Fail(c,"Squash source sine table is missing.");
        if(sprite)ValidateSquashWaterFireRecipe(c,n);
        if(task)
        {
            string child=n==EruptionLaunch?EruptionRock:SmallWaterOrb;
            if(!_script.SpriteDependencies.Contains(child))Fail(c,"Squash raw child dependency missing.");
            ValidateSquashWaterFireRecipe(c,child);
            if(n==WaterSpoutRain){if(!_script.SpriteDependencies.Contains(WaterSpoutHit))Fail(c,"Water rain splash dependency missing.");ValidateSquashWaterFireRecipe(c,WaterSpoutHit);}
        }
        if(_validatingInvokedProgram)
        {
            if(n is WaterSpoutLaunch or WaterSpoutRain&&(_cue.ActorHpQuarterTier is null or >3||_cue.MovePower is null))Fail(c,"Water Spout requires committed entry HP tier and source move power.");
            if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value))Fail(c,"Squash animation requires present actor and target.");
            foreach(var b in _battlers.Values)ValidateReactionGeometry(c,b);
            ValidateSquashWaterFireCapacity(c);
        }
        return true;
    }
    private void ValidateSquashWaterFireRecipe(SourceAnimationCommand c,string n)
    {
        if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Squash source recipe missing.");return;}
        var expected=n switch{EruptionRock=>("warm-rock","AnimEruptionLaunchRock",16,16,false),EruptionFallingRock=>("warm-rock","AnimEruptionFallingRock",32,32,false),SmallWaterOrb=>("glowy-blue-orb","AnimSmallWaterOrb",8,8,false),_=>("water-impact","AnimHitSplatBasic",32,32,true)};
        if((r.Asset,r.Callback,r.Width,r.Height,r.Blend)!=expected||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(f=>f.Length!=0))Fail(c,"Squash source recipe shape, material or frame control differs.");
        if(r.AffineAnimations.Length!=(n==WaterSpoutHit?4:0))Fail(c,"Squash source affine table count differs.");
        if(n==WaterSpoutHit)for(int i=0;i<4;i++)
        {
            SourceSpriteAffineCommand hold=new("frame",0,0,0,8,0),end=new("end",0,0,0,0,0);
            SourceSpriteAffineCommand[] p=i==0?[hold,end]:[new("frame",i==1?216:i==2?176:128,i==1?216:i==2?176:128,0,0,0),hold,end];
            if(!r.AffineAnimations[i].SequenceEqual(p))Fail(c,"Water splash source affine program differs.");
        }
    }
    private void ValidateSquashWaterFireCapacity(SourceAnimationCommand command)
    {
        int sprites=0,matrices=0,launchers=0,rain=0,tasks=1,maxSprites=0,maxMatrices=0;
        foreach(var c in OverlayInvocationCommands())
        {
            // This proof is sequential: branches could skip a visual drain.
            if(c.Op is "call" or "return" or "goto" or "choosetwoturnanim"||c.Op.StartsWith("jump",StringComparison.Ordinal))Fail(command,"Squash capacity proof requires linear source control flow.");
            // Launch tasks stay counted until their actual raw-child count is zero.
            // Each seven-rock wave therefore drains before the next wave starts.
            // Delayed sounds/background updaters need not drain at visual waits.
            if(c.Op=="waitforvisualfinish"){sprites=matrices=launchers=rain=0;continue;}
            if(c.Op=="createsprite")
            {if(c.Args[0] is not (EruptionFallingRock or "gSimplePaletteBlendSpriteTemplate"))Fail(command,"Squash companion sprite ownership exceeds reviewed closure.");sprites++;}
            if(c.Op=="createvisualtask")
            {
                string n=c.Args[0];if(!SquashWaterFireTask(n)&&n!="AnimTask_HorizontalShake")Fail(command,"Squash companion task ownership exceeds reviewed closure.");tasks++;
                if(n is EruptionLaunch or WaterSpoutLaunch){if(++launchers>1||rain!=0)Fail(command,"Overlapping squash raw emitters.");sprites+=n==EruptionLaunch?7:20;}
                if(n==WaterSpoutRain){if(++rain>1||launchers!=0)Fail(command,"Overlapping Water Spout rain ownership.");sprites+=21;matrices+=20;tasks+=2;}
            }
            if(c.Op is "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "fadetobg" or "restorebg" or "clearmonbg")tasks++;
            if(c.Op=="monbg")tasks+=_doubles?2:1;
            maxSprites=Math.Max(maxSprites,sprites);maxMatrices=Math.Max(maxMatrices,matrices);
        }
        if(22+maxSprites+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+maxMatrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(command,"Squash closure exceeds native object, matrix or task capacity.");
    }
    private bool TryCreateSquashWaterFireTask(SourceAnimationCommand c)
    {
        string n=c.Args[0];if(!SquashWaterFireTask(n))return false;
        string tag=n==EruptionLaunch?"ANIM_TAG_WARM_ROCK":"ANIM_TAG_GLOWY_BLUE_ORB";
        if(!_loaded.Contains(tag)||n==WaterSpoutRain&&!_loaded.Contains("ANIM_TAG_WATER_IMPACT"))Fail(c,"Squash raw child material is not loaded.");
        VisualTask task=n switch{EruptionLaunch=>new EruptionLaunchTask(this),WaterSpoutLaunch=>new WaterSpoutLaunchTask(this),_=>new WaterSpoutRainTask(this)};
        _tasks.Add(task);return true;
    }
    private bool TryCreateSquashWaterFireSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=EruptionFallingRock)return false;
        if(!_loaded.Contains("ANIM_TAG_WARM_ROCK"))Fail(c,"Falling rock material is not loaded.");
        _sprites.Add(new EruptionFallingRockVisual(slot,priority,_script.SpriteRecipes[EruptionFallingRock],this));return true;
    }
    private static short SquashShort(int n)=>unchecked((short)n);
    private static void SetSourceSquash(short[] d,int slot,int fromX,int fromY,int toX,int toY,int duration)
    {d[8]=SquashShort(duration);d[15]=SquashShort(slot);d[9]=SquashShort(fromX);d[10]=SquashShort(fromY);d[13]=SquashShort(toX);d[14]=SquashShort(toY);d[11]=SquashShort((toX-fromX)/duration);d[12]=SquashShort((toY-fromY)/duration);}
    private bool RunSourceSquash(short[] d)
    {
        if(d[8]==0)return false;
        if(--d[8]!=0){d[9]=SquashShort(d[9]+d[11]);d[10]=SquashShort(d[10]+d[12]);}else{d[9]=d[13];d[10]=d[14];}
        int slot=d[15];SetBattlerRotScale(slot,d[9],d[10],0);
        _offsets[slot]=new(_offsets[slot].X,d[8]!=0?SourceTaskAffineProgram.FootOffset(_yScale[slot],_battlers[slot].YOffset):0);return d[8]!=0;
    }
    private static void SquashMoveY(SourceBattleAnimation o,int slot,int delta)
    {var p=o.BattlerPosition(slot);o.SetBattlerPosition(slot,new(p.X,SquashShort(p.Y+delta)));}
    private static void SquashOffset(SourceBattleAnimation o,int slot,int? x=null,int? y=null)
    {var p=o._offsets[slot];o._offsets[slot]=new(SquashShort(x??p.X),SquashShort(y??p.Y));}
    private sealed class EruptionLaunchTask:VisualTask
    {
        internal readonly short[] Data=new short[16];
        public EruptionLaunchTask(SourceBattleAnimation o){Data[15]=SquashShort(o._cue.Actor.Value);Data[4]=SquashShort(o.BattlerPosition(Data[15]).Y);Data[5]=(short)(o._cue.Actor.IsPlayer?0:1);o.PrepareBattlerRotScale(Data[15],0);}
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;int b=d[15];
            switch(d[0])
            {
                case 0:SetSourceSquash(d,b,256,256,224,512,32);d[0]++;goto case 1;
                case 1:
                    if(++d[1]>1){d[1]=0;SquashOffset(o,b,x:(++d[2]&1)!=0?3:-3);}
                    if(d[5]!=0&&++d[3]>4){d[3]=0;SquashMoveY(o,b,1);}
                    if(!o.RunSourceSquash(d)){o.SetBattlerYOffsetFromScale(b);SquashOffset(o,b,x:0);d[1]=d[2]=d[3]=0;d[0]++;}break;
                case 2:if(++d[1]>4){SetSourceSquash(d,b,224,512,384,d[5]!=0?240:192,6);d[1]=0;d[0]++;}break;
                case 3:if(!o.RunSourceSquash(d)){Launch(o);d[0]++;}break;
                case 4:
                    if(++d[1]>1){d[1]=0;SquashOffset(o,b,y:o._offsets[b].Y+((++d[2]&1)!=0?3:-3));}
                    if(++d[3]>24){SetSourceSquash(d,b,384,d[5]!=0?240:192,256,256,8);if((d[2]&1)!=0)SquashOffset(o,b,y:o._offsets[b].Y-3);d[1]=d[2]=d[3]=0;d[0]++;}break;
                case 5:
                    if(d[5]!=0)SquashMoveY(o,b,-1);
                    if(!o.RunSourceSquash(d)){var p=o.BattlerPosition(b);o.SetBattlerPosition(b,new(p.X,d[4]));o.ResetBattlerRotScale(b);d[2]=0;d[0]++;}break;
                case 6:if(d[6]==0)Finished=true;break;
            }
        }
        private void Launch(SourceBattleAnimation o)
        {
            int b=Data[15],sign=Data[5]==0?1:-1;var p=o.BattlerPosition(b);
            // PrepareBattlerSpriteForRotScale selects DOUBLE: native 64x64 center is -64.
            p=new(SquashShort(p.X+(sign==1?-12:16)),SquashShort(p.Y+o._offsets[b].Y-64+(sign==1?74:44)));
            (int X,int Y)[] speeds=[(-2,-5),(-1,-1),(3,-6),(4,-2),(2,-8),(-5,-5),(4,-7)];
            for(int i=0,j=0;i<speeds.Length;i++)
            {
                int slot=o.FindFreeSpriteSlot();if(slot==64)continue;
                o._sprites.Add(new EruptionLaunchRockVisual(slot,o._script.SpriteRecipes[EruptionRock],p,speeds[i].X*sign,speeds[i].Y,64+4*j,this));o._oamY[slot]=0;j=(j+1)%5;Data[6]++;
            }
        }
    }
    private sealed class EruptionLaunchRockVisual:VisualSprite
    {
        internal readonly short[] Data=new short[8];private PixelPoint _position;private readonly SourceSpriteRecipe _recipe;private readonly int _tile;private readonly EruptionLaunchTask _parent;
        public EruptionLaunchRockVisual(int slot,SourceSpriteRecipe recipe,PixelPoint position,int vx,int vy,int tile,EruptionLaunchTask parent):base(slot,2)
        {_recipe=recipe;_position=position;_tile=tile;_parent=parent;Data[2]=SquashShort(position.X*8);Data[3]=SquashShort(position.Y*8);Data[4]=SquashShort(vx*8);Data[5]=SquashShort(vy*8);Data[7]=6;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;if(++d[0]>2){d[0]=0;d[1]++;d[3]=SquashShort(d[3]+(ushort)d[1]*(ushort)d[1]);}
            d[2]=SquashShort(d[2]+d[4]);d[3]=SquashShort(d[3]+d[5]);_position=new(d[2]>>3,d[3]>>3);
            if(_position.X is <-8 or >248||_position.Y is <-8 or >120){_parent.Data[6]--;Finished=true;}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,EruptionRock,_position,default,_tile,!Finished,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
    }
    private sealed class EruptionFallingRockVisual:VisualSprite
    {
        internal readonly short[] Data=new short[8];private PixelPoint _position,_offset;private readonly SourceSpriteRecipe _recipe;private readonly int _tile;
        public EruptionFallingRockVisual(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {_recipe=recipe;var a=o._animationArgs;_position=new(a[0],a[1]);Data[6]=a[2];Data[7]=a[3];_tile=a[4]*16;}
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;switch(d[0])
            {
                case 0:if(d[6]!=0){d[6]--;return;}d[0]++;goto case 1;
                case 1:_position=new(_position.X,SquashShort(_position.Y+8));if(_position.Y>=d[7]){_position=new(_position.X,d[7]);d[0]++;}break;
                case 2:if(++d[1]>1){d[1]=0;_offset=new(0,(++d[2]&1)!=0?-3:3);}if(++d[3]>16)Finished=true;break;
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,EruptionFallingRock,_position,_offset,_tile,true,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32};
    }
    private sealed class WaterSpoutLaunchTask:VisualTask
    {
        internal readonly short[] Data=new short[16];
        public WaterSpoutLaunchTask(SourceBattleAnimation o){Data[15]=SquashShort(o._cue.Actor.Value);Data[5]=SquashShort(o.BattlerPosition(Data[15]).Y);Data[1]=o._cue.ActorHpQuarterTier!.Value;o.PrepareBattlerRotScale(Data[15],0);}
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;int b=d[15];switch(d[0])
            {
                case 0:SetSourceSquash(d,b,256,256,224,512,32);d[0]++;goto case 1;
                case 1:
                    if(++d[3]>1){d[3]=0;if((++d[4]&1)!=0){SquashOffset(o,b,x:3);SquashMoveY(o,b,1);}else SquashOffset(o,b,x:-3);}
                    if(!o.RunSourceSquash(d)){o.SetBattlerYOffsetFromScale(b);SquashOffset(o,b,x:0);d[3]=d[4]=0;d[0]++;}break;
                case 2:if(++d[3]>4){SetSourceSquash(d,b,224,512,384,224,8);d[3]=0;d[0]++;}break;
                case 3:if(!o.RunSourceSquash(d)){d[3]=d[4]=0;d[0]++;}break;
                case 4:Launch(o);d[0]++;goto case 5;
                case 5:
                    if(++d[3]>1){d[3]=0;SquashOffset(o,b,y:o._offsets[b].Y+((++d[4]&1)!=0?2:-2));if(d[4]==10){SetSourceSquash(d,b,384,224,256,256,8);d[3]=d[4]=0;d[0]++;}}break;
                case 6:SquashMoveY(o,b,-1);if(!o.RunSourceSquash(d)){o.ResetBattlerRotScale(b);var p=o.BattlerPosition(b);o.SetBattlerPosition(b,new(p.X,d[5]));d[4]=0;d[0]++;}break;
                case 7:if(d[2]==0)Finished=true;break;
            }
        }
        private void Launch(SourceBattleAnimation o)
        {
            var body=o._battlers[o._cue.Actor.Value];var p=body.Center(o._doubles,true);int angle=172,increment=Math.Max(1,4-Data[1]);
            for(int i=0;i<20;i+=increment)
            {
                int slot=o.FindFreeSpriteSlot();if(slot<64)
                {
                    var child=new WaterSpoutOrbVisual(slot,body.Subpriority-1,o._script.SpriteRecipes[SmallWaterOrb],p,i,(o.SampleSourceSine(angle+64)*64)>>8,(o.SampleSourceSine(angle)*64)>>8,this);
                    o._sprites.Add(child);o._oamY[slot]=0;if((Data[2]&1)!=0)o.StepSourceSprite(child);Data[2]++;
                }
                angle=(angle+increment*2)&255;
            }
        }
    }
    private sealed class WaterSpoutOrbVisual:VisualSprite
    {
        internal readonly short[] Data=new short[8];private PixelPoint _position;private readonly SourceSpriteRecipe _recipe;private readonly WaterSpoutLaunchTask _parent;
        public WaterSpoutOrbVisual(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint p,int i,int vx,int vy,WaterSpoutLaunchTask parent):base(slot,priority)
        {_recipe=recipe;_position=p;_parent=parent;Data[1]=SquashShort(i);Data[2]=SquashShort(p.X*16);Data[3]=SquashShort(p.Y*16);Data[4]=SquashShort(vx);Data[5]=SquashShort(vy);Data[7]=2;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;if(d[0]==0){d[4]=SquashShort(d[4]+d[1]%6*3);d[5]=SquashShort(d[5]+d[1]%3*3);d[0]++;}
            d[2]=SquashShort(d[2]+d[4]);d[3]=SquashShort(d[3]+d[5]);_position=new(d[2]>>4,d[3]>>4);
            if(_position.X is <-8 or >248||_position.Y is <-8 or >120){_parent.Data[2]--;Finished=true;}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,SmallWaterOrb,_position,default,0,true,false,false,256,_recipe.Asset,Subpriority){Width=8,Height=8};
    }
    private sealed class WaterSpoutRainTask:VisualTask
    {
        internal readonly short[] Data=new short[16];
        public WaterSpoutRainTask(SourceBattleAnimation o){Data[1]=o._cue.ActorHpQuarterTier!.Value;Data[4]=(short)(o._cue.Actor.IsPlayer?136:16);Data[6]=(short)(o._cue.Actor.IsPlayer?40:80);Data[5]=98;Data[7]=SquashShort(Data[4]+49);Data[12]=SquashShort(Data[1]*5+5);}
        public override void Step(SourceBattleAnimation o)
        {
            var d=Data;switch(d[0])
            {
                case 0:
                    if(++d[2]>2){d[2]=0;Emit(o);}
                    if(d[10]!=0&&d[13]==0)
                    {
                        o._animationArgs[0]=1;o._animationArgs[1]=0;o._animationArgs[2]=12;
                        if(o.CreateHorizontalShakeTask() is {} target)o.AddSourceVisualTask(target,80);
                        o._animationArgs[0]=3;if(o.CreateHorizontalShakeTask() is {} partner)o.AddSourceVisualTask(partner,80);d[13]=1;
                    }
                    if(d[11]>=d[12])d[0]++;break;
                case 1:if(d[9]==0)Finished=true;break;
            }
        }
        private void Emit(SourceBattleAnimation o)
        {
            var d=Data;int y=((o._script.SineTable[d[8]]+3)>>4)+d[6],slot=o.FindFreeSpriteSlot();
            if(slot<64){o._sprites.Add(new WaterSpoutRainVisual(slot,o._script.SpriteRecipes[SmallWaterOrb],new(d[7],0),y,this));o._oamY[slot]=0;d[9]++;}
            d[11]++;d[8]=SquashShort((d[8]+39)&255);
            // ISO_RANDOMIZE2 promotes signed16 to signed32; native wraps before signed remainder.
            d[7]=SquashShort(unchecked(1103515245*d[7]+12345)%d[5]+d[4]);
        }
    }
    private sealed class WaterSpoutRainVisual:VisualSprite
    {
        internal readonly short[] Data=new short[8];private PixelPoint _position;private readonly SourceSpriteRecipe _recipe;private readonly WaterSpoutRainTask _parent;
        public WaterSpoutRainVisual(int slot,SourceSpriteRecipe recipe,PixelPoint p,int y,WaterSpoutRainTask parent):base(slot,0){_recipe=recipe;_position=p;_parent=parent;Data[5]=SquashShort(y);Data[7]=9;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            _position=new(_position.X,SquashShort(_position.Y+8));if(_position.Y<Data[5])return;
            _parent.Data[10]=1;int slot=o.RequireFreeSpriteSlot();Data[1]=SquashShort(slot);
            o._sprites.Add(new WaterSpoutHitVisual(slot,o._script.SpriteRecipes[WaterSpoutHit],_position,_parent));o._oamY[slot]=0;Finished=true;
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,SmallWaterOrb,_position,default,0,true,false,false,256,_recipe.Asset,Subpriority){Width=8,Height=8};
    }
    private sealed class WaterSpoutHitVisual:VisualSprite
    {
        internal readonly short[] Data=new short[8];private readonly SourceSpriteRecipe _recipe;private readonly PixelPoint _position;private readonly WaterSpoutRainTask _parent;private bool _invisible;private SourceSpriteAffineProgram? _affine;
        public WaterSpoutHitVisual(int slot,SourceSpriteRecipe recipe,PixelPoint position,WaterSpoutRainTask parent):base(slot,1){_recipe=recipe;_position=position;_parent=parent;Data[7]=9;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){if(++Data[1]>1){Data[1]=0;_invisible=!_invisible;if(++Data[2]==12){_parent.Data[9]--;Finished=true;}}}
        // A lower-slot birth waits until the next scan before BeginAffineAnim.
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_affine is null)_affine=new(_recipe.AffineAnimations[3]);else _affine.Step();}
        public override SourceVisualSpriteFrame Frame{get{var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,WaterSpoutHit,_position,default,0,!_invisible,false,true,256,_recipe.Asset,Subpriority){Width=32,Height=32,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
