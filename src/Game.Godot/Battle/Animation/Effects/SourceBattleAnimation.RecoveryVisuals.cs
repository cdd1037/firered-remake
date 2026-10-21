namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SleepLetterTemplate="gSleepLetterZSpriteTemplate",GreenStarTemplate="gGreenStarSpriteTemplate",MoonTemplate="gMoonSpriteTemplate",MoonSparkleTemplate="gMoonlightSparkleSpriteTemplate",AuroraRingTemplate="gAuroraBeamRingSpriteTemplate";
    private bool ValidateRecoveryVisualOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        bool sprite=c.Op=="createsprite"&&name is SleepLetterTemplate or GreenStarTemplate or MoonTemplate or MoonSparkleTemplate or AuroraRingTemplate;
        bool beam=c.Op=="createvisualtask"&&name=="AnimTask_MorningSunLightBeam";
        bool fade=c.Op=="createvisualtask"&&name=="AnimTask_MoonlightEndFade";
        bool alpha=c.Op=="createvisualtask"&&name=="AnimTask_AlphaFadeIn";
        bool rotate=c.Op=="createvisualtask"&&name=="AnimTask_RotateAuroraRingColors";
        if(!sprite&&!beam&&!fade&&!alpha&&!rotate)return false;
        if(sprite)
        {
            if(a.Length!=(name is SleepLetterTemplate or AuroraRingTemplate?8:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Recovery sprite source operand widths differ.");
            if(name==GreenStarTemplate&&(Number(a[3])!=30||Number(a[4])!=640))Fail(c,"Green-star lifetime or velocity exceeds the reviewed raw-child closure.");
            if(name==AuroraRingTemplate&&Number(a[7]) is <1 or >255)Fail(c,"Aurora ring translation duration differs.");
            ValidateRecoverySpriteRecipe(c,name);
            if(name==GreenStarTemplate)RequireVisualSeed(c);
        }
        else
        {
            if(a.Length!=(alpha?7:rotate?3:2)||Number(a[1]) is <0 or >255)Fail(c,"Recovery task source operand count or priority differs.");
            if(alpha&&(a.Skip(2).Take(4).Any(x=>Number(x) is <0 or >16)||Number(a[6]) is <short.MinValue or >=short.MaxValue)||rotate&&Number(a[2]) is <1 or >short.MaxValue)Fail(c,"Recovery task source operands differ.");
        }
        if(!_validatingInvokedProgram)return true;
        if(beam)
        {
            if(!_script.OverlayDependencies.Contains("morning-sun")||!_script.OverlayPalettes.TryGetValue("morning-sun",out var palette)||palette.Length!=16||!_script.SoundEffects.ContainsKey("SE_M_MORNING_SUN"))Fail(c,"Morning Sun overlay palette or callback sound dependency is missing.");
            var commands=OverlayInvocationCommands();
            if(commands.Any(x=>x.Op=="monbg")||commands.Count(IsOverlayCommand)!=1)Fail(c,"Morning Sun needs one BG1 owner without monbg in its bounded closure.");
        }
        if(fade&&OverlayInvocationCommands().Any(x=>x.Op=="monbg"))Fail(c,"Moonlight direct BG palette writes require a closure without monbg.");
        foreach(string tag in sprite?[RecoveryTag(name)]:fade?["ANIM_TAG_MOON","ANIM_TAG_GREEN_SPARKLE"]:rotate?["ANIM_TAG_RAINBOW_RINGS"]:Array.Empty<string>())RequireOverlaySpritePalette(c,tag);
        if(sprite&&name==GreenStarTemplate&&!_script.SpriteDependencies.Contains(GreenStarTemplate))Fail(c,"Morning Sun raw child dependency is missing.");
        ValidateRecoverySceneCapacity(c);return true;
    }
    private static string RecoveryTag(string name)=>name switch{SleepLetterTemplate=>"ANIM_TAG_LETTER_Z",GreenStarTemplate=>"ANIM_TAG_GREEN_STAR",MoonTemplate=>"ANIM_TAG_MOON",MoonSparkleTemplate=>"ANIM_TAG_GREEN_SPARKLE",AuroraRingTemplate=>"ANIM_TAG_RAINBOW_RINGS",_=>throw new InvalidDataException("Unknown recovery sprite.")};
    private void ValidateRecoverySpriteRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Recovery sprite recipe is missing.");return;}
        string callback=name switch{SleepLetterTemplate=>"AnimSleepLetterZ",GreenStarTemplate=>"AnimGreenStar",MoonTemplate=>"AnimMoon",MoonSparkleTemplate=>"AnimMoonlightSparkle",_=>"AnimAuroraBeamRings"};
        int width=name switch{SleepLetterTemplate=>32,MoonTemplate=>64,AuroraRingTemplate=>8,_=>16};int height=name==AuroraRingTemplate?16:width;
        if(r.Callback!=callback||r.Asset!=RecoveryTag(name).Replace("ANIM_TAG_","").ToLowerInvariant().Replace('_','-')||r.Width!=width||r.Height!=height||r.Blend!=(name==MoonTemplate)||r.AffineDoubleSize!=(name==AuroraRingTemplate)||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Recovery sprite source shape differs.");
        SourceSpriteAnimFrame F(int tile,int duration)=>new(tile,duration,false,false);
        SourceSpriteAnimFrame[][] frames=name switch{SleepLetterTemplate=>[[F(0,40)]],GreenStarTemplate=>[[F(0,6),F(4,6)],[F(8,6)],[F(12,6)]],MoonSparkleTemplate=>[[F(0,8),F(4,8),F(8,8),F(12,8)]],AuroraRingTemplate=>[[F(0,1)],[F(4,1)]],_=>[[F(0,1)]]};
        int[] loops=name switch{GreenStarTemplate=>[0,-1,-1],MoonTemplate or MoonSparkleTemplate=>[0],AuroraRingTemplate=>[-1,-1],_=>[-1]};
        SourceSpriteAffineCommand[][] affine=name switch{SleepLetterTemplate=>[[new("frame",20,20,226,0,0),new("frame",8,8,1,24,0),new("end",0,0,0,0,0)],[new("frame",20,20,30,0,0),new("frame",8,8,255,24,0),new("end",0,0,0,0,0)]],AuroraRingTemplate=>[[new("frame",0,0,0,1,0),new("frame",96,96,0,1,0),new("end",0,0,0,0,0)]],_=>[]};
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(loops)||r.AffineAnimations.Length!=affine.Length)Fail(c,"Recovery frame or affine table count differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Recovery source frame program differs.");
        for(int i=0;i<affine.Length;i++)if(!r.AffineAnimations[i].SequenceEqual(affine[i]))Fail(c,"Recovery source affine program differs.");
    }
    private void ValidateRecoverySceneCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(c,"Recovery raw/affine allocation needs a bounded linear closure.");
        bool stars=commands.Any(x=>x.Op=="createsprite"&&x.Args[0]==GreenStarTemplate||x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_MorningSunLightBeam"),rings=commands.Any(x=>x.Op=="createsprite"&&x.Args[0]==AuroraRingTemplate||x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_RotateAuroraRingColors"),sleep=commands.Any(x=>x.Op=="createsprite"&&x.Args[0]==SleepLetterTemplate);
        string[] sprites=stars?[GreenStarTemplate,"gHealingBlueStarSpriteTemplate","gSimplePaletteBlendSpriteTemplate"]:rings?[AuroraRingTemplate]:sleep?[SleepLetterTemplate]:[MoonTemplate,MoonSparkleTemplate,"gHealingBlueStarSpriteTemplate","gSimplePaletteBlendSpriteTemplate"];
        string[] tasks=stars?["AnimTask_MorningSunLightBeam","AnimTask_BlendBattleAnimPal"]:rings?["AnimTask_RotateAuroraRingColors","AnimTask_ShakeMon2"]:sleep?[]:["AnimTask_AlphaFadeIn","AnimTask_MoonlightEndFade"];
        if(commands.Any(x=>x.Op=="createsprite"&&!sprites.Contains(x.Args[0])||x.Op=="createvisualtask"&&!tasks.Contains(x.Args[0])))Fail(c,"Recovery companion allocations exceed the reviewed closure.");
        // Ignore waits, graphics-load stalls and destruction of other sprites to
        // overestimate occupancy. A green-star family is gone within40 callbacks;
        // a ring within duration+2. Only explicit delays advance this lower bound.
        int time=0,peak=0,affinePeak=0;List<(int End,int Slots,int Affines)> live=[];
        foreach(var command in commands)
        {
            if(command.Op=="delay"){time+=Number(command.Args[0])+2;continue;}
            if(command.Op!="createsprite")continue;
            live.RemoveAll(x=>x.End<=time);string template=command.Args[0];bool star=template==GreenStarTemplate,ring=template==AuroraRingTemplate,letter=template==SleepLetterTemplate;
            live.Add((star?time+40:ring?time+Number(command.Args[7])+2:int.MaxValue,star?3:1,ring||letter?1:0));
            peak=Math.Max(peak,live.Sum(x=>x.Slots));affinePeak=Math.Max(affinePeak,live.Sum(x=>x.Affines));
        }
        int retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length)),retainedAffine=_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256);
        if(22+peak+retained>64||4+affinePeak+retainedAffine>32)Fail(c,"Recovery closure exceeds source OAM or affine-matrix capacity.");
    }
    private bool TryCreateRecoveryVisualTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_MorningSunLightBeam":_tasks.Add(new MorningSunBeamTask(this));return true;
            case "AnimTask_AlphaFadeIn":_tasks.Add(new RecoveryAlphaTask(this));return true;
            case "AnimTask_MoonlightEndFade":
                if(new[]{"ANIM_TAG_MOON","ANIM_TAG_GREEN_SPARKLE"}.Any(t=>!_loaded.Contains(t)))Fail(c,"Moonlight palettes have not been loaded.");
                _tasks.Add(new MoonlightFadeTask(this));return true;
            case "AnimTask_RotateAuroraRingColors":
                if(!_loaded.Contains("ANIM_TAG_RAINBOW_RINGS"))Fail(c,"Aurora palette has not been loaded.");
                _tasks.Add(new AuroraPaletteTask(FindSpritePalette(_script.SpriteTags["ANIM_TAG_RAINBOW_RINGS"]),_animationArgs[0]));return true;
            default:return false;
        }
    }
    private bool TryCreateRecoveryVisualSprite(SourceAnimationCommand c,int slot,int priority)
    {
        string name=c.Args[0];if(name is not (SleepLetterTemplate or GreenStarTemplate or MoonTemplate or MoonSparkleTemplate or AuroraRingTemplate))return false;
        if(!_loaded.Contains(RecoveryTag(name)))Fail(c,"Recovery sprite graphics have not been loaded.");
        var r=_script.SpriteRecipes[name];
        _sprites.Add(name==GreenStarTemplate?new GreenStarParent(slot,priority,r,this):name is MoonTemplate or MoonSparkleTemplate?new MoonlightSprite(slot,priority,r,this):name==SleepLetterTemplate?new SleepLetterSprite(slot,priority,r,this):new AuroraRingSprite(slot,priority,r,this));return true;
    }
    private sealed class MorningSunBeamTask:VisualTask
    {
        private static readonly int[] Shifts=[-24,24,-4,0];
        private readonly int _baseX;private int _state=1,_alpha,_tick,_pass,_wait;
        public MorningSunBeamTask(SourceBattleAnimation o)
        {
            _baseX=o._cue.Actor.IsPlayer?-10:-135;o.BeginOverlay("morning-sun");o.SetBackgroundPriority(1,1);o.SetOverlayScroll(_baseX,0);o.SetAlphaBlend(2,63,0,16);Sound(o);
        }
        private static void Sound(SourceBattleAnimation o)=>o._audio.Play(new("SE_M_MORNING_SUN",o.AdjustPanning(-64),o._frame));
        public override void Step(SourceBattleAnimation o)
        {
            switch(_state)
            {
                case 1:if(_tick++>0){_tick=0;_alpha=Math.Min(12,_alpha+1);o.SetAlphaBlend(2,63,_alpha,16-_alpha);if(_alpha==12)_state=2;}break;
                case 2:_alpha=Math.Max(0,_alpha-1);o.SetAlphaBlend(2,63,_alpha,16-_alpha);if(_alpha==0){o.SetOverlayScroll(_baseX+Shifts[_pass],0);_state=++_pass==4?4:3;}break;
                case 3:if(++_wait==4){_wait=0;_state=1;Sound(o);}break;
                case 4:o.EndOverlay(false);o.SetBackgroundPriority(1,1);o.ClearAlphaBlend();Finished=true;break;
            }
        }
    }
    private sealed class RecoveryAlphaTask:VisualTask
    {
        private readonly int _targetA,_targetB,_deltaA,_deltaB,_delay;private int _a,_b,_tick,_alternate;
        public RecoveryAlphaTask(SourceBattleAnimation o)
        {var a=o._animationArgs;_a=a[0];_b=a[1];_targetA=a[2];_targetB=a[3];_deltaA=Math.Sign(_targetA-_a);_deltaB=Math.Sign(_targetB-_b);_delay=a[4];o._alphaA=_a;o._alphaB=_b;}
        public override void Step(SourceBattleAnimation o)
        {
            _tick=unchecked((short)(_tick+1));if(_tick<=_delay)return;_tick=0;
            if((++_alternate&1)!=0){if(_a!=_targetA)_a+=_deltaA;}else if(_b!=_targetB)_b+=_deltaB;
            o._alphaA=_a;o._alphaB=_b;if(_a==_targetA&&_b==_targetB)Finished=true;
        }
    }
    private sealed class MoonlightFadeTask:VisualTask
    {
        private const ushort Color=27|(29<<5)|(31<<10);private readonly uint _restore;private int _state,_colorStep,_wait;
        public MoonlightFadeTask(SourceBattleAnimation o)
        {
            uint mons=o.SelectPalettes(1920);_restore=mons|0xe;
            uint objects=mons|(1u<<(16+o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_MOON"])))|(1u<<(16+o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_GREEN_SPARKLE"])));
            Begin(o,objects,0,16);Step(o);
        }
        private static void Begin(SourceBattleAnimation o,uint mask,int from,int to)
        {if(!o.PaletteFadeActive){o._fade=new(mask,from,to,Color);o._fade.Update(o);}}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_state)
            {
                case 0:
                    int step=++_colorStep;ushort color;
                    if(step<=15)color=(ushort)(((13*step)>>3)|(((14*step)>>3)<<5)|(((15*step)>>3)<<10));else{color=Color;_state++;}
                    Span<ushort> colors=stackalloc ushort[15];colors.Fill(color);for(int bank=1;bank<=3;bank++)o.WriteBackgroundPaletteRange(bank,1,colors);break;
                case 1:
                    if(!o.PaletteFadeActive){foreach(var sprite in o._sprites.OfType<MoonlightSprite>())sprite.DestroySignaled=true;_wait=0;_state++;}break;
                case 2:if(++_wait>30){Begin(o,_restore,16,0);_state++;}break;
                case 3:if(!o.PaletteFadeActive)Finished=true;break;
            }
        }
    }
    private sealed class AuroraPaletteTask(int slot,int duration):VisualTask
    {
        private int _tick,_age;
        public override void Step(SourceBattleAnimation o)
        {if(++_tick==3){_tick=0;var palette=o._spritePaletteBanks[slot];ushort first=palette[1];for(int i=1;i<8;i++)palette[i]=palette[i+1];palette[8]=first;}if(++_age==duration)Finished=true;}
    }
    private sealed class SleepLetterSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;private readonly PixelPoint _position;private readonly int _direction;private PixelPoint _offset;private short _sum,_age,_drift;
        public SleepLetterSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {_recipe=recipe;_frames=new(recipe);_direction=o._cue.Actor.IsPlayer?1:-1;_affine=new(recipe.AffineAnimations[_direction==1?0:1]);var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(unchecked((short)(p.X+_direction*o._animationArgs[0])),unchecked((short)(p.Y+o._animationArgs[1])));}
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override void Step(SourceBattleAnimation o){_offset=new(_drift/10,-(_sum/40));_drift=unchecked((short)(_drift+_direction*2));_sum=unchecked((short)(_sum+_age));if(++_age>60)Finished=true;}
    }
    private sealed class MoonlightSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private PixelPoint _position;private int _tick,_distance;
        public bool DestroySignaled{get;set;}
        public MoonlightSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {_recipe=recipe;_frames=new(recipe);_position=new(recipe.Template==MoonTemplate?o._animationArgs[0]:unchecked((short)(o._battlers[o._cue.Actor.Value].Base(o._doubles).X+o._animationArgs[0])),o._animationArgs[1]);}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,true,false,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height};
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override void Step(SourceBattleAnimation o)
        {if(_recipe.Template==MoonSparkleTemplate&&++_tick>1){_tick=0;if(_distance<120){_position=new(_position.X,unchecked((short)(_position.Y+1)));_distance++;}}if(DestroySignaled)Finished=true;}
    }
    private sealed class AuroraRingSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;private readonly PhysicalLinear _linear;private readonly PixelPoint _position;private PixelPoint _offset;private bool _paused=true,_restart;
        public AuroraRingSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var a=o._animationArgs;var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));var t=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
            _linear=new(unchecked((short)(t.X+(o._cue.Actor.IsPlayer?a[2]:-a[2])-_position.X)),unchecked((short)(t.Y+a[3]-_position.Y)),a[4]);Step(o);
        }
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=8,Height=16,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_restart)_restart=false;else _frames.Step();if(!_paused)_affine.Step();}
        public override void Step(SourceBattleAnimation o)
        {if(o._animationArgs[7]==-1){_frames=new(_recipe,1);_restart=true;_paused=false;}if(_linear.Step(out var point))Finished=true;else _offset=point;}
    }
    private sealed class GreenStarParent:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private readonly GreenStarChild[] _children;private int _y,_fraction,_remaining=30,_revealed,_state;
        public GreenStarParent(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);int x=o.NextVisualRandom()&63;if(x>31)x=32-x;var p=o._battlers[o._cue.Actor.Value].Base(o._doubles);_position=new(unchecked((short)(p.X+x)),unchecked((short)(p.Y+32)));_children=new GreenStarChild[2];
            for(int i=0;i<2;i++){int child=o.FindFreeSpriteSlot(slot);if(child==SourceSpriteRegistry<VisualSprite>.Capacity)throw new InvalidDataException("Validated green-star source capacity was exhausted.");_children[i]=new(child,unchecked((byte)(priority+1)),recipe,_position,i+1);o._sprites.Add(_children[i]);}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,new(0,_y),_frames.Frame.Tile,_state==0,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override void Step(SourceBattleAnimation o)
        {
            if(_state==1){if(_children.All(c=>c.Stopped)){foreach(var child in _children)o._sprites.Remove(child);Finished=true;}return;}
            _y=unchecked((short)(_y-((_fraction+640)>>8)));_fraction=(_fraction+640)&255;
            if(_revealed==0&&_y< -8){_children[0].Visible=true;_revealed++;}if(_revealed==1&&_y< -16){_children[1].Visible=true;_revealed++;}
            if(--_remaining== -1)_state=1;
        }
    }
    private sealed class GreenStarChild(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,int animation):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private int _y,_fraction,_remaining=30;
        public bool Visible{get;set;}public bool Stopped{get;private set;}
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,new(0,_y),_frames?.Frame.Tile??0,Visible,false,false,256,recipe.Asset,Subpriority){Width=16,Height=16};
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_frames is null)_frames=new(recipe,animation);else _frames.Step();}
        public override void Step(SourceBattleAnimation o)
        {if(!Visible||Stopped)return;_y=unchecked((short)(_y-((_fraction+640)>>8)));_fraction=(_fraction+640)&255;if(--_remaining== -1){Visible=false;Stopped=true;}}
    }
}
