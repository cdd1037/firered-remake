namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string FocusEye="gOpeningEyeSpriteTemplate",FocusHalo="gWhiteHaloSpriteTemplate",FocusAlert="gTealAlertSpriteTemplate",FocusLock="gLockOnTargetSpriteTemplate",FocusCorner="gLockOnMoveTargetSpriteTemplate",SoundZ="gSnoreZSpriteTemplate",SoundNote="gJaggedMusicNoteSpriteTemplate",SoundRing="gUproarRingSpriteTemplate";
    private static string? FocusSoundAsset(string template)=>template switch{FocusEye=>"opening-eye",FocusHalo=>"round-white-halo",FocusAlert=>"teal-alert",FocusLock or FocusCorner=>"lock-on",SoundZ=>"snore-z",SoundNote=>"jagged-music-note",SoundRing=>"thin-ring",_=>null};
    private bool ValidateFocusSoundCommand(SourceAnimationCommand c)
    {
        string name=c.Args.FirstOrDefault()??"";bool task=c.Op=="createvisualtask"&&name=="AnimTask_UproarDistortion";
        if(!task&&(c.Op!="createsprite"||FocusSoundAsset(name) is null))return false;
        var a=c.Args;int width=task?3:name switch{FocusEye=>7,FocusHalo or FocusLock=>3,FocusCorner=>4,FocusAlert=>6,SoundZ=>10,SoundNote=>7,_=>9};
        if(a.Length!=width||Number(a[1]) is <0 or >255||!task&&(Number(a[1])>1||Number(a[2]) is <0 or >127)||a.Skip(task?2:3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Focus/sound source operands differ.");
        if(task)
        {
            SourceTaskAffineCommand[] expected=[new("frame",-12,8,0,4),new("frame",20,-20,0,4),new("frame",-8,12,0,4),new("end")];
            if(Number(a[2]) is <0 or >3||!_script.TaskAffinePrograms.TryGetValue("sUproarAffineAnimCmds",out var p)||!p.SequenceEqual(expected))Fail(c,"Uproar participant or source affine program differs.");
        }
        else
        {
            ValidateFocusSoundRecipe(c,name);
            if(name==FocusEye&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1)||name==FocusCorner&&Number(a[3]) is <1 or >4||name==FocusAlert&&Number(a[5]) is <1 or >32767||name==SoundZ&&(Number(a[7]) is <1 or >32767||Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >1)||name==SoundNote&&(Number(a[3]) is <0 or >1||Number(a[6]) is <0 or >1)||name==SoundRing&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1||Number(a[7]) is <0 or >32767||Number(a[8]) is <0 or >16))Fail(c,"Focus/sound source selectors or duration differ.");
            if(name is FocusLock or FocusCorner&&!new[]{"SE_M_LOCK_ON","SE_M_LEER"}.All(_script.SoundEffects.ContainsKey))Fail(c,"Lock-On callback sounds are missing.");
        }
        if(!_validatingInvokedProgram)return true;
        var commands=OverlayInvocationCommands();
        if(task&&SourceAnimBattlerSlot(Number(a[2])) is null)Fail(c,"Uproar affine participant has no source sprite.");
        if(Enumerable.Range(0,64).Count(i=>!IsReservedSpriteSlot(i)&&_spriteScene.Sprites[i] is null)<commands.Count(x=>x.Op=="createsprite"))Fail(c,"Focus/sound counted sprites exceed source scene capacity.");
        if((task||name is SoundNote or SoundRing)&&commands.Any(x=>x.Op is "setalpha" or "blendoff" or "fadetobg" or "restorebg" or "changebg" or "monbg_static" or "clearmonbg_static" or "createsoundtask"||x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_UproarDistortion"||x.Op=="createsprite"&&x.Args[0] is not(SoundNote or SoundRing)))Fail(c,"Uproar affine and palette companions exceed the reviewed closure.");
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(c,"Focus/sound source ownership requires a bounded closure.");
        if(task)
        {
            int taskBirths=1+_tasks.Count(t=>!t.Finished)+commands.Count(x=>x.Op=="createvisualtask"||x.Op=="createsoundtask")+commands.Count(x=>x.Op=="monbg")*(_doubles?2:1)+commands.Count(x=>x.Op=="clearmonbg");
            if(taskBirths>16)Fail(c,"Uproar source task capacity includes its script and background controllers.");
        }
        if(name is FocusAlert or SoundRing||task)
        {
            int affineBirths=commands.Count(x=>x.Op=="createsprite"&&x.Args[0] is FocusAlert or SoundRing);
            if(4+affineBirths+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Focus/sound source closure exceeds affine capacity.");
        }
        if(name==FocusHalo&&(commands.Count(x=>x.Op=="createsprite"&&x.Args[0]==FocusHalo)!=1||commands.Any(x=>x.Op is "setalpha" or "blendoff"||x.Op=="createsprite"&&x.Args[0] is not(FocusEye or FocusHalo or FocusAlert)||x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_BlendColorCycle")))Fail(c,"Mind Reader requires its sole halo alpha-register owner.");
        if(name is FocusLock or FocusCorner)
        {
            if(commands.Any(x=>x.Op=="createvisualtask"||x.Op=="createsprite"&&x.Args[0] is not(FocusLock or FocusCorner))||commands.Count(x=>x.Op=="createsprite"&&x.Args[0]==FocusLock)!=1||commands.Count(x=>x.Op=="setarg"&&Number(x.Args[0])==7)!=1||commands.Count(x=>x.Op=="setarg"&&Number(x.Args[0])==7&&unchecked((ushort)AnimationArgument(x.Args[1]))==65535)!=1)Fail(c,"Lock-On palette/register owner or stop signal differs.");
            int signal=Array.FindIndex(commands,x=>x.Op=="setarg"&&Number(x.Args[0])==7),first=Array.FindIndex(commands,x=>x.Op=="createsprite"&&x.Args[0] is FocusLock or FocusCorner);
            if(commands.Skip(first).Take(signal-first).Any(x=>x.Op=="waitforvisualfinish")||signal<first)Fail(c,"Lock-On stop signal is unreachable before its visual wait.");
        }
        return true;
    }
    private void ValidateFocusSoundRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Focus/sound source recipe is missing.");return;}
        string asset=FocusSoundAsset(name)!;string callback=name switch{FocusEye=>"AnimSpriteOnMonPos",FocusHalo=>"AnimWhiteHalo",FocusAlert=>"AnimTealAlert",FocusLock=>"AnimLockOnTarget",FocusCorner=>"AnimLockOnMoveTarget",SoundZ=>"AnimTravelDiagonally",SoundNote=>"AnimJaggedMusicNote",_=>"AnimUproarRing"};
        int size=name is FocusHalo or SoundRing?64:name==FocusCorner?16:32;
        SourceSpriteAnimFrame[] frames=name==FocusEye?[new(0,40,false,false),new(16,8,false,false),new(32,40,false,false)]:[new(0,1,false,false)];
        if(r.Asset!=asset||r.Callback!=callback||r.Width!=size||r.Height!=size||r.Blend!=(name is FocusHalo or SoundRing)||r.AffineDoubleSize!=(name==SoundRing)||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(frames)||!r.LoopStarts.SequenceEqual(new[]{name==FocusEye?-1:0})||r.FrameLoops.Any(x=>x.Length!=0)||r.AffineAnimations.Length!=(name==SoundRing?2:0))Fail(c,"Focus/sound source sprite shape or programs differ.");
        if(name==SoundRing)for(int i=0;i<2;i++)if(!r.AffineAnimations[i].SequenceEqual(new SourceSpriteAffineCommand[]{new("frame",16,16,0,0,0),new("frame",i==0?16:32,i==0?16:32,0,i==0?30:15,0),new("end",0,0,0,0,0)}))Fail(c,"Uproar ring source affine table differs.");
        RequireOverlaySpritePalette(c,"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));
    }
    private bool TryCreateFocusSoundTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_UproarDistortion")return false;
        if(SourceAnimBattlerSlot(_animationArgs[0]) is not {} slot)throw new InvalidDataException("Uproar affine participant has no source sprite.");
        _tasks.Add(new TaskAffinePlayback(this,new(slot),_script.TaskAffinePrograms["sUproarAffineAnimCmds"]));return true;
    }
    private bool TryCreateFocusSoundSprite(SourceAnimationCommand c,int slot,int priority)
    {
        string name=c.Args[0];if(FocusSoundAsset(name) is not {} asset)return false;
        string tag="ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_');if(!_loaded.Contains(tag))Fail(c,"Focus/sound source graphics are not loaded.");
        var r=_script.SpriteRecipes[name];var a=_animationArgs.Select(v=>(int)v).ToArray();
        if(name is FocusEye or SoundRing)
        {
            if(name==SoundRing)
            {
                int bank=FindSpritePalette(_script.SpriteTags[tag]);if(bank!=255)BlendSpritePaletteRange(bank,1,15,a[5],(ushort)a[4]);
                r=r with{Callback="AnimSpriteOnMonPos",AffineAnimations=[r.AffineAnimations[1]]};
            }
            _sprites.Add(new ElementalSprite(slot,priority,r,Resolve(c.Args[1]),a,this));
        }
        else if(name is FocusLock or FocusCorner)_sprites.Add(new LockOnSprite(slot,priority,r,this,name==FocusCorner?a[0]:0));
        else _sprites.Add(new FocusSoundSprite(slot,priority,r,this));return true;
    }
    private sealed class FocusSoundSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceAffineMatrix _matrix=new(256,0,0,256);
        private PixelPoint _position,_offset;private PhysicalLinear? _linear;private int _phase,_wait,_age,_alpha=7,_tile;private short _x,_y,_dx,_dy;private bool _hidden;
        public FocusSoundSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var a=o._animationArgs;var target=o._battlers[o._cue.Target.Value];_position=target.Center(o._doubles,true);
            switch(r.Template)
            {
                case FocusHalo:_wait=90;o.SetAlphaBlend(0,63,7,9);break;
                case FocusAlert:
                    _position=new(unchecked((short)(_position.X+o.InitialXDirection()*a[0])),unchecked((short)(_position.Y+a[1])));var end=target.Center(o._doubles,true);
                    ushort rotation=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(_position.X-end.X)),unchecked((short)(_position.Y-end.Y)))+0x6000));_matrix=SourceAffineMatrix.FromScaleRotation(256,256,rotation);_linear=new(end.X-_position.X,end.Y-_position.Y,a[2]);break;
                case SoundZ:
                    var chosen=o._battlers[(a[5]==0?o._cue.Actor:o._cue.Target).Value];var center=a[6]==0?chosen.Center(o._doubles,true):target.Base(o._doubles);
                    _position=new(unchecked((short)(center.X+o.InitialXDirection()*a[0]*(a[6]==0?2:1))),unchecked((short)(center.Y+a[1]*(a[6]==0?2:1))));
                    if(!o._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);var destination=a[6]==0?chosen.Center(o._doubles,true):chosen.Base(o._doubles);_linear=new(unchecked((short)(destination.X+a[2]))-_position.X,unchecked((short)(destination.Y+a[3]))-_position.Y,a[4]);break;
                case SoundNote:
                    var b=o._battlers[(a[0]==0?o._cue.Actor:o._cue.Target).Value];if(!b.Slot.IsPlayer)a[1]=unchecked((short)-a[1]);var p=b.Center(o._doubles,true);_position=new(unchecked((short)(p.X+a[1])),unchecked((short)(p.Y+a[2])));_x=unchecked((short)(_position.X<<3));_y=unchecked((short)(_position.Y<<3));_dx=a[1];_dy=a[2];_tile=a[3]*16;break;
            }
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==FocusHalo)
            {
                if(_phase==0){if(_wait>0)_wait--;else _phase=1;}
                else if(_phase==1){o._alphaA=_alpha;o._alphaB=16-_alpha;if(--_alpha<0){_hidden=true;_phase=2;}}
                else{o.ClearAlphaBlend();o._blendSecondTargets=0;Finished=true;}
            }
            else if(_recipe.Template==SoundNote){_x=unchecked((short)(_x+_dx));_y=unchecked((short)(_y+_dy));_position=new(_x>>3,_y>>3);if(++_age>16)Finished=true;}
            else if(_phase==0){if(_linear!.Step(out var p))_phase=1;else _offset=p;}
            else Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+_tile,!_hidden,false,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,Affine=_recipe.Template==FocusAlert,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D};
    }
    private sealed class LockOnSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly int _corner;private PixelPoint _position,_offset;private PhysicalLinear? _linear;
        private int _phase,_wait=20,_afterWait=1,_path,_alternate,_afterLinear,_coefficient,_reverse,_blinks,_age;private bool _hidden;
        public LockOnSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o,int corner):base(slot,priority)
        {
            _recipe=r;_frames=new(r);_corner=corner;var p=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_position=new(p.X-32+(corner==0?0:corner<=2?-24:24),p.Y-32+(corner==0?0:corner is 1 or 3?-24:24));
        }
        private void Wait(int frames,int next){_wait=frames;_afterWait=next;_phase=0;}
        private void Travel(PixelPoint end,int duration,int next){_linear=new(end.X-_position.X,end.Y-_position.Y,duration);_afterLinear=next;_phase=2;}
        private void Consolidate(){_position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:if(_wait>0)_wait--;else _phase=_afterWait;break;
                case 1:
                    if(_alternate==0)Wait(1,1);
                    else{Consolidate();PixelPoint delta=_path switch{0=>new(64,64),1=>new(0,-64),2=>new(-64,64),_=>new(32,-32)};Travel(new(_position.X+delta.X,_position.Y+delta.Y),8,3);_path++;o._audio.Play(new("SE_M_LOCK_ON",o.AdjustPanning(63),o._frame));}_alternate^=1;break;
                case 2:if(_linear!.Step(out var translatedOffset))_phase=_afterLinear;else _offset=translatedOffset;break;
                case 3:if(_path==4)Wait(10,4);else _phase=1;break;
                case 4:
                    if(_corner==0)Wait(3,5);
                    else{Consolidate();var p=o._battlers[o._cue.Target.Value].Center(o._doubles,true);Travel(new(p.X+(_corner<=2?-8:8),p.Y+(_corner is 1 or 3?-8:8)),6,6);}break;
                case 5:
                    _coefficient=Math.Clamp(_coefficient+(_reverse==0?3:-3),0,16);o.BlendPalettes(o.SelectPalettes(31),_coefficient,0x7fff);
                    if(_coefficient==16){_reverse++;int bank=o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_LOCK_ON"]);ushort[] colors=[o._spritePaletteUnfadedBanks[bank][8],o._spritePaletteUnfadedBanks[bank][9]];o.LoadSpritePaletteRange(bank,1,colors);o._audio.Play(new("SE_M_LEER",o.AdjustPanning(63),o._frame));}
                    else if(_coefficient==0)_phase=6;break;
                case 6:if(unchecked((ushort)o._animationArgs[7])==65535){_blinks=_age=0;_phase=7;}break;
                default:if(_age%3==0){_blinks++;_hidden=!_hidden;}_age++;if(_blinks==8)Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+(_corner==0?0:16),!_hidden,_corner>=3,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_corner is 2 or 4};
    }
}
