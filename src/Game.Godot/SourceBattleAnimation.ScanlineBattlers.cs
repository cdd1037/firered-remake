namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string DigPlume="gDirtPlumeSpriteTemplate",DigMound="gDirtMoundSpriteTemplate",SketchPencil="gPencilSpriteTemplate",DanceOrb="gDragonDanceOrbSpriteTemplate";
    private static bool ScanlineBattlerTask(string n)=>n is "AnimTask_DigDownMovement" or "AnimTask_DigUpMovement" or "AnimTask_SketchDrawMon" or "AnimTask_DragonDanceWaver" or "AnimTask_AcidArmor" or "AnimTask_BlendPalInAndOutByTag";
    private bool ValidateScanlineBattlerCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";bool task=c.Op=="createvisualtask"&&ScanlineBattlerTask(n),sprite=c.Op=="createsprite"&&n is DigPlume or DigMound or SketchPencil or DanceOrb;
        if(!task&&!sprite)return false;
        int width=n switch{DigPlume=>9,DigMound=>6,SketchPencil=>3,DanceOrb=>4,"AnimTask_BlendPalInAndOutByTag"=>7,"AnimTask_DigDownMovement" or "AnimTask_DigUpMovement" or "AnimTask_AcidArmor"=>3,_=>2};
        if(a.Length!=width||Number(a[task?1:2])<0||Number(a[task?1:2])>(task?255:127)||sprite&&Number(a[1]) is <0 or >1)Fail(c,"Battler scanline operand width differs.");
        if(n=="AnimTask_BlendPalInAndOutByTag")
        {
            if(!_script.SpriteTags.ContainsKey(a[2])||PaletteNumber(a[3]) is <0 or >32767||Number(a[4]) is <1 or >16||Number(a[5]) is <0 or >32767||Number(a[6]) is <1 or >32767)Fail(c,"Tagged palette pulse operands differ.");
            RequireOverlaySpritePalette(c,a[2]);
        }
        else if(a.Skip(task?2:3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Battler scanline source words differ.");
        if(n is "AnimTask_DigDownMovement" or "AnimTask_DigUpMovement" or "AnimTask_AcidArmor"&&Number(a[2]) is <0 or >1||n is DigPlume or DigMound&&(Number(a[3]) is <0 or >1||Number(a[4]) is <0 or >1)||n==DigPlume&&Number(a[8])<=0||n==DigMound&&Number(a[5])<0||n==DanceOrb&&Number(a[3]) is <0 or >255)Fail(c,"Battler scanline participant, phase or duration differs.");
        if(sprite)
        {
            if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Battler scanline recipe missing.");return true;}
            string asset=n==DigPlume?"mud-sand":n==DigMound?"dirt-mound":n==SketchPencil?"pencil":"hollow-orb";
            string callback=n==DigPlume?"AnimDirtPlumeParticle":n==DigMound?"AnimDigDirtMound":n==SketchPencil?"AnimPencil":"AnimDragonDanceOrb";
            int w=n==DigPlume?8:n==DanceOrb?16:32,h=n==DigMound?16:w;
            if(r.Asset!=asset||r.Callback!=callback||r.Width!=w||r.Height!=h||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.AffineAnimations.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,1,false,false)})||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(f=>f.Length!=0))Fail(c,"Battler scanline static source recipe differs.");
            if(n!=DigPlume)RequireOverlaySpritePalette(c,"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));
        }
        if(_script.SineTable.Length!=320)Fail(c,"Battler scanline sine table missing.");
        if(_validatingInvokedProgram)
        {
            foreach(var b in _battlers.Values)ValidateReactionGeometry(c,b);
            ValidateScanlineBattlerCapacity(c);
        }
        return true;
    }
    private void ValidateScanlineBattlerCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();string n=c.Args[0];int family=n is DigPlume or DigMound or "AnimTask_DigDownMovement" or "AnimTask_DigUpMovement"?0:n is SketchPencil or "AnimTask_SketchDrawMon"?1:n is DanceOrb or "AnimTask_DragonDanceWaver" or "AnimTask_BlendPalInAndOutByTag"?2:3;
        bool Sprite(string name)=>family==0?name is DigPlume or DigMound or "gBasicHitSplatSpriteTemplate":family==1?name==SketchPencil:family==2?name==DanceOrb:false;
        bool Task(string name)=>family==0?name is "AnimTask_DigDownMovement" or "AnimTask_DigUpMovement" or "AnimTask_ShakeMon":family==1?name is "AnimTask_SketchDrawMon" or "AnimTask_Splash":family==2?name is "AnimTask_DragonDanceWaver" or "AnimTask_BlendPalInAndOutByTag":name=="AnimTask_AcidArmor";
        if(commands.Any(x=>x.Op=="createsprite"&&!Sprite(x.Args[0])||x.Op=="createvisualtask"&&!Task(x.Args[0])))Fail(c,"Battler scanline companion owners exceed source closure.");
        var own=commands.Where(x=>x.Op=="createvisualtask"&&ScanlineBattlerTask(x.Args[0])).ToArray();
        if(own.GroupBy(x=>x.Args[0]+(family==0?":"+x.Args[2]:"")).Any(g=>g.Count()>1))Fail(c,"Duplicate source scanline or pulse owner.");
        int births=commands.Count(x=>x.Op=="createsprite"),retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length));
        if(22+births+retained>64||4+commands.Count(x=>x.Op=="createsprite"&&x.Args[0]=="gBasicHitSplatSpriteTemplate")+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32)Fail(c,"Battler scanline closure exceeds source object capacity.");
        int tasks=1+commands.Count(x=>x.Op=="createvisualtask"||x.Op is "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse")+commands.Count(x=>x.Op=="monbg")*(_doubles?2:1)+commands.Count(x=>x.Op is "clearmonbg" or "clearmonbg_static");
        if(tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Battler scanline closure exceeds source task capacity.");
    }
    // GetBattlerYCoordWithElevation uses raw battle coordinates, not picture Y offset.
    private int ScanlineBattlerY(int slot){var b=_battlers[slot];return (b.Base(_doubles).Y-(b.Slot.IsPlayer?0:b.Elevation))&255;}
    private bool TryCreateScanlineBattlerTask(SourceAnimationCommand c)
    {
        string n=c.Args[0];if(!ScanlineBattlerTask(n))return false;
        if(n=="AnimTask_BlendPalInAndOutByTag")
        {int bank=FindSpritePalette(_script.SpriteTags[c.Args[2]]);if(bank!=255)_tasks.Add(new BlendMonInAndOutTask(bank,(ushort)PaletteNumber(c.Args[3]),Number(c.Args[4]),Number(c.Args[5]),Number(c.Args[6])));return true;}
        var task=new BattlerScanlineTask(n,this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private bool TryCreateScanlineBattlerSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(DigPlume or DigMound or SketchPencil or DanceOrb))return false;var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Battler scanline sprite graphics not loaded.");
        _sprites.Add(new BattlerScanlineSprite(slot,priority,r,this));return true;
    }
    private sealed class BattlerScanlineTask:VisualTask
    {
        private readonly string _name;private readonly short[] _d=new short[16];private readonly int _slot,_rank;private readonly int _mode;
        private static short S(int value)=>unchecked((short)value);
        private static void Both(SourceBattleAnimation o,int i,int value){o.ScanlineBuffers[0][i]=o.ScanlineBuffers[1][i]=unchecked((ushort)value);}
        public BattlerScanlineTask(string name,SourceBattleAnimation o)
        {
            _name=name;_mode=o._animationArgs[0];_slot=name=="AnimTask_SketchDrawMon"||name=="AnimTask_AcidArmor"&&_mode!=0?o._cue.Target.Value:o._cue.Actor.Value;_rank=o._battlers[_slot].BackgroundRank;
            var bg=o.ScanlineBackgroundOffset(_rank);int y=o.ScanlineBattlerY(_slot);
            if(name is "AnimTask_DigDownMovement" or "AnimTask_DigUpMovement"){Step(o);return;}
            if(name=="AnimTask_SketchDrawMon")
            {_d[0]=S(y+32);_d[1]=4;_d[6]=S(bg.X);_d[15]=S(o._battlers[_slot].SourceHeight);for(int i=_d[0]-64;i<=_d[0];i++)if(i>=0)Both(o,i,_d[6]+240);o.StartScanline(_rank);}
            else if(name=="AnimTask_DragonDanceWaver")
            {_d[2]=S(bg.X);_d[3]=S(Math.Max(0,y-32));_d[4]=S(y+32);for(int i=_d[3];i<=_d[4];i++)Both(o,i,_d[2]);o.StartScanline(_rank);}
            else
            {_d[3]=16;_d[5]=S(_slot);_d[6]=32;_d[8]=S(o._battlers[_slot].Slot.IsPlayer?24:-24);_d[13]=S(Math.Max(0,y-34));_d[14]=S(_d[13]+66);_d[15]=S(_slot);o.SetAlphaBlend(1<<_rank,63,o._alphaA,o._alphaB);for(int i=0;i<160;i++){Both(o,i*2,bg.X);Both(o,i*2+1,bg.Y);}o.StartScanline(_rank,true);}
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_name=="AnimTask_DigDownMovement"){DigDown(o);return;}if(_name=="AnimTask_DigUpMovement"){DigUp(o);return;}
            if(_name=="AnimTask_AcidArmor"){Acid(o);return;}
            if(_name=="AnimTask_SketchDrawMon")
            {
                if(_d[4]==0){if(++_d[5]>20)_d[4]++;}
                else if(++_d[1]>3){_d[1]=0;_d[2]=S(_d[3]&3);_d[5]=S(_d[0]-_d[3]+(_d[2]==1?-2:_d[2] is 2 or 3?1:0));if(_d[5]>=0)Both(o,_d[5],_d[6]);if(++_d[3]>=_d[15]){o.StopScanlineDeferred();Finished=true;}}return;
            }
            switch(_d[0])
            {
                case 0:if(++_d[7]>1){_d[7]=0;if(++_d[6]==3)_d[0]++;}Dance(o);break;
                case 1:if(++_d[1]>60)_d[0]++;Dance(o);break;
                case 2:if(++_d[7]>1){_d[7]=0;if(--_d[6]==0)_d[0]++;}Dance(o);break;
                case 3:o.StopScanlineDeferred();_d[0]++;break;
                case 4:Finished=true;break;
            }
        }
        private void Dance(SourceBattleAnimation o){int angle=_d[5];for(int i=_d[3];i<=_d[4];i++){o.ScanlineBuffers[o.ScanlineWriteBuffer][i]=unchecked((ushort)(((o._script.SineTable[angle]*_d[6])>>7)+_d[2]));angle=(angle+8)&255;}_d[5]=S((_d[5]+9)&255);}
        private void DigMask(SourceBattleAnimation o,int start,int end){int x=o.ScanlineBackgroundOffset(_rank).X;int y=Math.Max(0,start);for(;y<end;y++)Both(o,y,x);for(;y<160;y++)Both(o,y,x+240);o.StartScanline(_rank);}
        private void DigDown(SourceBattleAnimation o)
        {
            if(_mode!=0){o.SetSourceBattlerSpriteInvisible(_slot,true);o._offsets[_slot]=default;var p=o.ScanlineBackgroundOffset(_rank);o.SetScanlineBackgroundOffset(_rank,new(p.X,0));Finished=true;return;}
            switch(_d[0])
            {
                case 0:var p=o.ScanlineBackgroundOffset(_rank);int y=o.ScanlineBattlerY(_slot);_d[10]=S(_slot);_d[11]=S(_rank);_d[12]=S(p.X);_d[13]=S(p.Y);_d[14]=S(Math.Max(0,y-32));_d[15]=S(y+32);o.SetSourceBattlerSpriteInvisible(_slot,true);_d[0]++;break;
                case 1:DigMask(o,_d[14],_d[15]);_d[0]++;break;
                case 2:_d[2]=S((_d[2]+6)&127);if(++_d[4]>2){_d[4]=0;_d[3]++;}_d[5]=S(_d[3]+(o._script.SineTable[_d[2]]>>4));o.SetScanlineBackgroundOffset(_rank,new(_d[12],S(_d[13]-_d[5])));if(_d[5]>63){_d[5]=S(120-_d[14]);o.SetScanlineBackgroundOffset(_rank,new(_d[12],S(_d[13]-_d[5])));o._offsets[_slot]=new(272-o.BattlerPosition(_slot).X,o._offsets[_slot].Y);_d[0]++;}break;
                case 3:o.StopScanlineDeferred();_d[0]++;break;
                case 4:Finished=true;o.SetSourceBattlerSpriteInvisible(_slot,true);break;
            }
        }
        private void DigUp(SourceBattleAnimation o)
        {
            if(_mode==0){if(_d[0]==0){_d[10]=S(_slot);o.SetSourceBattlerSpriteInvisible(_slot,false);o._offsets[_slot]=new(0,160-o.BattlerPosition(_slot).Y);_d[0]++;}else Finished=true;return;}
            switch(_d[0])
            {
                case 0:int y=o.ScanlineBattlerY(_slot);_d[10]=S(_slot);_d[11]=S(_rank);_d[12]=S(o.ScanlineBackgroundOffset(_rank).X);_d[14]=S(y-32);_d[15]=S(y+32);_d[0]++;break;
                case 1:DigMask(o,0,_d[15]);_d[0]++;break;
                case 2:o._offsets[_slot]=new(o._offsets[_slot].X,96);_d[0]++;break;
                case 3:o._offsets[_slot]=new(o._offsets[_slot].X,o._offsets[_slot].Y-8);if(o._offsets[_slot].Y==0){o.StopScanlineDeferred();_d[0]++;}break;
                case 4:Finished=true;break;
            }
        }
        private void Acid(SourceBattleAnimation o)
        {
            switch(_d[0])
            {
                case 0:
                    var bg=o.ScanlineBackgroundOffset(_rank);int offset=_d[14]*2,v1=0,v2=0,i=0;_d[1]=S((_d[1]+2)&255);int angle=_d[1];_d[9]=S(2016/_d[6]);_d[10]=S(-(_d[7]*2/_d[9]));_d[11]=_d[7];int v3=_d[11]>>5;_d[12]=S(v3);int line=_d[14];
                    while(line>_d[13]){var b=o.ScanlineBuffers[o.ScanlineWriteBuffer];b[offset+1]=unchecked((ushort)(i-v2+bg.Y));b[offset]=unchecked((ushort)(bg.X+v3+(o._script.SineTable[angle]>>5)));angle=(angle+10)&255;_d[11]=S(_d[11]+_d[10]);v3=_d[11]>>5;_d[12]=S(v3);i++;offset-=2;v1+=_d[6];v2=v1>>5;line--;}
                    for(int index=line*2;index>=0;index-=2)Both(o,index,bg.X+240);
                    if(++_d[6]>63){_d[6]=64;_d[2]++;if((_d[2]&1)!=0)_d[3]--;else _d[4]++;o.SetAlphaBlend(1<<_rank,63,_d[3],_d[4]);if(_d[3]==0&&_d[4]==16){_d[2]=_d[3]=0;_d[0]++;}}else _d[7]=S(_d[7]+_d[8]);break;
                case 1:if(++_d[2]>12){o.StopScanlineDeferred();_d[2]=0;_d[0]++;}break;
                case 2:_d[2]++;if((_d[2]&1)!=0)_d[3]++;else _d[4]--;o.SetAlphaBlend(1<<_rank,63,_d[3],_d[4]);if(_d[3]==16&&_d[4]==0){_d[2]=_d[3]=0;_d[0]++;}break;
                case 3:Finished=true;break;
            }
        }
    }
    private sealed class BattlerScanlineSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly short[] _d=new short[8];private PixelPoint _position,_offset;private readonly HorizontalArc? _arc;private bool _invisible,_destroyNext;private readonly int _tile;
        private static short S(int x)=>unchecked((short)x);
        public BattlerScanlineSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;var a=o._animationArgs;int target=recipe.Template==SketchPencil?o._cue.Target.Value:recipe.Template is DigPlume or DigMound?(a[0]==0?o._cue.Actor.Value:o._cue.Target.Value):o._cue.Actor.Value;var b=o._battlers[target];var p=b.Base(o._doubles);int y=o.ScanlineBattlerY(target);
            if(recipe.Template==DigMound){_position=new(p.X-16+a[1]*32,y+32);_tile=a[1]*8;_d[0]=a[2];}
            else if(recipe.Template==DigPlume){int x=24;if(a[1]==1){x=-24;a[2]=S(-a[2]);}_position=new(p.X+x,y+30);_arc=new(S(_position.X+a[2])-_position.X,S(_position.Y+a[3])-_position.Y,a[5],a[4]);}
            else if(recipe.Template==SketchPencil){_position=new(p.X-16,y+16);_d[3]=16;_d[5]=S(b.SourceHeight+2);_d[6]=S(o.AdjustPanning(63));}
            else{_position=b.Center(o._doubles,true);_d[5]=1;_d[6]=a[0];_d[7]=S(Math.Max(b.SourceHeight,b.SourceWidth)/2);Orbit(o);}
        }
        private void Orbit(SourceBattleAnimation o)=>_offset=new(o._script.SineTable[_d[6]+64]*_d[7]>>8,o._script.SineTable[_d[6]]*_d[7]>>8);
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template==DigMound){if(_destroyNext)Finished=true;else if(_d[0]>0)_d[0]--;else _destroyNext=true;return;}
            if(_arc is not null){if(_arc.Step(o._script.SineTable,out var offset))Finished=true;else _offset=offset;return;}
            if(_recipe.Template==SketchPencil){Pencil(o);return;}
            _d[6]=S((_d[6]-_d[5])&255);if(_d[0]==1&&_d[7]<=149)_d[7]=S(Math.Min(150,_d[7]+8));Orbit(o);if(++_d[4]>5){_d[4]=0;if(_d[5]<=15)_d[5]++;}
            if(++_d[3]>(_d[0]==0?60:20)){if(_d[0]==0){_d[3]=0;_d[0]++;}else Finished=true;}
        }
        private void Pencil(SourceBattleAnimation o)
        {
            if(_d[0] is 0 or 2){if(++_d[2]>1){_d[2]=0;_invisible=!_invisible;}if(++_d[1]>16){_invisible=false;if(_d[0]==0)_d[0]++;else Finished=true;}return;}
            if(++_d[1]>3&&_d[2]<_d[5]){_d[1]=0;_position=new(_position.X,_position.Y-1);_d[2]++;if(_d[2]%10==0)o._audio.Play(new("SE_M_SKETCH",_d[6],o._frame));}
            _d[4]=S(_d[4]+_d[3]);if(_d[4]>31){_d[4]=S(64-_d[4]);_d[3]=S(-_d[3]);}else if(_d[4]<=-32){_d[4]=S(-64-_d[4]);_d[3]=S(-_d[3]);}_offset=new(_d[4],0);if(_d[5]==_d[2]){_d[1]=_d[2]=0;_d[0]++;}
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_tile,!_invisible,false,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,OamPriority=2};
    }
}
