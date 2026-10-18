namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string CureOverlay="cure-bubbles";
    private static bool CureTemplate(string name)=>name is "gAromatherapySmallFlowerSpriteTemplate" or "gAromatherapyBigFlowerSpriteTemplate" or "gSparklingStarsSpriteTemplate" or "gBlendThinRingExpandingSpriteTemplate" or "gBellSpriteTemplate" or "gHealBellMusicNoteSpriteTemplate" or "gSilverWindBigSparkSpriteTemplate" or "gSilverWindMediumSparkSpriteTemplate" or "gSilverWindSmallSparkSpriteTemplate";
    private bool ValidateCureCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        bool mask=c.Op=="createvisualtask"&&name=="AnimTask_StatusClearedEffect";
        bool music=c.Op=="createvisualtask"&&name is "AnimTask_LoadMusicNotesPals" or "AnimTask_FreeMusicNotesPals";
        if(mask||music)
        {
            if(a.Length!=(mask?3:2)||Number(a[1]) is <0 or >255||mask&&Number(a[2]) is not(0 or 1))Fail(c,"Cure task source operands differ.");
            if(_validatingInvokedProgram)
            {
                var closure=OverlayInvocationCommands();
                if(mask)
                {
                    if(!_script.OverlayDependencies.Contains(CureOverlay)||!_script.OverlayPalettes.TryGetValue(CureOverlay,out var colors)||colors.Length!=16)
                        Fail(c,"Cure task requires its indexed BG1 material and palette.");
                    if(closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==name)!=1||
                        closure.Any(x=>x.Op is "setalpha" or "blendoff" or "fadetobg" or "restorebg" or "changebg" or "monbg_static" or "visible" or "invisible" or "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||
                            x.Op=="createvisualtask"&&x.Args[0] is not("AnimTask_StatusClearedEffect" or "AnimTask_BlendBattleAnimPal" or "AnimTask_BlendBattleAnimPalExclude")||
                            x.Op=="createsprite"&&!CureTemplate(x.Args[0])&&x.Args[0] is not("gSimplePaletteBlendSpriteTemplate" or "gGrantingStarsSpriteTemplate" or "gThinRingExpandingSpriteTemplate")))
                        Fail(c,"Cure OBJ-window requires one source owner and its reviewed palette/particle companions.");
                    int at=Array.IndexOf(closure.ToArray(),c);
                    if(closure.Skip(at+1).Any(x=>x.Op is "monbg" or "clearmonbg"))Fail(c,"A later battler-background owner conflicts with the cure window.");
                    int needed=1+(Number(a[2])!=0&&_doubles&&IsSourceBattlerVisible(_cue.Actor.Value^2)?1:0);
                    int free=Enumerable.Range(0,64).Count(i=>!IsReservedSpriteSlot(i)&&_spriteScene.Sprites[i] is null);
                    if(!IsSourceBattlerPresent(_cue.Actor.Value)||free<needed)Fail(c,"Native cure copy requires present actor and available raw sprite cells.");
                }
                else
                {
                    int tag=_script.SpriteTags.GetValueOrDefault("ANIM_TAG_MUSIC_NOTES_2",-1);
                    if(!_script.SpritePaletteGroups.TryGetValue(tag,out var banks)||banks.Length!=3||banks.Any(p=>p.Length!=16)||
                        !_script.SpritePalettes.TryGetValue(tag,out var first)||!banks[0].SequenceEqual(first))
                        Fail(c,"Heal Bell requires all three original music-note palette banks.");
                    if(closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_LoadMusicNotesPals")!=1||
                        closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_FreeMusicNotesPals")!=1||
                        closure.Any(x=>x.Op=="createvisualtask"&&x.Args[0] is not("AnimTask_LoadMusicNotesPals" or "AnimTask_FreeMusicNotesPals" or "AnimTask_BlendBattleAnimPal" or "AnimTask_BlendBattleAnimPalExclude")))
                        Fail(c,"Heal Bell palette banks require one reviewed load/free owner.");
                }
            }
            return true;
        }
        if(c.Op!="createsprite"||!CureTemplate(name))return false;
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Cure source sprite recipe is missing.");r=_script.SpriteRecipes[name];
        bool flower=r.Callback=="AnimFlyingParticle",star=r.Callback=="AnimSparklingStars",note=r.Callback=="AnimHealBellMusicNote",ring=r.Callback=="AnimBlendThinRing",bell=name=="gBellSpriteTemplate";
        if(!flower&&!star&&!note&&!ring&&!bell||a.Length!=(flower||star||note?10:7)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0))
            Fail(c,"Cure sprite source shape or callback operands differ.");
        if(flower)
        {
            bool big=name=="gAromatherapyBigFlowerSpriteTemplate",silver=r.Asset=="sparkle-6";
            int scale=name=="gSilverWindMediumSparkSpriteTemplate"?192:name=="gSilverWindSmallSparkSpriteTemplate"?143:256;
            int rotation=name=="gSilverWindMediumSparkSpriteTemplate"?-12:name=="gSilverWindSmallSparkSpriteTemplate"?-15:-10;
            if(r.Asset!=(silver?"sparkle-6":"flower")||silver!=(name is "gSilverWindBigSparkSpriteTemplate" or "gSilverWindMediumSparkSpriteTemplate" or "gSilverWindSmallSparkSpriteTemplate")||r.Width!=(big||silver?16:8)||r.Height!=r.Width||r.Blend||r.AffineDoubleSize||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(big||silver?0:4,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{silver?0:-1})||r.AffineAnimations.Length!=(big||silver?1:0)||
                Number(a[5]) is <0 or >255||Number(a[6]) is <1 or >255||Number(a[7]) is <0 or >255||Number(a[8]) is <0 or >3||Number(a[9]) is <0 or >1||_script.SineTable.Length!=320)
                Fail(c,"Flying particle source programs, phase, positive speed or participants differ.");
            if((big||silver)&&!r.AffineAnimations[0].SequenceEqual(new SourceSpriteAffineCommand[]{new("frame",scale,scale,0,0,0),new("frame",0,0,silver?rotation&255:4,1,0),new("jump",0,0,0,0,1)}))Fail(c,"Flying particle source rotation differs.");
            if(silver&&_validatingInvokedProgram)ValidateItemWindCapacity(c);
        }
        else if(star)
        {
            if(!_script.SpriteRecipes.TryGetValue("gGrantingStarsSpriteTemplate",out var original)||r.Asset!=original.Asset||r.Width!=original.Width||r.Height!=original.Height||r.Blend||r.AffineAnimations.Length!=0||r.AffineDoubleSize||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(original.Animations[0])||!r.LoopStarts.SequenceEqual(original.LoopStarts)||Number(a[5]) is <0 or >1||Number(a[8]) is <0 or >32767||Number(a[9]) is <0 or >1)
                Fail(c,"Sparkling-star shared frame or coordinate mode differs.");
        }
        else if(ring)
        {
            if(!_script.SpriteRecipes.TryGetValue("gThinRingExpandingSpriteTemplate",out var original)||r.Asset!=original.Asset||r.Width!=64||r.Height!=64||!r.Blend||!r.AffineDoubleSize||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(original.Animations[0])||!r.LoopStarts.SequenceEqual(original.LoopStarts)||r.AffineAnimations.Length!=original.AffineAnimations.Length||r.AffineAnimations.Where((p,i)=>!p.SequenceEqual(original.AffineAnimations[i])).Any()||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1)
                Fail(c,"Blend ring shared frame/affine or coordinate mode differs.");
        }
        else if(note)
        {
            if(r.Asset!="music-notes-2"||r.Width!=16||r.Height!=16||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||Number(a[7]) is <1 or >32767||Number(a[8]) is <0 or >7||Number(a[9]) is <0 or >5)
                Fail(c,"Music note source tile, palette selector or duration differs.");
        }
        else if(r.Callback!="AnimSpriteOnMonPos"||r.Asset!="bell"||r.Width!=32||r.Height!=32||r.Blend||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new SourceSpriteAnimFrame[]{new(0,6,false,false),new(16,6,false,false),new(32,15,false,false),new(16,6,false,false),new(0,6,false,false),new(16,6,true,false),new(32,15,true,false),new(16,6,true,false),new(0,6,false,false),new(16,6,false,false),new(32,15,false,false),new(16,6,false,false),new(0,6,false,false)})||!r.LoopStarts.SequenceEqual(new[]{-1})||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1)
            Fail(c,"Bell source frame/position recipe differs.");
        return true;
    }
    private bool TryCreateCureTask(SourceAnimationCommand c)
    {
        string name=c.Args[0];
        if(name=="AnimTask_StatusClearedEffect"){_tasks.Add(new CureScrollingMaskTask(this,_animationArgs[0]!=0));return true;}
        if(name is not("AnimTask_LoadMusicNotesPals" or "AnimTask_FreeMusicNotesPals"))return false;
        int tag=_script.SpriteTags["ANIM_TAG_MUSIC_NOTES_2"];int[] tags=[tag,9999,9998];
        if(name=="AnimTask_FreeMusicNotesPals"){foreach(int t in tags)FreeSpritePalette(t);return true;}
        int first=FindSpritePalette(tag);
        if(first==255||_spritePaletteTags.Count(t=>t==65535)-4<2)throw new InvalidDataException("Native music palette allocation lacks its loaded bank or two free banks.");
        int[] slots=[first,AllocateSpritePalette(tags[1]),AllocateSpritePalette(tags[2])];var banks=_script.SpritePaletteGroups[tag];
        for(int i=0;i<3;i++){ResetPaletteTransform(16+slots[i]);LoadSpritePaletteRange(slots[i],0,banks[i]);}
        return true;
    }
    private PixelPoint CureAveragePosition(int slot,bool pic)
    {
        PixelPoint Position(int n)=>pic?_battlers[n].Center(_doubles,true):_battlers[n].Base(_doubles);
        var p=Position(slot);if(!_doubles||!IsSourceBattlerVisible(slot^2))return p;var q=Position(slot^2);return new((p.X+q.X)/2,(p.Y+q.Y)/2);
    }
    private bool TryCreateCureSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!CureTemplate(c.Args[0]))return false;var r=_script.SpriteRecipes[c.Args[0]];var a=_animationArgs.Select(x=>(int)x).ToArray();
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Cure graphics are not loaded.");
        if(r.Callback=="AnimFlyingParticle")_sprites.Add(new CureFlowerSprite(slot,priority,r,this,a));
        else if(r.Callback=="AnimHealBellMusicNote")_sprites.Add(new CureMusicNoteSprite(slot,priority,r,this,a));
        else if(r.Callback=="AnimSparklingStars")
        {
            int target=a[2]==0?_cue.Actor.Value:_cue.Target.Value;var p=CureAveragePosition(target,a[6]!=0);
            p=new(unchecked((short)(p.X+InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));
            _sprites.Add(new ElementalSprite(slot,priority,r with{Callback="AnimGrantingStars"},Resolve(c.Args[1]),a,this,p));
        }
        else
        {
            if(r.Callback=="AnimBlendThinRing")
            {
                int target=a[2]==0?_cue.Actor.Value:_cue.Target.Value;
                if(_doubles&&IsSourceBattlerVisible(target^2))
                {
                    int average=CureAveragePosition(target,(a[3]^1)!=0).X;
                    int origin=_battlers[target].Base(_doubles).X;
                    _animationArgs[0]=unchecked((short)((target&1)!=0?a[0]-((average-origin)-a[0]):average-origin));a[0]=_animationArgs[0];
                }
            }
            _sprites.Add(new ElementalSprite(slot,priority,r with{Callback="AnimSpriteOnMonPos"},Resolve(c.Args[1]),a,this));
        }
        return true;
    }
    private sealed class CureScrollingMaskTask:VisualTask
    {
        private readonly ObjectWindowCopy[] _copies;private short _phase,_delay,_alpha,_hold,_fraction,_scroll;
        public CureScrollingMaskTask(SourceBattleAnimation o,bool partner)
        {
            int actor=o._cue.Actor.Value;int[] bodies=partner&&o._doubles&&o.IsSourceBattlerVisible(actor^2)?[actor,actor^2]:[actor];
            int free=Enumerable.Range(0,64).Count(i=>!o.IsReservedSpriteSlot(i)&&o._spriteScene.Sprites[i] is null);
            if(!o.IsSourceBattlerPresent(actor)||free<bodies.Length)throw new InvalidDataException("Cure raw sprite allocation is exhausted.");
            o._window0=new(false,0,0,63,63);o.SetObjectWindow(true);o.SetAlphaBlend(2,63,0,16);o.SetBackgroundPriority(1,0);
            _copies=bodies.Select(b=>{var copy=new ObjectWindowCopy(o.RequireFreeSpriteSlot(),o,b);o._sprites.Add(copy);return copy;}).ToArray();
            o.BeginOverlay(CureOverlay);o.SetOverlayScroll(0,0);
        }
        public override void Step(SourceBattleAnimation o)
        {
            _fraction=unchecked((short)(_fraction+0x1a0));_scroll=unchecked((short)(_scroll+(_fraction>>8)));_fraction&=255;o.SetOverlayScroll(0,_scroll);
            switch(_phase)
            {
                case 0:if(_delay++>=2){_delay=0;_alpha++;o._alphaA=_alpha;o._alphaB=16-_alpha;if(_alpha==10)_phase++;}break;
                case 1:if(++_hold==30)_phase++;break;
                case 2:
                    if(_delay++<2)break;_delay=0;_alpha--;o._alphaA=_alpha;o._alphaB=16-_alpha;if(_alpha!=0)break;
                    o.EndOverlay(false);o._window0=new(false,0,0,63,63);o.SetObjectWindow(false);o.ClearAlphaBlend();o._blendSecondTargets=0;
                    foreach(var copy in _copies)o._sprites.Remove(copy);Finished=true;break;
            }
        }
    }
    private sealed class CureFlowerSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private readonly int _amplitude,_speed,_increment,_priority;private short _timer;private int _angle;
        public CureFlowerSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o,int[] a):base(slot,priority)
        {
            _recipe=r;_frames=new(r);if(r.AffineAnimations.Length!=0)_affine=new(r.AffineAnimations[0]);int b=a[6]==0?o._cue.Actor.Value:o._cue.Target.Value;
            _amplitude=a[1];_angle=a[2];_speed=(b&1)==0?-a[3]:a[3];_increment=a[4];_priority=(o.BattlerBackgroundPriority(b)+(a[5] is 1 or 3?1:0))&3;
            int y=a[5] switch{2=>o._battlers[b].Center(o._doubles,true).Y+a[0],3=>o._battlers[o._cue.Target.Value].Center(o._doubles,true).Y+a[0],_=>a[0]};
            _position=new((b&1)==0?256:-16,unchecked((short)y));
        }
        public override void Step(SourceBattleAnimation o)
        {
            int age=_timer;_timer=unchecked((short)(_timer+1));_offset=new(unchecked((short)(_speed*age)),unchecked((short)(_amplitude*o._script.SineTable[_angle]>>8)));_angle=(_increment*age)&255;
            if(_speed>0?_position.X+_offset.X>247:_position.X+_offset.X<=-16){Finished=true;return;}_frames.Step();_affine?.Step();
        }
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine?.Matrix??new(256,0,0,256);var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,OamPriority=_priority,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class CureMusicNoteSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PhysicalLinear _linear;private readonly PixelPoint _position;private PixelPoint _offset;private readonly int _tile,_palette;private bool _followup;
        public CureMusicNoteSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o,int[] a):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var origin=o._battlers[o._cue.Actor.Value].Base(o._doubles);_position=new(unchecked((short)(origin.X+o.InitialXDirection()*a[0])),unchecked((short)(origin.Y+a[1])));
            if(!o._cue.Actor.IsPlayer)o._animationArgs[2]=unchecked((short)-a[2]);
            var end=new PixelPoint(unchecked((short)(origin.X+o._animationArgs[2])),unchecked((short)(origin.Y+a[3])));_linear=new(end.X-_position.X,end.Y-_position.Y,a[4]);
            _tile=((a[6]&1)!=0?32:0)+(a[5]<<2);int tag=(a[6]>>1) switch{0=>o._script.SpriteTags["ANIM_TAG_MUSIC_NOTES_2"],1=>9999,_=>9998};_palette=o.FindSpritePalette(tag)&15;
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_followup){Finished=true;return;}if(_linear.Step(out var p))_followup=true;else _offset=p;_frames.Step();
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_tile,true,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,PaletteSlot=_palette};
    }
}
