namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string PerishNote="gPerishSongMusicNoteSpriteTemplate",PerishPalette="gPerishSongMusicNote2SpriteTemplate",DestinyShadow="gDestinyBondWhiteShadowSpriteTemplate",CurseNail="gCurseNailSpriteTemplate",CurseSpirit="gCurseGhostSpriteTemplate";
    private static bool GhostSongSprite(string name)=>name is PerishNote or PerishPalette or CurseNail or CurseSpirit;
    private static bool GhostSongTask(string name)=>name is "AnimTask_DestinyBondWhiteShadow" or "AnimTask_CurseStretchingBlackBg" or "AnimTask_DrawFallingWhiteLinesOnAttacker";
    private bool ValidateGhostSongCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";bool task=c.Op=="createvisualtask"&&GhostSongTask(name),sprite=c.Op=="createsprite"&&GhostSongSprite(name);
        if(!task&&!sprite)return false;
        bool destiny=name=="AnimTask_DestinyBondWhiteShadow",note=name is PerishNote or PerishPalette;
        int count=task?(destiny?4:2):note?6:3;
        if(a.Length!=count||Number(a[1]) is <0 or >255||sprite&&(Number(a[1])>1||Number(a[2]) is <0 or >127)||a.Skip(task?2:3).Any(x=>AnimationArgument(x) is <short.MinValue or >short.MaxValue))Fail(c,"Ghost/song source operand widths differ.");
        if(destiny&&(AnimationArgument(a[2])<0||AnimationArgument(a[3])<1)||name==PerishNote&&AnimationArgument(a[4]) is <0 or >7||name==PerishPalette&&AnimationArgument(a[3]) is <0 or >120)Fail(c,"Ghost/song frame or bounded duration differs.");
        if(sprite)ValidateGhostSongRecipe(c,name);if(destiny)ValidateGhostSongRecipe(c,DestinyShadow);
        if(!_validatingInvokedProgram)return true;
        var closure=OverlayInvocationCommands();
        if(closure.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn"))Fail(c,"Ghost/song requires an acyclic direct or committed two-branch closure.");
        if(closure.Count(x=>x.Op=="choosetwoturnanim")>1)Fail(c,"Ghost/song has multiple branch owners.");
        int counted=closure.Count(x=>x.Op=="createsprite"),raw=closure.Any(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_DestinyBondWhiteShadow")?2:closure.Any(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_DrawFallingWhiteLinesOnAttacker")?1:0;
        if(counted+raw>Enumerable.Range(0,64).Count(i=>!IsReservedSpriteSlot(i)&&_spriteScene.Sprites[i] is null))Fail(c,"Ghost/song raw and counted sprites exceed source capacity.");
        int affine=closure.Count(x=>x.Op=="createsprite"&&x.Args[0] is PerishNote or PerishPalette);
        if(4+affine+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Ghost/song source affine capacity is exhausted.");
        int tasks=1+_tasks.Count(t=>!t.Finished)+closure.Count(x=>x.Op is "createvisualtask" or "createsoundtask" or "clearmonbg" or "fadetobg" or "fadetobgfromset" or "restorebg")+closure.Count(x=>x.Op=="monbg")*(_doubles?2:1);
        if(tasks>16)Fail(c,"Ghost/song task bound includes the script and background controllers.");
        if(note)
        {
            if(closure.Any(x=>x.Op is "setalpha" or "blendoff" or "monbg" or "fadetobg" or "restorebg" or "choosetwoturnanim"||x.Op=="createsprite"&&x.Args[0] is not(PerishNote or PerishPalette or "gSimplePaletteBlendSpriteTemplate")||x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_SetGrayscaleOrOriginalPal"))Fail(c,"Perish notes have an unreviewed palette/register companion.");
        }
        else
        {
            string[] allowedTasks=destiny?["AnimTask_DestinyBondWhiteShadow","AnimTask_ShakeMonInPlace","AnimTask_BlendBattleAnimPalExclude"]:["AnimTask_CurseStretchingBlackBg","AnimTask_DrawFallingWhiteLinesOnAttacker","AnimTask_ShakeMon2","AnimTask_SwayMon","AnimTask_BlendColorCycle"];
            if(closure.Any(x=>x.Op is "setalpha" or "monbg_static" or "clearmonbg_static"||x.Op=="createvisualtask"&&!allowedTasks.Contains(x.Args[0])||x.Op=="createsprite"&&x.Args[0] is not(CurseNail or CurseSpirit or "gSimplePaletteBlendSpriteTemplate")))Fail(c,"Ghost alpha/window owner has an unreviewed companion.");
            var owners=closure.Select((x,i)=>(x,i)).Where(p=>p.x.Op=="createvisualtask"&&GhostSongTask(p.x.Args[0])||p.x.Op=="createsprite"&&p.x.Args[0] is CurseNail or CurseSpirit).ToArray();
            if(owners.GroupBy(p=>p.x.Args[0]).Any(g=>g.Count()!=1))Fail(c,"Ghost register owner is duplicated.");
            for(int i=1;i<owners.Length;i++)if(!closure.Skip(owners[i-1].i+1).Take(owners[i].i-owners[i-1].i-1).Any(x=>x.Op=="waitforvisualfinish"))Fail(c,"Ghost register owners are not separated by visual completion.");
        }
        if(destiny)
        {
            if(!_script.SpriteDependencies.Contains(DestinyShadow))Fail(c,"Destiny shadow raw child dependency is missing.");
            foreach(var b in _battlers.Values.Where(b=>b.Slot.Value==_cue.Actor.Value||IsSourceBattlerVisible(b.Slot.Value)))ValidateReactionGeometry(c,b);
        }
        if(name=="AnimTask_DrawFallingWhiteLinesOnAttacker")
        {
            if(!IsSourceBattlerPresent(_cue.Actor.Value)||!_script.OverlayDependencies.Contains("curse")||!_script.OverlayPalettes.TryGetValue("curse",out var colors)||colors.Length!=16||colors[1]!=32767)Fail(c,"Curse copy requires its source actor and indexed white-line material.");
        }
        return true;
    }
    private void ValidateGhostSongRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Ghost/song source recipe is missing.");return;}
        bool note=name is PerishNote or PerishPalette;string asset=note?"music-notes-2":name==DestinyShadow?"white-shadow":name==CurseNail?"nail":"ghostly-spirit";
        string callback=name switch{PerishNote=>"AnimPerishSongMusicNote",PerishPalette=>"AnimPerishSongMusicNote2",DestinyShadow=>"AnimDestinyBondWhiteShadow",CurseNail=>"AnimCurseNail",_=>"AnimGhostStatusSprite"};
        if(r.Asset!=asset||r.Callback!=callback||r.Width!=(note?16:name==DestinyShadow?64:32)||r.Height!=(note||name==CurseNail?16:32)||r.Blend==note||r.AffineDoubleSize||r.Subsprites.Length!=0||r.FrameLoops.Any(x=>x.Length!=0)||r.Animations.Length!=(note?8:1)||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(note?-1:0,note?8:1))||r.AffineAnimations.Length!=(note?3:0))Fail(c,"Ghost/song source shape or program count differs.");
        for(int i=0;i<r.Animations.Length;i++)if(!r.Animations[i].SequenceEqual(new[]{new SourceSpriteAnimFrame(note?(i%6)*4:0,note?(i==2?41:10):1,false,note&&i>=6)}))Fail(c,"Ghost/song source frame commands differ.");
        if(note)for(int i=0;i<3;i++)if(!r.AffineAnimations[i].SequenceEqual(new SourceSpriteAffineCommand[]{new("frame",0,0,i==0?0:i==1?248:8,i==0?5:16,0),new("end",0,0,0,0,0)}))Fail(c,"Perish source affine program differs.");
        if(note||name==CurseSpirit)if(_script.SineTable.Length!=320)Fail(c,"Ghost/song sine data is missing.");
        RequireOverlaySpritePalette(c,"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));
    }
    private bool TryCreateGhostSongSprite(SourceAnimationCommand c,int slot,int priority)
    {
        string name=c.Args[0];if(!GhostSongSprite(name))return false;var r=_script.SpriteRecipes[name];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Ghost/song graphics are not loaded.");
        _sprites.Add(name is PerishNote or PerishPalette?new PerishSongSprite(slot,priority,r,this):new CurseSprite(slot,priority,r,this));return true;
    }
    private bool TryCreateGhostSongTask(SourceAnimationCommand c)
    {
        switch(c.Args[0])
        {
            case "AnimTask_DestinyBondWhiteShadow":if(!_loaded.Contains("ANIM_TAG_WHITE_SHADOW"))Fail(c,"Destiny shadow graphics are not loaded.");_tasks.Add(new DestinyShadowTask(this,_animationArgs[0],_animationArgs[1]));return true;
            case "AnimTask_CurseStretchingBlackBg":_tasks.Add(new CurseBlackWindowTask(this));return true;
            case "AnimTask_DrawFallingWhiteLinesOnAttacker":_tasks.Add(new CurseLinesTask(this));return true;
            default:return false;
        }
    }
    // effects_2.c:3741–3835. The second note is an invisible palette controller.
    private sealed class PerishSongSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram _affine;private PixelPoint _position,_offset;private short _age,_angle,_wobble,_velocity,_height,_bounces;private int _phase,_delay;private bool _hidden,_restart;
        public PerishSongSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;var a=o._animationArgs;bool controller=r.Template==PerishPalette;_frames=new(r,controller?0:a[1]);_affine=new(r.AffineAnimations[0]);_position=controller?o._battlers[o._cue.Target.Value].Center(o._doubles,true):new(120,unchecked((short)(a[0]/2-15)));_delay=120-a[0];_angle=a[2];_hidden=controller;Step(o);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==PerishPalette)
            {
                _age=unchecked((short)(_age+1));if(_age==_delay)o.SetSpriteGreyscaleOrOriginal(o.FindSpritePalette(o._script.SpriteTags["ANIM_TAG_MUSIC_NOTES_2"]),false);if(_age==_delay+80)Finished=true;return;
            }
            if(_phase==0)
            {
                _age++;_wobble=(short)((_wobble+10)&255);int angle=(_age*3+unchecked((ushort)_angle))&255;
                _offset=new(o._script.SineTable[angle+64]*100>>8,_age/2+(o._script.SineTable[angle]*10>>8)+(o._script.SineTable[_wobble+64]*4>>8));
                if(_age>120){_phase=1;_age=0;_position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;_velocity=5;_height=_bounces=0;_affine=new(_recipe.AffineAnimations[1]);_restart=true;}
            }
            else if(_phase==1){if(++_age>10){_age=0;_phase=2;}}
            else{_height=unchecked((short)(_height+_velocity));_offset=new(0,_height);_velocity++;if(_height>48&&_velocity>0){_velocity=(short)(_bounces-5);_bounces++;}if(_bounces>3){_hidden=_velocity%2!=0;Finished=true;}}
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();if(_restart)_restart=false;else _affine.Step();}
        public override SourceVisualSpriteFrame Frame{get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_hidden,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class DestinyShadowSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private PixelPoint _position;private short _x,_y,_dx,_dy,_remaining;
        public DestinyShadowSprite(int slot,SourceSpriteRecipe r,PixelPoint start,PixelPoint end,int duration):base(slot,55)
        {_recipe=r;_frames=new(r);_position=start;_x=unchecked((short)(start.X<<4));_y=unchecked((short)(start.Y<<4));_dx=unchecked((short)(((end.X-start.X)<<4)/duration));_dy=unchecked((short)(((end.Y-start.Y)<<4)/duration));_remaining=(short)duration;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){if(_remaining==0)return;_x=unchecked((short)(_x+_dx));_y=unchecked((short)(_y+_dy));_position=new(_x>>4,_y>>4);if(--_remaining==0)_x=0;}
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,true,false,true,256,_recipe.Asset,Subpriority){Width=64,Height=32};
    }
    // ghost.c:786–923. Children use raw CreateSprite and do not own visual counts.
    private sealed class DestinyShadowTask:VisualTask
    {
        private readonly List<DestinyShadowSprite> _children=[];private int _phase,_timer,_toggle,_alpha,_beta=16,_hold;private bool _peak;
        public DestinyShadowTask(SourceBattleAnimation o,int hold,int duration)
        {
            _hold=hold;o.SetAlphaBlend(0,63,0,16);var b=o._battlers[o._cue.Actor.Value];var p=b.Center(o._doubles,true);var start=new PixelPoint(p.X,p.Y+b.SourceHeight/2);
            foreach(var target in o._battlers.Values.OrderBy(x=>x.Slot.Value).Where(x=>(x.Slot.Value&1)!=(o._cue.Actor.Value&1)&&o.IsSourceBattlerVisible(x.Slot.Value)))
            {int slot=o._sprites.FindFreeSlot(o.IsReservedSpriteSlot);if(slot==64)continue;var end=target.Center(o._doubles,true);var child=new DestinyShadowSprite(slot,o._script.SpriteRecipes[DestinyShadow],start,new(end.X,end.Y+target.SourceHeight/2),duration);o._sprites.Add(child);_children.Add(child);}
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    if(!_peak&&++_timer>1){_timer=0;if((++_toggle&1)!=0){if(_alpha<16)_alpha++;}else if(_beta>0)_beta--;o._alphaA=_alpha;o._alphaB=_beta;if(_toggle>=24){_toggle=0;_peak=true;}}
                    if(_hold>0)_hold--;else if(_peak)_phase++;break;
                case 1:
                    if(++_timer>1){_timer=0;if((++_toggle&1)!=0){if(_alpha>0)_alpha--;}else if(_beta<16)_beta++;o._alphaA=_alpha;o._alphaB=_beta;if(_alpha==0&&_beta==16){foreach(var child in _children)o._sprites.Remove(child);_phase++;}}break;
                case 2:if(++_timer>0)_phase++;break;
                default:o.ClearAlphaBlend();o._blendSecondTargets=0;Finished=true;break;
            }
        }
    }
    private sealed class CurseSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private PixelPoint _position,_offset;private int _phase,_wait=60,_steps,_timer,_alpha,_beta=5,_angle,_height,_age,_direction;private bool _hidden,_flip;
        public CurseSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);_position=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
            if(r.Template==CurseNail){var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(unchecked((short)(p.X+o.InitialXDirection()*o._animationArgs[0]+(o._cue.Actor.IsPlayer?24:-24))),unchecked((short)(p.Y+o._animationArgs[1])));_direction=o._cue.Actor.IsPlayer?-2:2;_flip=o._cue.Actor.IsPlayer;}
            else Step(o);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==CurseSpirit)
            {
                if(_phase==1){o.ClearAlphaBlend();o._blendSecondTargets=0;Finished=true;return;}
                _offset=new((o._cue.Actor.IsPlayer?1:-1)*(o._script.SineTable[_angle]*12>>8),-(++_height));_angle=(_angle+6)&255;
                if(++_age==1){_alpha=11;o.SetAlphaBlend(0,63,_alpha,_beta);}else if(_age>30){_beta=Math.Min(16,_beta+1);_alpha=Math.Max(0,_alpha-1);o._alphaA=_alpha;o._alphaB=_beta;if(_alpha==0&&_beta==16){_hidden=true;_phase=1;}}return;
            }
            switch(_phase)
            {
                case 0:
                    if(_wait>0){_wait--;break;}_offset=new(unchecked((short)(_offset.X+_direction)),0);if(unchecked((ushort)(_offset.X+7))>14){_position=new(unchecked((short)(_position.X+_offset.X)),_position.Y);_offset=default;if(++_steps==3){_wait=30;_phase=1;}else _wait=40;}break;
                case 1:if(_wait>0)_wait--;else _phase=2;break;
                case 2:o.SetAlphaBlend(0,63,16,0);_timer=_alpha=0;_phase=3;break;
                case 3:if(_timer<2)_timer++;else{_timer=0;_alpha++;o._alphaA=16-_alpha;o._alphaB=_alpha;if(_alpha==16){_hidden=true;_phase=4;}}break;
                default:o.ClearAlphaBlend();o._blendSecondTargets=0;o._window0=new(false,0,0,63,63);Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+(_recipe.Template==CurseNail?_steps*8:0),!_hidden,_flip,true,256,_recipe.Asset,Subpriority){Width=32,Height=_recipe.Height};
    }
    private sealed class CurseBlackWindowTask:VisualTask
    {
        private int _step;private bool _waiting;
        public CurseBlackWindowTask(SourceBattleAnimation o){int x=o._cue.Actor.IsPlayer?200:40;o._window0=new(true,(ushort)(x<<8|x),40<<8|40,63,31);o.SetBrightness(8,SourceBrightnessMode.Darken,16);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_waiting){if(o.PaletteFadeActive)return;o._window0=new(false,0,0,63,63);o.ClearAlphaBlend();o._blendSecondTargets=0;Finished=true;return;}
            int x=o._cue.Actor.IsPlayer?200:40,n=_step++;int left=n<16?(x*16-x*n)/16:0,right=n<16?x+(240-x)*n/16:240,top=n<16?(640-40*n)/16:0,bottom=n<16?40+72*n/16:112;
            o._window0=new(true,(ushort)(left<<8|right),(ushort)(top<<8|bottom),63,31);
            if(n>=16){if(!o.PaletteFadeActive){o._fade=new(o.SelectPalettes(1),16,16,0);o._fade.Update(o);}_waiting=true;}
        }
    }
    // utility_funcs.c:288–393. The source loads only BG bank eight, index one.
    // Its copy shares the actor matrix; this authored lifetime has no matrix writer.
    private sealed class CurseLinesTask:VisualTask
    {
        private readonly ObjectWindowCopy _copy;private readonly int _partner;private int _scroll,_loops,_x,_y;
        public CurseLinesTask(SourceBattleAnimation o)
        {
            int actor=o._cue.Actor.Value;_partner=o._doubles&&actor is 0 or 3&&o.IsSourceBattlerVisible(actor^2)?actor^2:-1;
            o._window0=new(false,0,0,63,63);o.SetObjectWindow(true);o.SetAlphaBlend(2,63,8,12);o.SetBackgroundPriority(1,_partner<0?0:1);
            if(_partner>=0)o.SetBattlerObjectPriority(_partner,(o.BattlerObjectPriority(_partner)-1)&3);
            _copy=new(o.RequireFreeSpriteSlot(),o,actor);o._sprites.Add(_copy);o.BeginOverlayMaterial("curse");o.LoadBackgroundPaletteRange(8,1,new ushort[]{32767});var p=o.BattlerPosition(actor);_x=-p.X+32;_y=-p.Y+32;o.SetOverlayScroll(_x,_y);
        }
        public override void Step(SourceBattleAnimation o)
        {
            _scroll+=4;_y-=4;if(_scroll==64){_scroll=0;_y+=64;if(++_loops==4){o.EndOverlay(false);o._window0=new(false,0,0,63,63);o.SetObjectWindow(false);o.ClearAlphaBlend();o._blendSecondTargets=0;o._sprites.Remove(_copy);if(_partner>=0)o.SetBattlerObjectPriority(_partner,(o.BattlerObjectPriority(_partner)+1)&3);Finished=true;return;}}o.SetOverlayScroll(_x,_y);
        }
    }
}
