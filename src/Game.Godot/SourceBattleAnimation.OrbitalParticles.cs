namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static readonly string[] MagicalLeafTags=["ANIM_TAG_LEAF","ANIM_TAG_RAZOR_LEAF"];
    private static readonly ushort[] MagicalLeafColors=[31,31|(19<<5),31|(31<<5),31<<5,5|(14<<5)|(31<<10),22|(10<<5)|(31<<10),22|(21<<5)|(31<<10)];
    private bool ValidateOrbitalParticleCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op is "playsewithpan" or "loopsewithpan"&&name is "SE_M_REVERSAL" or "SE_M_REFLECT" or "SE_M_SKY_UPPERCUT" or "SE_M_ENCORE" or "SE_M_SKETCH")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:2)||SoundPan(a[1]) is < -64 or >63||!_script.SoundEffects.ContainsKey(name))Fail(c,"Orbital particle sound operands or dependency differ.");
            if(c.Op=="loopsewithpan"&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Orbital particle sound loop differs.");
            return true;
        }
        if(c.Op=="createvisualtask"&&name=="AnimTask_CycleMagicalLeafPal")
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Magical Leaf palette task operands differ.");
            foreach(string tag in MagicalLeafTags)
                if(!_script.SpriteTags.TryGetValue(tag,out int id)||!_script.SpritePalettes.TryGetValue(id,out var palette)||palette.Length!=16)Fail(c,"Magical Leaf requires both indexed source palettes.");
            ValidateOrbitalParticleClosure(c);return true;
        }
        if(c.Op!="createsprite"||name is not ("gHiddenPowerOrbSpriteTemplate" or "gHiddenPowerOrbScatterSpriteTemplate" or "gSprayWaterDropletSpriteTemplate"))return false;
        bool spray=name=="gSprayWaterDropletSpriteTemplate",orbit=name=="gHiddenPowerOrbSpriteTemplate";
        if(a.Length!=(orbit||spray?5:4)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Orbital particle source operands differ.");
        if(spray){if(Number(a[3]) is <0 or >1||Number(a[4]) is <0 or >1)Fail(c,"Water droplet direction or participant differs.");RequireVisualSeed(c);}
        else if(Number(a[orbit?4:3]) is <0 or >255)Fail(c,"Hidden Power radius duration or angle differs.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Orbital particle recipe is missing.");
        r=_script.SpriteRecipes[name];
        if(r.Callback!=(spray?"AnimSprayWaterDroplet":orbit?"AnimOrbitFast":"AnimOrbitScatter")||r.Asset!=(spray?"sweat-bead":"red-orb")||r.Width!=(spray?8:16)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize!=(!spray&&!orbit)||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Orbital particle source frame recipe differs.");
        SourceSpriteAffineCommand[] affine=[new("frame",128,128,0,0,0),new("frame",8,8,0,1,0),new("jump",0,0,0,0,1)];
        if(r.AffineAnimations.Length!=(spray?0:1)||!spray&&!r.AffineAnimations[0].SequenceEqual(affine)||!spray&&_script.SineTable.Length!=320)Fail(c,"Orbital particle source affine or sine recipe differs.");
        if(!spray)ValidateOrbitalParticleClosure(c);
        return true;
    }
    private void ValidateOrbitalParticleClosure(SourceAnimationCommand c)
    {
        if(!_validatingInvokedProgram)return;
        var commands=_script.Commands;
        if(commands.Any(x=>x.Op is "call" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(c,"Orbital particles require a bounded linear source closure.");
        int orbs=commands.Count(x=>x.Op=="createsprite"&&x.Args[0] is "gHiddenPowerOrbSpriteTemplate" or "gHiddenPowerOrbScatterSpriteTemplate");
        if(orbs>14||commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_CycleMagicalLeafPal")>1)Fail(c,"Orbital particle closure exceeds reviewed source capacity.");
        if(orbs!=0&&commands.Any(x=>x.Op=="createsprite"&&x.Args[0] is not ("gHiddenPowerOrbSpriteTemplate" or "gHiddenPowerOrbScatterSpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_ScaleMonAndRestore" or "AnimTask_BlendMonInAndOut")))Fail(c,"Orbital particle companions exceed the reviewed affine capacity.");
        if(orbs!=0&&_spriteScene.Sprites.Any(s=>s.Frame.Affine||s.Frame.Scale!=256))Fail(c,"Orbital particle closure cannot prove retained affine matrix capacity.");
        if(orbs!=0&&Enumerable.Range(0,SourceSpriteRegistry<VisualSprite>.Capacity).Count(i=>!_battlers.Values.Any(b=>b.Visible&&b.Slot.Value==i)&&!_spriteScene.Sprites.Any(s=>s.Slot==i))<orbs)Fail(c,"Orbital particle closure lacks source sprite cells.");
        if(c.Args[0] is "AnimTask_CycleMagicalLeafPal" or "gHiddenPowerOrbSpriteTemplate")
        {
            int start=Array.IndexOf(commands,c);int signal=Array.FindIndex(commands,start+1,x=>x.Op=="setarg"&&x.Args[0]=="7"&&unchecked((short)AnimationArgument(x.Args[1]))==-1);
            if(signal<0||commands.Skip(start+1).Take(signal-start-1).Any(x=>x.Op=="waitforvisualfinish"))Fail(c,"Orbital particle stop signal must precede its visual wait.");
        }
    }
    private bool TryCreateOrbitalParticle(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not ("gHiddenPowerOrbSpriteTemplate" or "gHiddenPowerOrbScatterSpriteTemplate" or "gSprayWaterDropletSpriteTemplate"))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Orbital particle graphics have not been loaded.");
        _sprites.Add(new OrbitalParticle(slot,priority,r,this));return true;
    }
    private bool TryCreateOrbitalParticleTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_CycleMagicalLeafPal")return false;
        int[] slots=MagicalLeafTags.Select(t=>FindSpritePalette(_script.SpriteTags[t])).ToArray();
        if(MagicalLeafTags.Any(t=>!_loaded.Contains(t))||slots.Any(s=>s==255))Fail(c,"Magical Leaf palettes have not been loaded.");
        // Its synchronous phase zero only caches banks; a pre-existing stop signal still destroys it.
        var task=new MagicalLeafPaletteTask(slots);task.Initialize(this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private sealed class MagicalLeafPaletteTask(int[] slots):VisualTask
    {
        private int _coefficient,_color;private short _delay;
        public void Initialize(SourceBattleAnimation o){if(o._animationArgs[7]==-1)Finished=true;}
        public override void Step(SourceBattleAnimation o)
        {
            _delay=unchecked((short)(_delay+1));if(_delay>=0)
            {
                _delay=0;foreach(int slot in slots)for(int i=0;i<16;i++)o._spritePaletteBanks[slot][i]=SourcePaletteMath.Blend(o._spritePaletteUnfadedBanks[slot][i],_coefficient,MagicalLeafColors[_color]);
                if(++_coefficient==17){_coefficient=0;if(++_color==7)_color=0;}
            }
            if(o._animationArgs[7]==-1)Finished=true;
        }
    }
    private sealed class OrbitalParticle:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly bool _orbit,_spray;private readonly int _basePriority;
        private short _duration,_angle,_radiusX,_radiusY,_ticks,_phase,_vx,_vy,_direction;private int _priority;
        public OrbitalParticle(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);_orbit=r.Callback=="AnimOrbitFast";_spray=r.Callback=="AnimSprayWaterDroplet";_priority=priority;
            var a=o._animationArgs;var actor=o._battlers[o._cue.Actor.Value];_position=actor.Center(o._doubles,true);
            if(_spray)
            {
                int v1=o.NextVisualRandom()&511,v2=o.NextVisualRandom()&127;_vx=(short)((v1&1)!=0?736+v1:736-v1);_vy=(short)((v2&1)!=0?896+v2:896-v2);_direction=a[0];
                var p=o._battlers[(a[1]==0?o._cue.Actor:o._cue.Target).Value].Base(o._doubles);_position=new(p.X,p.Y+32);
                // Dummy END performs no FRAME write: preserve the callback's raw HFLIP bit.
            }
            else
            {
                _affine=new(r.AffineAnimations[0]);
                if(_orbit){_duration=a[0];_angle=a[1];_basePriority=actor.Subpriority;Step(o);}
                else{_vx=unchecked((short)((o._script.SineTable[a[0]]*10)>>8));_vy=unchecked((short)((o._script.SineTable[a[0]+64]*7)>>8));}
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,_spray?_direction!=0:f.HFlip,false,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=!_spray,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();if(!_orbit)_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_spray)
            {
                _offset=new(unchecked((short)(_offset.X+(_direction==0?1:-1)*(_vx>>8))),unchecked((short)(_offset.Y-(_vy>>8))));_vy=unchecked((short)(_vy-32));if(_vx<0)_vx=0;if(++_ticks==31)Finished=true;return;
            }
            if(!_orbit)
            {
                _offset=new(unchecked((short)(_offset.X+_vx)),unchecked((short)(_offset.Y+_vy)));
                if(unchecked((uint)(_position.X+_offset.X+16))>272||_position.Y+_offset.Y>160||_position.Y+_offset.Y< -16)Finished=true;return;
            }
            _priority=unchecked((byte)(_basePriority+(_angle is >=64 and <192?1:-1)));
            _offset=new(unchecked((short)((o._script.SineTable[_angle]*(_radiusX>>8))>>8)),unchecked((short)((o._script.SineTable[_angle+64]*(_radiusY>>8))>>8)));_angle=(short)((_angle+9)&255);
            switch(_phase)
            {
                case 0:_radiusX=unchecked((short)(_radiusX+1024));_radiusY=unchecked((short)(_radiusY+256));if(++_ticks==_duration){_ticks=0;_phase=1;}break;
                case 1:_radiusX=unchecked((short)(_radiusX-1024));_radiusY=unchecked((short)(_radiusY-256));if(++_ticks==_duration){_phase=2;return;}break;
            }
            if(o._animationArgs[7]==-1)Finished=true;
        }
    }
}
