namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string HelpingClapTemplate="gHelpingHandClapSpriteTemplate",ImprisonOrbTemplate="sImprisonOrbSpriteTemplate",FastNoteTemplate="gFastFlyingMusicNotesSpriteTemplate";
    private bool ValidateSupportStatusOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&name is "ANIM_TAG_TAG_HAND" or "ANIM_TAG_HOLLOW_ORB")
        {if(a.Length!=1)Fail(c,"Support graphics operands differ.");return true;}
        if(c.Op=="createvisualtask"&&name is "AnimTask_HelpingHandAttackerMovement" or "AnimTask_ImprisonOrbs" or "AnimTask_TeeterDanceMovement")
        {
            if(a.Length!=2||Number(a[1])!=5||_script.SineTable.Length!=320)Fail(c,"Support task source operands differ.");
            if(name=="AnimTask_ImprisonOrbs")
            {if(!_script.SpriteDependencies.Contains(ImprisonOrbTemplate))Fail(c,"Imprison raw child dependency is missing.");ValidateSupportStatusRecipe(c,ImprisonOrbTemplate);if(_validatingInvokedProgram)ValidateReactionGeometry(c,_battlers[_cue.Actor.Value]);}
            if(name=="AnimTask_TeeterDanceMovement"&&_validatingInvokedProgram)
            {
                if(!_script.MaterialDependencies.Contains("duck"))Fail(c,"Teeter load-only Duck material is missing.");
            }
            return true;
        }
        if(c.Op!="createsprite"||name is not (HelpingClapTemplate or FastNoteTemplate))return false;
        if(a.Length!=(name==HelpingClapTemplate?4:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Support sprite source operands differ.");
        if(Number(a[3])<0||Number(a[3])>(name==HelpingClapTemplate?1:7))Fail(c,"Support frame/hand selector differs.");
        ValidateSupportStatusRecipe(c,name);return true;
    }
    private void ValidateSupportStatusRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Support sprite recipe missing.");return;}
        bool note=name==FastNoteTemplate,clap=name==HelpingClapTemplate;
        if(r.Asset!=(note?"music-notes":clap?"tag-hand":"hollow-orb")||r.Callback!=(note?"AnimFlyingMusicNotes":clap?"AnimHelpingHandClap":"SpriteCallbackDummy")||r.Width!=(clap?32:16)||r.Height!=r.Width||r.Blend!=(name==ImprisonOrbTemplate)||r.AffineDoubleSize!=note||r.FrameLoops.Any(x=>x.Length!=0)||_script.SineTable.Length!=320)Fail(c,"Support source sprite shape differs.");
        SourceSpriteAnimFrame[][] expected=note?Enumerable.Range(0,8).Select(i=>new[]{new SourceSpriteAnimFrame((i%6)*4,i==2?41:10,false,i>=6)}).ToArray():[[new(0,1,false,false)]];
        if(r.Animations.Length!=expected.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(note?-1:0,expected.Length)))Fail(c,"Support frame table shape differs.");
        for(int i=0;i<expected.Length;i++)if(!r.Animations[i].SequenceEqual(expected[i]))Fail(c,"Support frame commands differ.");
        SourceSpriteAffineCommand[] affine=[new("frame",12,12,0,16,0),new("frame",-12,-12,0,16,0),new("jump",0,0,0,0,0)];
        if(r.AffineAnimations.Length!=(note?1:0)||note&&!r.AffineAnimations[0].SequenceEqual(affine))Fail(c,"Support affine program differs.");
    }
    private bool TryCreateSupportStatusSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (HelpingClapTemplate or FastNoteTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Support graphics are not loaded.");
        _sprites.Add(new SupportStatusSprite(slot,priority,r,this));return true;
    }
    private bool TryCreateSupportStatusTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_HelpingHandAttackerMovement":_tasks.Add(new HelpingHandMovement(this));return true;
            case "AnimTask_TeeterDanceMovement":_tasks.Add(new TeeterDanceMovement(this));return true;
            case "AnimTask_ImprisonOrbs":if(!_loaded.Contains("ANIM_TAG_HOLLOW_ORB"))Fail(c,"Imprison raw child graphics are not loaded.");_tasks.Add(new ImprisonOrbs(this));return true;
            default:return false;
        }
    }
    private sealed class SupportStatusSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly int _direction;private PixelPoint _position,_offset;private int _phase,_timer,_tile,_angle;private bool _orbited;private short _x,_y,_vx,_vy;
        private static short S(int v)=>unchecked((short)v);
        public SupportStatusSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;var a=o._animationArgs;bool clap=recipe.Template==HelpingClapTemplate;_frames=new(recipe,clap?0:a[0]);
            if(clap){_direction=a[0]==0?1:-1;_position=new(_direction==1?100:140,56);return;}
            if(!o._cue.Actor.IsPlayer)a[1]=S(-a[1]);var center=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(S(center.X+a[1]),S(center.Y+a[2]));_x=S(_position.X<<4);_y=S(_position.Y<<4);_vx=S((a[1]<<4)/5);_vy=S((a[2]<<7)/5);_affine=new(recipe.AffineAnimations[0]);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==HelpingClapTemplate){Clap(o);return;}
            _x=S(_x+_vx);_y=S(_y+_vy);_position=new(_x>>4,_y>>4);
            if(_timer>5&&!_orbited){_angle=(_angle+16)&255;_offset=new(o._script.SineTable[_angle+64]*18>>8,o._script.SineTable[_angle]*18>>8);if(_angle==0)_orbited=true;}
            if(++_timer==48)Finished=true;
        }
        private void Next(int duration){if(++_timer==duration){_timer=0;_phase++;}}
        private void Clap(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:_position=new(S(_position.X-((_timer&1)!=0?_direction*2:0)),S(_position.Y-_direction*2));Next(9);break;
                case 1:Next(4);break;
                case 2:_timer++;_position=new(_position.X,S(_position.Y+_direction*3));Sway(o);if(_timer==12){_timer=0;_phase++;}break;
                case 3:Next(2);break;
                case 4:_timer++;_position=new(_position.X,S(_position.Y-_direction*3));Sway(o);if(_timer==12)_phase++;break;
                case 5:_timer++;_position=new(_position.X,S(_position.Y+_direction*3));Sway(o);if(_timer==15)_tile+=16;if(_timer==18){_timer=0;_phase++;}break;
                case 6:_position=new(S(_position.X+_direction*6),_position.Y);Next(9);break;
                case 7:_position=new(S(_position.X+_direction*2),_position.Y);Next(1);break;
                default:_position=new(S(_position.X-_direction*3),_position.Y);if(++_timer==5)Finished=true;break;
            }
        }
        private void Sway(SourceBattleAnimation o)=>_offset=new(_direction*(o._script.SineTable[_timer*10]>>3),0);
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        // Source frame flips do not touch OAM when affine mode is enabled.
        // Selectors6/7 retain frame-control VFlip, but affine notes render unflipped.
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,_affine is null?_tile:_frames.Frame.Tile,true,_direction==1,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class HelpingHandMovement:VisualTask
    {
        private readonly int _battler,_direction;private int _phase,_timer,_repeat;
        public HelpingHandMovement(SourceBattleAnimation o){_battler=o._cue.Actor.Value;_direction=o._doubles?(o._battlers[_battler].Base(true).X>o._battlers[_battler^2].Base(true).X?1:-1):o._cue.Actor.IsPlayer?-1:1;}
        private void Next(int ticks){if(++_timer==ticks){_timer=0;_phase++;}}
        public override void Step(SourceBattleAnimation o)
        {
            int delta=_phase switch{1=>-_direction*3,2=>_direction*3,4=>_direction,6=>-_direction*4,7=>_direction*4,_=>0};var p=o._offsets[_battler];o._offsets[_battler]=new(unchecked((short)(p.X+delta)),p.Y);
            switch(_phase)
            {case 0:Next(13);break;case 1:case 2:Next(6);break;case 3:if(++_timer==2){_timer=0;if(_repeat++==0)_phase=1;else _phase++;}break;case 4:Next(3);break;case 5:Next(6);break;case 6:case 7:Next(5);break;default:o._offsets[_battler]=new(0,p.Y);Finished=true;break;}
        }
    }
    private sealed class TeeterDanceMovement:VisualTask
    {
        private readonly int _battler,_direction,_baseX;private int _phase,_angle,_smallAngle,_baseDelta,_x2;
        public TeeterDanceMovement(SourceBattleAnimation o){_battler=o._cue.Actor.Value;_direction=o._cue.Actor.IsPlayer?1:-1;_baseX=o.BattlerPosition(_battler).X;}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==2){Finished=true;return;}
            _smallAngle=(_smallAngle+8)&255;_x2=o._script.SineTable[_smallAngle]>>5;
            if(_phase==0){_angle=(_angle+2)&255;_baseDelta=(o._script.SineTable[_angle]>>3)*_direction;if(_angle==0){_baseDelta=0;_phase=1;}}
            else if(_smallAngle==0){_x2=0;_phase=2;}
            var position=o.BattlerPosition(_battler);o.SetBattlerPosition(_battler,new(unchecked((short)(_baseX+_baseDelta)),position.Y));
            o._offsets[_battler]=new(unchecked((short)_x2),o._offsets[_battler].Y);
        }
    }
    private sealed class ImprisonOrbs:VisualTask
    {
        private readonly PixelPoint _center;private readonly int _radius;private readonly SourceSpriteRecipe _recipe;private readonly List<ImprisonOrb> _children=[];private int _phase,_timer,_attempt,_alphaA=16,_alphaB;
        public ImprisonOrbs(SourceBattleAnimation o){var b=o._battlers[o._cue.Actor.Value];_center=b.Center(o._doubles,true);_radius=Math.Max(b.SourceWidth/3,b.SourceHeight/3);_recipe=o._script.SpriteRecipes[ImprisonOrbTemplate];o.SetAlphaBlend(0,63,16,0);}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    if(++_timer>8){_timer=0;int slot=o.FindFreeSpriteSlot();if(slot<64){PixelPoint offset=_attempt switch{0=>new(_radius,-_radius),1=>new(-_radius,_radius),2=>new(_radius,_radius),3=>new(-_radius,-_radius),_=>default};var child=new ImprisonOrb(slot,_recipe,_center,offset);_children.Add(child);o._sprites.Add(child);o._oamY[slot]=0;}if(++_attempt==5)_phase++;}break;
                case 1:
                    if((_timer&1)!=0)_alphaA--;else _alphaB++;o._alphaA=_alphaA;o._alphaB=_alphaB;
                    if(++_timer==32){foreach(var child in _children){child.Destroy();o._sprites.Remove(child);}_phase++;}break;
                case 2:_phase++;break;
                default:o.ClearAlphaBlend();Finished=true;break;
            }
        }
    }
    private sealed class ImprisonOrb(int slot,SourceSpriteRecipe recipe,PixelPoint position,PixelPoint offset):VisualSprite(slot,0)
    {
        public override bool Counted=>false;
        public void Destroy()=>Finished=true;
        public override void Step(SourceBattleAnimation o){}
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,offset,0,true,false,true,256,recipe.Asset,0){Width=16,Height=16};
    }
}
