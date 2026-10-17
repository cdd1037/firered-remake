namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private static readonly string[] MusicPaletteTags=["ANIM_TAG_MUSIC_NOTES","ANIM_TAG_BENT_SPOON","ANIM_TAG_SPHERE_TO_CUBE","ANIM_TAG_LARGE_FRESH_EGG"];
    // sParticlesColorBlendTable writes only faded entries 1..5, retaining every other bank entry.
    private static readonly ushort[][] MusicColors=[
        [32767,31|(26<<5)|(28<<10),31|(22<<5)|(26<<10),31|(17<<5)|(24<<10),31|(13<<5)|(22<<10)],
        [32767,25|(31<<5)|(26<<10),20|(31<<5)|(21<<10),15|(31<<5)|(16<<10),10|(31<<5)|(12<<10)],
        [32767,31|(31<<5)|(24<<10),31|(31<<5)|(17<<10),31|(31<<5)|(10<<10),31|(31<<5)|(3<<10)],
        [32767,26|(28<<5)|(31<<10),21|(26<<5)|(31<<10),16|(24<<5)|(31<<10),12|(22<<5)|(31<<10)]];
    private bool ValidateMusicCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="createvisualtask"&&a.FirstOrDefault() is "AnimTask_MusicNotesRainbowBlend" or "AnimTask_MusicNotesClearRainbowBlend")
        {if(a.Length!=2||Number(a[1]) is <0 or >255||MusicPaletteTags.Any(t=>!_script.SpriteTags.ContainsKey(t)))Fail(c,"Music palette task dependencies or operands differ.");return true;}
        if(c.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimWavyMusicNotes" or "AnimSlowFlyingMusicNotes" or "AnimBellyDrumHand"))return false;
        var r=_script.SpriteRecipes[a[0]];bool hand=r.Callback=="AnimBellyDrumHand",slow=r.Callback=="AnimSlowFlyingMusicNotes";
        if(a.Length!=(hand?4:slow?7:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||r.Blend||r.Width!=(hand?32:16)||r.Height!=(hand?32:16)||r.AffineDoubleSize==hand||r.AffineAnimations.Length!=(hand?0:1))Fail(c,"Music sprite recipe or operands differ.");
        if(hand){if(Number(a[3]) is <0 or >1)Fail(c,"Belly Drum hand selector differs.");}
        else if(MusicPaletteTags.Any(t=>!_script.SpriteTags.ContainsKey(t))||_script.SineTable.Length!=320||Number(a[slow?4:3])<0||Number(a[slow?4:3])>=r.Animations.Length||Number(a[slow?5:4]) is <0 or >3||slow&&(Number(a[3]) is <0 or >1||Number(a[6]) is <0 or >255)||!slow&&Number(a[5])<0)Fail(c,"Music frame, palette or phase operand differs.");
        return true;
    }
    private bool TryCreateMusicTask(SourceAnimationCommand c)
    {
        bool clear=c.Args[0]=="AnimTask_MusicNotesClearRainbowBlend";
        if(!clear&&c.Args[0]!="AnimTask_MusicNotesRainbowBlend")return false;
        if(clear){for(int i=1;i<4;i++)FreeSpritePalette(_script.SpriteTags[MusicPaletteTags[i]]);return true;}
        for(int i=0;i<4;i++)
        {int tag=_script.SpriteTags[MusicPaletteTags[i]],slot=i==0?FindSpritePalette(tag):AllocateSpritePalette(tag);if(slot!=255)WriteFadedSpritePalette(slot,1,MusicColors[i]);}
        return true;
    }
    private bool TryCreateMusicSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var r)||r.Callback is not ("AnimWavyMusicNotes" or "AnimSlowFlyingMusicNotes" or "AnimBellyDrumHand"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Music graphics are not loaded.");
        _sprites.Add(new MusicSprite(slot,priority,r,c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class MusicSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly PhysicalLinear? _linear;
        private readonly bool _hand,_slow,_flip;private PixelPoint _position,_offset;private int _paletteSlot,_paletteRow,_cycle,_blendTimer,_timer,_phase,_state;private short _x,_y,_vx,_vy;
        public MusicSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] a,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_hand=recipe.Callback=="AnimBellyDrumHand";_slow=recipe.Callback=="AnimSlowFlyingMusicNotes";_frames=new(recipe,_hand?0:a[_slow?1:0]);
            _position=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_paletteSlot=o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')]);
            _paletteSlot&=15; // CreateSprite stores lookup result in the four-bit OAM palette field.
            if(_hand){_flip=a[0]==1;_position=new(_position.X+(_flip?16:-16),_position.Y+8);_timer=8;return;}
            _affine=new(recipe.AffineAnimations[0]);_paletteRow=a[_slow?2:1];SelectPalette(o);
            if(_slow){_position=new(_position.X,_position.Y+8);_linear=new(a[0]==0?-32:32,-40,40);_phase=a[3];}
            else
            {
                var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);int dx=(byte)target.X-_position.X,dy=(byte)target.Y-_position.Y;
                int x2=dx*256,time=x2/(dx<0?-40:40);if(time==0)time=1;
                _vx=(short)(x2/time);_vy=(short)(dy*256/time);_x=(short)(_position.X<<4);_y=(short)(_position.Y<<4);_cycle=a[2];
            }
        }
        private void SelectPalette(SourceBattleAnimation o){int slot=o.FindSpritePalette(o._script.SpriteTags[MusicPaletteTags[_paletteRow]]);if(slot!=255)_paletteSlot=slot;}
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,_flip||_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D,PaletteSlot=_paletteSlot};}}
        public override void Step(SourceBattleAnimation o)
        {
            if(_hand){if(_state==1)Finished=true;else if(_timer>0)_timer--;else _state=1;}
            else if(_slow)
            {
                if(_linear!.Step(out var p))Finished=true;
                else{int dx=(o._script.SineTable[_phase]*8)>>8;if(p.X<0)dx=-dx;_offset=new(p.X+dx,p.Y+((o._script.SineTable[_phase]*4)>>8));_phase=(_phase+8)&255;}
            }
            else
            {
                _timer=(short)(_timer+1);int phase=(_timer*5)&255;_x+=_vx;_y+=_vy;_position=new(_x>>4,_y>>4);_offset=new(0,(o._script.SineTable[phase]*15)>>8);
                if(_position.X is < -16 or >256||_position.Y is < -16 or >128)Finished=true;
                else if(_cycle!=0){_blendTimer=(short)(_blendTimer+1);if(_blendTimer>_cycle){_blendTimer=0;_paletteRow=(_paletteRow+1)&3;SelectPalette(o);}}
            }
            if(!Finished){_frames.Step();_affine?.Step();}
        }
    }
}
