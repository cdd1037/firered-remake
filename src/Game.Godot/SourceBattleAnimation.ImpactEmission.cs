namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string BarrageBallTemplate="gBarrageBallSpriteTemplate",DrillImpactTemplate="gFlashingHitSplatSpriteTemplate";
    private bool ValidateImpactEmissionOperand(SourceAnimationCommand command)
    {
        var a=command.Args;string name=a.FirstOrDefault()??"";
        bool task=command.Op=="createvisualtask"&&name is "AnimTask_DrillPeckHitSplats" or "AnimTask_BarrageBall";
        bool sprite=command.Op=="createsprite"&&name is "gCoinThrowSpriteTemplate" or "gFallingCoinSpriteTemplate" or "gCrossChopHandSpriteTemplate" or "gCrossImpactSpriteTemplate";
        if(!task&&!sprite)return false;
        if(task)
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(command,"Impact emitter takes only its source task priority.");
            string child=name=="AnimTask_BarrageBall"?BarrageBallTemplate:DrillImpactTemplate;
            if(!_script.SpriteDependencies.Contains(child))Fail(command,"Impact emitter child dependency is missing.");
            ValidateImpactEmissionRecipe(command,child);
            if(_script.SineTable.Length!=320)Fail(command,"Impact emitter requires the source sine table.");
            if(_validatingInvokedProgram)
            {
                // Each invoked linear root owns exactly one emitter; unrelated
                // catalog helpers need only their operand and recipe ABI checked.
                if(_script.Commands.Count(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is "AnimTask_DrillPeckHitSplats" or "AnimTask_BarrageBall")!=1)
                    Fail(command,"Impact emitter closure must own exactly one task.");
                // The reviewed roots use at most four extra matrices. Existing
                // affine-off tails consume cells, but no source matrix slots.
                if(_spriteScene.Sprites.Any(s=>s.Frame.Affine||s.Frame.Scale!=256))Fail(command,"Impact emitter cannot prove retained affine matrix capacity.");
                int needed=name=="AnimTask_BarrageBall"?2:8;
                if(Enumerable.Range(0,SourceSpriteRegistry<VisualSprite>.Capacity).Count(i=>!_battlers.Values.Any(b=>b.Visible&&b.Slot.Value==i)&&!_spriteScene.Sprites.Any(s=>s.Slot==i))<needed)Fail(command,"Impact emitter source sprite capacity proof failed.");
                if(name=="AnimTask_DrillPeckHitSplats"&&_script.Commands.Any(c=>c.Op=="loadspritegfx"&&c.Args[0]=="ANIM_TAG_WHIRLWIND_LINES"))
                {
                    if(!_script.MaterialDependencies.Contains("whirlwind-lines"))Fail(command,"Drill Peck load-only source material is missing.");
                    int tag=_script.SpriteTags.GetValueOrDefault("ANIM_TAG_WHIRLWIND_LINES",-1);
                    var palette=_script.SpritePalettes.GetValueOrDefault(tag,[]);
                    if(tag<0||palette.Length!=16)Fail(command,"Drill Peck load-only source palette is missing.");
                    if(_spriteMaterials is not null&&(!_spriteMaterials.TryGetValue("whirlwind-lines",out var loaded)||loaded.PaletteTag!=tag||!loaded.Palette.SequenceEqual(palette)||loaded.PaletteIndices.Length!=loaded.Width*loaded.Height))Fail(command,"Drill Peck load-only indexed material differs.");
                }

                var root=_script.Commands;
                if(!root.Contains(command)||root.Any(c=>c.Op is "call" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(command,"Impact emitter requires a single linear source invocation.");
                string[] allowed=name=="AnimTask_BarrageBall"?["gShakeMonOrTerrainSpriteTemplate"]:["gBowMonSpriteTemplate","gSlideMonToOriginalPosSpriteTemplate"];
                if(root.Count(c=>c.Op=="createsprite")>4||root.Any(c=>c.Op=="createsprite"&&!allowed.Contains(c.Args[0]))||root.Any(c=>c.Op=="createvisualtask"&&c.Args[0]!=name&&c.Args[0] is not ("AnimTask_ShakeMon" or "AnimTask_ShakeMon2")))Fail(command,"Impact emitter companion exceeds its source capacity proof.");
                if(name=="AnimTask_BarrageBall"&&(_battlers[_cue.Target.Value].SourceHeight is <8 or >64||(_battlers[_cue.Target.Value].SourceHeight&7)!=0))Fail(command,"Barrage requires exact source picture height.");
            }

            return true;
        }
        int length=name switch{"gCoinThrowSpriteTemplate"=>8,"gFallingCoinSpriteTemplate"=>3,"gCrossChopHandSpriteTemplate"=>6,_=>7};
        if(a.Length!=length||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(command,"Impact sprite source operands differ.");
        ValidateImpactEmissionRecipe(command,name);
        if(name=="gCrossChopHandSpriteTemplate"&&Number(a[5]) is <0 or >1)Fail(command,"Cross Chop hand selector differs.");
        if(name=="gCrossImpactSpriteTemplate"&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >32767))Fail(command,"Cross impact battler or duration differs.");
        if(name=="gCoinThrowSpriteTemplate")
        {
            if(Number(a[7]) is <1 or >32767)Fail(command,"Coin speed must be positive.");
            if(_validatingInvokedProgram)
            {
                var actor=_battlers[_cue.Actor.Value].Center(_doubles,true);var target=_battlers[_cue.Target.Value].Center(_doubles,true);
                int x=unchecked((short)(actor.X+InitialXDirection()*Number(a[3]))),end=unchecked((short)(target.X+(_cue.Actor.IsPlayer?1:-1)*Number(a[5])));
                int duration=(Math.Abs(end-x)<<8)/Number(a[7]);
                if(duration is <1 or >32767)Fail(command,"Coin source translation requires a positive signed duration.");
            }
        }
        if(name=="gFallingCoinSpriteTemplate"&&_script.SineTable.Length!=320)Fail(command,"Falling coin requires the source sine table.");
        return true;
    }
    private void ValidateImpactEmissionRecipe(SourceAnimationCommand command,string template)
    {
        if(!_script.SpriteRecipes.TryGetValue(template,out var recipe))Fail(command,"Impact emission sprite recipe is missing.");
        recipe=_script.SpriteRecipes[template];
        var expected=template switch
        {
            "gCoinThrowSpriteTemplate"=>("coin","AnimCoinThrow",16,false),
            "gFallingCoinSpriteTemplate"=>("coin","AnimFallingCoin",16,false),
            "gCrossChopHandSpriteTemplate"=>("hands-and-feet","AnimCrossChopHand",32,false),
            "gCrossImpactSpriteTemplate"=>("cross-impact","AnimCrossImpact",32,true),
            DrillImpactTemplate=>("impact","AnimFlashingHitSplat",32,false),
            _=>("red-ball","SpriteCallbackDummy",32,false)
        };
        if(recipe.Asset!=expected.Item1||recipe.Callback!=expected.Item2||recipe.Width!=expected.Item3||recipe.Height!=expected.Item3||recipe.Blend!=expected.Item4||recipe.AffineDoubleSize||recipe.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Impact emission source shape differs.");
        bool coin=recipe.Asset=="coin",hand=recipe.Callback=="AnimCrossChopHand";
        SourceSpriteAnimFrame[][] frames=hand?[[new(48,1,false,false)],[new(48,1,true,false)]]:[[new(coin?8:0,1,false,false)]];
        int[] loops=hand?[-1,-1]:[coin?-1:0];
        if(recipe.Animations.Length!=frames.Length||!recipe.LoopStarts.SequenceEqual(loops))Fail(command,"Impact emission source frame table differs.");
        for(int i=0;i<frames.Length;i++)if(!recipe.Animations[i].SequenceEqual(frames[i]))Fail(command,"Impact emission source frame command differs.");
        SourceSpriteAffineCommand[][] affines=template switch
        {
            "gFallingCoinSpriteTemplate"=>[[new("frame",0,0,10,1,0),new("jump",0,0,0,0,0)]],
            BarrageBallTemplate=>[[new("frame",0,0,252,24,0),new("end",0,0,0,0,0)],[new("frame",256,256,192,0,0),new("frame",0,0,4,24,0),new("end",0,0,0,0,0)]],
            DrillImpactTemplate=>[[new("frame",0,0,0,8,0),new("end",0,0,0,0,0)],[new("frame",216,216,0,0,0),new("frame",0,0,0,8,0),new("end",0,0,0,0,0)],[new("frame",176,176,0,0,0),new("frame",0,0,0,8,0),new("end",0,0,0,0,0)],[new("frame",128,128,0,0,0),new("frame",0,0,0,8,0),new("end",0,0,0,0,0)]],
            _=>[]
        };
        if(recipe.AffineAnimations.Length!=affines.Length)Fail(command,"Impact emission affine table differs.");
        for(int i=0;i<affines.Length;i++)if(!recipe.AffineAnimations[i].SequenceEqual(affines[i]))Fail(command,"Impact emission affine program differs.");
    }
    private bool TryCreateImpactEmissionSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gCoinThrowSpriteTemplate" or "gFallingCoinSpriteTemplate" or "gCrossChopHandSpriteTemplate" or "gCrossImpactSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Impact sprite graphics have not been loaded.");
        _sprites.Add(new ImpactEmissionSprite(slot,priority,recipe,this));return true;
    }
    private bool TryCreateImpactEmissionTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_DrillPeckHitSplats":
                if(!_loaded.Contains("ANIM_TAG_IMPACT"))Fail(command,"Drill Peck impact graphics have not been loaded.");
                var drill=new DrillPeckImpactTask();drill.Step(this);if(!drill.Finished)_tasks.Add(drill);return true;
            case "AnimTask_BarrageBall":
                if(!_loaded.Contains("ANIM_TAG_RED_BALL"))Fail(command,"Barrage ball graphics have not been loaded.");
                var ball=new BarrageBallTask(this);if(!ball.Finished)_tasks.Add(ball);return true;
            default:return false;
        }
    }
    private sealed class ImpactEmissionSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private SourceAffineMatrix _matrix=SourceAffineMatrix.FromScaleRotation(256,256,0);private PhysicalLinear? _linear;
        private PixelPoint _position,_offset;private short _ticks,_remaining,_x,_angle,_amplitude=-16;private int _phase;private readonly bool _flip;
        public ImpactEmissionSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;
            var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            var actor=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
            _position=target; // Cmd_createsprite always starts at the target, regardless of priority participant.
            switch(recipe.Callback)
            {
                case "AnimCrossChopHand":
                    _position=new(unchecked((short)(target.X+owner.InitialXDirection()*a[0])),unchecked((short)(target.Y+a[1])));
                    _flip=a[2]!=0;_linear=new(_flip?20:-20,-20,30);break;
                case "AnimCrossImpact":
                    var center=a[2]==0?actor:target;
                    _position=new(unchecked((short)(center.X+owner.InitialXDirection()*a[0])),unchecked((short)(center.Y+a[1])));_remaining=a[3];break;
                case "AnimCoinThrow":
                    _position=new(unchecked((short)(actor.X+owner.InitialXDirection()*a[0])),unchecked((short)(actor.Y+a[1])));
                    if(!owner._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);
                    int endX=unchecked((short)(target.X+a[2])),endY=unchecked((short)(target.Y+a[3]));
                    int rotation=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(endX-_position.X)),unchecked((short)(endY-_position.Y)))+0xc000));
                    _matrix=SourceAffineMatrix.FromScaleRotation(256,256,rotation);
                    _linear=new(endX-_position.X,endY-_position.Y,(Math.Abs(endX-_position.X)<<8)/a[4]);break;
                case "AnimFallingCoin":
                    _position=new(_position.X,unchecked((short)(_position.Y+8)));_affine=new(recipe.AffineAnimations[0]);break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames.Frame;var m=_affine?.Matrix??_matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip^_flip,_recipe.Blend,256,_recipe.Asset,Subpriority)
                {Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_recipe.Asset=="coin",MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Callback)
            {
                case "AnimCrossImpact":
                    if(_phase!=0)Finished=true;else if(_remaining>0)_remaining--;else _phase=1;break;
                case "AnimFallingCoin":
                    _x=unchecked((short)(_x+128));int x=_x>>8;if(owner._cue.Actor.IsPlayer)x=-x;
                    _offset=new(x,unchecked((short)((owner._script.SineTable[_angle]*_amplitude)>>8)));_angle=unchecked((short)(_angle+5));
                    if(_angle>126){_angle=0;_amplitude=(short)(_amplitude/2);if(++_ticks==2)Finished=true;}break;
                case "AnimCoinThrow":
                    if(_phase!=0)Finished=true;else if(_linear!.Step(out var coin))_phase=1;else _offset=coin;break;
                case "AnimCrossChopHand":
                    if(_phase==3){Finished=true;break;}
                    if(_phase==1)
                    {
                        if(++_ticks==11)
                        {
                            int endX=unchecked((short)(_position.X-_offset.X)),endY=unchecked((short)(_position.Y-_offset.Y));
                            _position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;
                            _linear=new(endX-_position.X,endY-_position.Y,8);_phase=2;
                        }
                    }
                    else if(_linear!.Step(out var hand))_phase=_phase==0?1:3;else _offset=hand;
                    break;
            }
        }
    }
    private sealed class DrillPeckImpactTask:VisualTask
    {
        private short _angle;
        public override void Step(SourceBattleAnimation owner)
        {
            if((_angle%32)==0)
            {
                owner._animationArgs[0]=unchecked((short)((owner._script.SineTable[_angle]*-13)>>8));
                owner._animationArgs[1]=unchecked((short)((owner._script.SineTable[_angle+64]*-13)>>8));
                owner._animationArgs[2]=1;owner._animationArgs[3]=3;
                // FlashingHit's constructor is its original initialization callback
                // and static affine BeginAnim. Do not run its toggle callback twice.
                int slot=owner._sprites.CreateAndAnimate(s=>new FlashingHit(s,3,owner._animationArgs.Take(4).Select(v=>(int)v).ToArray(),owner),_=>{},_=>{},owner.IsReservedSpriteSlot);
                if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)throw new InvalidDataException("Counted Drill Peck allocation exhausted source sprites.");
                owner._oamY[slot]=0; // Template OAM is reset even when the first toggle is invisible.
            }
            _angle=unchecked((short)(_angle+8));if(_angle>255)Finished=true;
        }
    }
    private sealed class BarrageBallTask:VisualTask
    {
        private readonly BarrageBallSprite? _child;private short _state,_timer,_count;
        public BarrageBallTask(SourceBattleAnimation owner)
        {
            int slot=owner.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity){Finished=true;return;}
            var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var target=owner._battlers[owner._cue.Target.Value];var end=target.Center(owner._doubles,true);
            _child=new(slot,unchecked((byte)(target.Subpriority-5)),owner._script.SpriteRecipes[BarrageBallTemplate],start,new(end.X,unchecked((short)(end.Y+target.SourceHeight/4))),owner._cue.Actor.IsPlayer?0:1);
            owner._sprites.Add(_child);owner._oamY[slot]=0; // Raw CreateSprite: no eager callback/player tick.
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:if(++_timer>1){_timer=0;_child!.Translate(owner);if(++_count>7)_state++;}break;
                case 1:if(_child!.Translate(owner)){_timer=_count=0;_state++;}break;
                case 2:if(++_timer>1){_timer=0;_count++;_child!.Invisible=(_count&1)!=0;if(_count==16){_child.Retire();owner._sprites.Remove(_child);_state++;}}break;
                case 3:Finished=true;break;
            }
        }
    }
    private sealed class BarrageBallSprite(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,PixelPoint end,int affine):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;private readonly HorizontalArc _arc=new(end.X-position.X,end.Y-position.Y,16,-32);private PixelPoint _offset;
        public bool Invisible{get;set;}public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames?.Frame??recipe.Animations[0][0];var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,recipe.Template,position,_offset,f.Tile,!Invisible,f.HFlip,false,256,recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation owner){} // SpriteCallbackDummy; the task owns translation/destruction.
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {if(_frames is null){_frames=new(recipe);_affine=new(recipe.AffineAnimations[affine]);}else{_frames.Step();_affine!.Step();}}
        public bool Translate(SourceBattleAnimation owner){if(_arc.Step(owner._script.SineTable,out var offset))return true;_offset=offset;return false;}
        public void Retire()=>Finished=true;
    }
}
