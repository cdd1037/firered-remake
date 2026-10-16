using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceElementalAnimationCatalog
{
    public static bool Supports(int move)=>move is 7 or 52 or 53 or 56 or 190 or 324 or 341 or 84 or 86 or 9 or 55 or 85 or 121 or 152 or 192 or 48 or 103 or 319 or 71 or 72 or 202 or 105 or 51 or 61 or 92 or 141 or 145;
    public static IReadOnlyDictionary<string,SourcePixelAsset> Assets(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path));
        return doc.RootElement.GetProperty("assets").EnumerateArray().Select(r=>r.GetProperty("id").GetString()!).ToDictionary(id=>id,id=>SourceHitAnimationCatalog.Asset(doc,Path.GetDirectoryName(path)!,id));
    }
    public static IReadOnlyDictionary<int,SourceAnimationScript> Read(string path,JsonDocument hitManifest)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path));var shared=SourceHitAnimationCatalog.Script(hitManifest,1);
        var recipes=shared.SpriteRecipes.Concat(SourceHitAnimationCatalog.Recipes(doc.RootElement.GetProperty("spriteRecipes"))).ToDictionary(p=>p.Key,p=>p.Value);
        static SourceAnimationCommand[] Commands(JsonElement rows)=>rows.EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray();
        var subs=doc.RootElement.GetProperty("subroutines").EnumerateObject().ToDictionary(p=>p.Name,p=>Commands(p.Value));
        var scripts=doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>
        {
            var commands=Commands(r.GetProperty("commands"));
            return new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,commands){SineTable=shared.SineTable,SpriteRecipes=recipes,Subroutines=commands.Where(c=>c.Op=="call").Select(c=>c.Args[0]).Distinct().ToDictionary(name=>name,name=>subs[name])};
        }).ToDictionary(s=>s.MoveId);
        if(scripts.Count==0||scripts.Keys.Any(id=>!Supports(id)))throw new InvalidDataException("Unadmitted elemental script.");return scripts;
    }
}

public sealed partial class SourceBattleAnimation
{
    private int _elementalSinTimer;
    private static string? ElementalCallback(string template)=>template switch
    {
        "gSupersonicRingSpriteTemplate" or "gScreechRingSpriteTemplate" or "gMetalSoundSpriteTemplate" or "gEmberSpriteTemplate"=>"TranslateAnimSpriteToTargetMonLocation",
        "gEmberFlareSpriteTemplate"=>"AnimEmberFlare",
        "gFireSpiralInwardSpriteTemplate"=>"AnimFireSpiralInward",
        "gFireSpreadSpriteTemplate"=>"AnimFireSpread",
        "gFlamethrowerFlameSpriteTemplate" or "gHydroPumpOrbSpriteTemplate" or "gMudShotOrbSpriteTemplate" or "gSignalBeamRedOrbSpriteTemplate" or "gSignalBeamGreenOrbSpriteTemplate"=>"AnimToTargetInSinWave",
        "gOctazookaBallSpriteTemplate" or "gZapCannonBallSpriteTemplate"=>"TranslateAnimSpriteToTargetMonLocation",
        "gToxicBubbleSpriteTemplate" or "gHealingBlueStarSpriteTemplate" or "gOctazookaSmokeSpriteTemplate" or "gExplosionSpriteTemplate"=>"AnimSpriteOnMonPos",
        "gWaterHitSplatSpriteTemplate"=>"AnimHitSplatBasic",
        "gWaterGunProjectileSpriteTemplate" or "gEggThrowSpriteTemplate"=>"AnimThrowProjectile",
        "gWaterGunDropletSpriteTemplate"=>"AnimWaterGunDroplet",
        "gSmallBubblePairSpriteTemplate"=>"AnimSmallBubblePair",
        "gZapCannonSparkSpriteTemplate"=>"AnimZapCannonSpark",
        "gSparkElectricityFlashingSpriteTemplate"=>"AnimSparkElectricityFlashing",
        "gThunderboltOrbSpriteTemplate"=>"AnimThunderboltOrb",
        "gWaterBubbleProjectileSpriteTemplate"=>"AnimWaterBubbleProjectile","gPoisonBubbleSpriteTemplate" or "gWaterBubbleSpriteTemplate"=>"AnimBubbleEffect","gAcidPoisonBubbleSpriteTemplate"=>"AnimAcidPoisonBubble","gAcidPoisonDropletSpriteTemplate"=>"AnimAcidPoisonDroplet","gLeechLifeNeedleSpriteTemplate"=>"AnimLeechLifeNeedle","gLightningSpriteTemplate"=>"AnimLightning","gAbsorptionOrbSpriteTemplate"=>"AnimAbsorptionOrb","gPowerAbsorptionOrbSpriteTemplate"=>"AnimPowerAbsorptionOrb",_=>null
    };
    private bool ValidateElementalOperand(SourceAnimationCommand c)
    {
        if(SourceExplosionAnimationCatalog.Supports(_script.MoveId))return false;
        if(ValidateElectricOperand(c))return true;
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_SMALL_EMBER" or "ANIM_TAG_WATER_ORB" or "ANIM_TAG_BROWN_ORB" or "ANIM_TAG_GLOWY_RED_ORB" or "ANIM_TAG_GLOWY_GREEN_ORB" or "ANIM_TAG_GRAY_SMOKE" or "ANIM_TAG_BLACK_BALL" or "ANIM_TAG_WATER_IMPACT" or "ANIM_TAG_DUCK" or "ANIM_TAG_BLACK_BALL_2" or "ANIM_TAG_SHOCK_3" or "ANIM_TAG_LIGHTNING" or "ANIM_TAG_SMALL_BUBBLES" or "ANIM_TAG_LARGE_FRESH_EGG" or "ANIM_TAG_EXPLOSION" or "ANIM_TAG_ICE_CRYSTALS" or "ANIM_TAG_GOLD_RING" or "ANIM_TAG_PURPLE_RING" or "ANIM_TAG_METAL_SOUND_WAVES" or "ANIM_TAG_BLUE_STAR" or "ANIM_TAG_ORBS" or "ANIM_TAG_TOXIC_BUBBLE" or "ANIM_TAG_POISON_BUBBLE" or "ANIM_TAG_NEEDLE" or "ANIM_TAG_BUBBLE")return true;
        if(c.Op is "playsewithpan" or "loopsewithpan" or "waitplaysewithpan"&&a.FirstOrDefault() is "SE_M_EMBER" or "SE_M_FLAME_WHEEL" or "SE_M_FIRE_PUNCH" or "SE_M_SELF_DESTRUCT" or "SE_M_CRABHAMMER" or "SE_M_BUBBLE" or "SE_M_HYPER_BEAM" or "SE_M_TRI_ATTACK2" or "SE_M_ABSORB" or "SE_M_ABSORB_2" or "SE_M_BUBBLE3" or "SE_M_GIGA_DRAIN" or "SE_M_SCREECH" or "SE_M_SUPERSONIC" or "SE_M_MEGA_KICK" or "SE_M_TOXIC" or "SE_M_BUBBLE2")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:c.Op=="waitplaysewithpan"?3:2)||a[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER"))Fail(c,"Elemental sound shape differs.");
            if(a.Length==3&&Number(a[2]) is <0 or >255)Fail(c,"Elemental delayed sound differs.");
            if(a.Length==4&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Elemental sound loop differs.");return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_StartSinAnimTimer")
        {if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <1 or >32767)Fail(c,"Sine timer operands differ.");return true;}
        if(c.Op!="createsprite"||a.Length==0||ElementalCallback(a[0]) is not string callback)return false;
        int count=callback switch{"TranslateAnimSpriteToTargetMonLocation"=>9,"AnimEmberFlare"=>10,"AnimFireSpiralInward"=>4,"AnimFireSpread" or "AnimWaterGunDroplet"=>8,"AnimThrowProjectile"=>9,"AnimZapCannonSpark"=>10,"AnimSparkElectricityFlashing"=>11,"AnimLightning"=>5,"AnimPowerAbsorptionOrb" or "AnimLeechLifeNeedle" or "AnimBubbleEffect"=>6,"AnimAcidPoisonBubble"=>9,"AnimAcidPoisonDroplet"=>8,"AnimWaterBubbleProjectile"=>10,_=>7};
        if(a.Length!=count||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!=callback||recipe.Width is not (8 or 16 or 32 or 64)||recipe.Height is not (8 or 16 or 32 or 64)||_script.SineTable.Length!=320)Fail(c,"Elemental sprite recipe/operands differ.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Elemental sprite participant/priority differs.");
        foreach(var arg in a.Skip(3))if(Number(arg) is < -32768 or >32767)Fail(c,"Elemental source argument outside s16.");
        if(callback is "TranslateAnimSpriteToTargetMonLocation" or "AnimEmberFlare" or "AnimFireSpread"&&Number(a[7]) is <1 or >32767)Fail(c,"Elemental duration differs.");
        if(callback=="TranslateAnimSpriteToTargetMonLocation"&&Number(a[8]) is not (0 or 1))Fail(c,"Unimplemented projectile coordinate mode.");
        if(callback=="AnimEmberFlare"&&(Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >1))Fail(c,"Diagonal coordinate mode differs.");
        if(callback is "AnimThrowProjectile" or "AnimWaterGunDroplet"&&Number(a[7]) is <1 or >32767)Fail(c,"Projectile duration differs.");
        if(callback=="AnimAbsorptionOrb"&&Number(a[6]) is <1 or >32767)Fail(c,"Absorption duration differs.");
        if(callback=="AnimPowerAbsorptionOrb"&&Number(a[5]) is <1 or >32767)Fail(c,"Power absorption duration differs.");
        if(callback=="AnimAcidPoisonBubble"&&(Number(a[5]) is <1 or >32767||Number(a[6])!=1))Fail(c,"Acid projectile duration/animation mode differs.");
        if(callback=="AnimAcidPoisonDroplet"&&Number(a[7]) is <1 or >32767)Fail(c,"Acid droplet duration differs.");
        if(callback=="AnimLeechLifeNeedle"&&Number(a[5]) is <1 or >32767)Fail(c,"Needle duration differs.");
        if(callback=="AnimBubbleEffect"&&Number(a[5]) is <0 or >1)Fail(c,"Bubble target grouping differs.");
        if(callback=="AnimWaterBubbleProjectile"&&Number(a[9]) is <2 or >255)Fail(c,"Water bubble timer differs.");
        if(callback=="AnimSmallBubblePair"&&(Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >1))Fail(c,"Small bubbles operands differ.");
        if(callback is "AnimZapCannonSpark" or "AnimSparkElectricityFlashing")
        {
            if(Number(a[6]) is <1 or >32767||Number(a[7]) is <0 or >255||Number(a[8]) is <0 or >255||Number(a[9]) is <0 or >2)Fail(c,"Circling spark operands differ.");
            if(callback=="AnimSparkElectricityFlashing"&&(Number(a[10])&32767)==0)Fail(c,"Zero spark flash divisor has no admitted retail semantics.");
        }
        if(callback=="AnimThunderboltOrb"&&(Number(a[3]) is <1 or >32767||Number(a[6]) is <0 or >255))Fail(c,"Electric orb lifetime differs.");
        if(callback=="AnimSpriteOnMonPos"&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1))Fail(c,"Positioned sprite coordinate mode differs.");
        if(callback=="AnimHitSplatBasic"&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >3))Fail(c,"Water hit affine index differs.");
        if(callback=="AnimFireSpiralInward"&&Number(a[3]) is <0 or >255)Fail(c,"Spiral angle differs.");return true;
    }
    private bool TryCreateElementalSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(SourceExplosionAnimationCatalog.Supports(_script.MoveId))return false;
        if(TryCreateElectricSprite(c,slot,priority))return true;
        if(ElementalCallback(c.Args[0]) is null)return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Elemental graphics have not been loaded.");
        if(recipe.Callback=="AnimHitSplatBasic")
        {
            var center=_battlers[Resolve(c.Args[5]).Value].Center(_doubles,true);
            _sprites.Add(new HitSplat(slot,priority,new(center.X+InitialXDirection()*Number(c.Args[3]),center.Y+Number(c.Args[4])),Number(c.Args[6]),recipe.Asset));return true;
        }
        if(recipe.Callback=="AnimWaterBubbleProjectile"){_sprites.Add(new WaterBubbleProjectile(slot,priority,recipe,c.Args.Skip(3).Select(Number).ToArray(),this));return true;}
        _sprites.Add(new ElementalSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private bool TryCreateElementalTask(SourceAnimationCommand c)
    {
        if(TryCreateElectricTask(c))return true;
        if(c.Args[0]!="AnimTask_StartSinAnimTimer")return false;
        _elementalSinTimer=0;_tasks.Add(new ElementalSinTimer(Number(c.Args[2])));return true;
    }
    private sealed class ElementalSinTimer(int duration):VisualTask
    {
        private int _remaining=duration;
        public override void Step(SourceBattleAnimation owner){owner._elementalSinTimer=(owner._elementalSinTimer+3)&255;if(--_remaining==0)Finished=true;}
    }
    private PixelPoint AverageElementalTarget(bool respectPicOffsets)
    {
        var target=_battlers[_cue.Target.Value];var a=respectPicOffsets?target.Center(_doubles,true):target.Base(_doubles);
        if(!_doubles)return a;
        var partner=_battlers[_cue.Target.Value^2];var b=respectPicOffsets?partner.Center(true,true):partner.Base(true);
        return new((a.X+b.X)/2,(a.Y+b.Y)/2);
    }
    private sealed class ElementalSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteAnimFrame[] _animation;private readonly int _loop;
        private readonly PixelPoint _position;private PixelPoint _offset;private PhysicalLinear? _linear;
        private bool _animationEnded,_hidden;private readonly SourceSpriteAffineProgram? _affine;
        private int _waveStep,_tileOffset,_blink,_blinkDelay;
        private int _remaining,_angle,_amplitudeChange,_fx,_fy,_vx,_vy,_phase,_amplitude,_animIndex,_animDelay,_state;
        public ElementalSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot createdAt,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_animation=recipe.Animations[0];_loop=recipe.LoopStarts[0];_animDelay=_animation[0].Duration-1;
            if(recipe.AffineAnimations.Length>0)_affine=new(recipe.AffineAnimations[0]);
            var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];int direction=owner.InitialXDirection();
            // Cmd_createsprite always starts at the target; createdAt controls only OAM subpriority.
            var center=target.Center(owner._doubles,true);_position=center;
            switch(recipe.Callback)
            {
                case "AnimSpriteOnMonPos":
                    var placed=a[2]==0?actor:target;center=a[3]==0?placed.Center(owner._doubles,true):placed.Base(owner._doubles);
                    _position=new(center.X+direction*a[0],center.Y+a[1]);break;
                case "AnimLightning":_position=new(center.X+(actor.Slot.IsPlayer?a[0]:-a[0]),center.Y+a[1]);break;
                case "AnimSmallBubblePair":
                    center=a[3]==0?actor.Center(owner._doubles,true):center;
                    _position=new(center.X+direction*a[0],center.Y+a[1]);_remaining=a[2];break;
                case "AnimWaterGunDroplet":
                    _position=new(center.X+direction*a[0],center.Y+a[1]);_linear=new(a[2],a[4],a[4]);break;
                case "AnimThunderboltOrb":
                    center=target.Center(owner._doubles,true);_position=new(center.X+(target.Slot.IsPlayer?-a[1]:a[1]),center.Y+a[2]);_remaining=a[0];_blink=_blinkDelay=a[3];break;
                case "AnimZapCannonSpark":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var sparkEnd=target.Center(owner._doubles,true);
                    _linear=new(sparkEnd.X-_position.X,sparkEnd.Y-_position.Y,a[3]);_amplitude=a[2];_waveStep=a[5];_angle=a[4];_tileOffset=a[6]*4;MoveSpark(owner,true);break;
                case "AnimSparkElectricityFlashing":
                    var sparkMon=(a[7]&32768)!=0?target:actor;center=sparkMon.Center(owner._doubles,true);
                    _position=new(center.X+(sparkMon.Slot.IsPlayer?-a[0]:a[0]),center.Y+a[1]);_remaining=a[3];_amplitude=a[2];_waveStep=a[5];_angle=a[4];_tileOffset=a[6]*4;_blinkDelay=a[7]&32767;MoveSpark(owner,false);break;
                case "AnimBubbleEffect":
                    center=a[2]==0?center:owner.AverageElementalTarget(true);_position=new(center.X+(a[2]==0?direction:actor.Slot.IsPlayer?1:-1)*a[0],center.Y+a[1]);break;
                case "AnimAcidPoisonDroplet":
                    center=owner.AverageElementalTarget(true);_position=new(center.X+(actor.Slot.IsPlayer?a[0]:-a[0]),center.Y+a[1]);_linear=new(a[2],a[4],a[4]);break;
                case "AnimAcidPoisonBubble":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var acidEnd=owner.AverageElementalTarget(true);
                    _linear=new(acidEnd.X+(actor.Slot.IsPlayer?a[4]:-a[4])-_position.X,acidEnd.Y+a[5]-_position.Y,a[2]);_amplitude=-30;_waveStep=32768/a[2];break;
                case "AnimLeechLifeNeedle":
                    center=target.Center(owner._doubles,true);int needleSign=target.Slot.IsPlayer?-1:1;_position=new(center.X+needleSign*a[0],center.Y+needleSign*a[1]);_linear=new(-needleSign*a[0],-needleSign*a[1],a[2]);break;
                case "AnimAbsorptionOrb":
                    _position=new(center.X+direction*a[0],center.Y+a[1]);var absorbEnd=actor.Center(owner._doubles,true);
                    _linear=new(absorbEnd.X-_position.X,absorbEnd.Y-_position.Y,a[3]);_amplitude=a[2];_waveStep=32768/a[3];break;
                case "AnimPowerAbsorptionOrb":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);_linear=new(-direction*a[0],-a[1],a[2]);break;
                case "AnimThrowProjectile":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var arcEnd=target.Center(owner._doubles,true);
                    _linear=new(arcEnd.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,arcEnd.Y+a[3]-_position.Y,a[4]);_amplitude=a[5];_waveStep=32768/a[4];break;
                case "AnimFireSpiralInward":_angle=a[0];_remaining=30;MoveSpiral(owner);break;
                case "AnimFireSpread":_position=new(center.X+direction*a[0],center.Y+a[1]);_remaining=a[4];_vx=a[2];_vy=a[3];break;
                case "TranslateAnimSpriteToTargetMonLocation":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);
                    var destination=a[5]==0?target.Center(owner._doubles,true):target.Base(owner._doubles);
                    _linear=new(destination.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,destination.Y+a[3]-_position.Y,a[4]);break;
                case "AnimEmberFlare":
                    center=a[6]==0?target.Center(owner._doubles,true):target.Base(owner._doubles);
                    // The source deliberately initializes the target position twice; when
                    // picture offsets are disabled each initialization resets the base.
                    _position=new(center.X+direction*a[0]*(a[6]==0&&a[5]==1?2:1),center.Y+a[1]*(a[6]==0&&a[5]==1?2:1));
                    var mon=a[5]==0?actor:target;var end=a[6]==0?mon.Center(owner._doubles,true):mon.Base(owner._doubles);
                    int dx=a[2];if(actor.Slot.IsPlayer==target.Slot.IsPlayer&&actor.Slot.Value>=2)dx=-dx;
                    _linear=new(end.X+(actor.Slot.IsPlayer?dx:-dx)-_position.X,end.Y+a[3]-_position.Y,a[4]);break;
                case "AnimToTargetInSinWave":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var targetCenter=target.Center(owner._doubles,true);
                    _linear=new(targetCenter.X-_position.X,targetCenter.Y-_position.Y,30);_phase=(owner._elementalSinTimer>127?owner._elementalSinTimer-127:owner._elementalSinTimer)*256;_amplitude=owner._elementalSinTimer>127?-a[3]:a[3];MoveWave(owner);break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var matrix=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);
                return new(Slot,_recipe.Template,_position,_offset,_animation[_animIndex].Tile+_tileOffset,!_hidden,_animation[_animIndex].HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){VFlip=_animation[_animIndex].VFlip,Width=_recipe.Width,Height=_recipe.Height,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=matrix.A,MatrixB=matrix.B,MatrixC=matrix.C,MatrixD=matrix.D};
            }
        }
        private void MoveSpark(SourceBattleAnimation owner,bool translating)
        {
            PixelPoint linear=default;
            if(translating&&_linear!.Step(out linear)){Finished=true;return;}
            _offset=new(linear.X+((owner._script.SineTable[_angle]*_amplitude)>>8),linear.Y+((owner._script.SineTable[_angle+64]*_amplitude)>>8));
            _angle=(_angle+_waveStep)&255;
            if(_angle%(translating?3:_blinkDelay)==0)_hidden=!_hidden;
            if(!translating&&_remaining--<=0)Finished=true;
        }
        private void MoveSpiral(SourceBattleAnimation owner)
        {
            int radius=60+(_amplitudeChange>>8);_offset=new((owner._script.SineTable[_angle]*radius)>>8,(owner._script.SineTable[_angle+64]*radius)>>8);
            _angle=(_angle+9)&255;_amplitudeChange=(short)(_amplitudeChange-512);_remaining--;
        }
        private void MoveWave(SourceBattleAnimation owner)
        {
            if(_linear!.Step(out var offset)){Finished=true;return;}
            _offset=new(offset.X,offset.Y+((owner._script.SineTable[_phase>>8]*_amplitude)>>8));
            if((_phase+1792)>>8>127){_phase=0;_amplitude=-_amplitude;}else _phase+=1792;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==2){Finished=true;return;}
            switch(_recipe.Callback)
            {
                case "AnimLightning":case "AnimSpriteOnMonPos":if(_animationEnded||_affine?.Ended==true)Finished=true;break;
                case "AnimFireSpiralInward":if(_remaining>0)MoveSpiral(owner);else _state=2;break;
                case "AnimFireSpread":if(_remaining-->0){_fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);_offset=new(_fx>>8,_fy>>8);}else _state=2;break;
                case "AnimToTargetInSinWave":MoveWave(owner);break;
                case "AnimBubbleEffect":_angle=(_angle+11)&255;_fy=(short)(_fy+48);_offset=new((owner._script.SineTable[_angle]*4)>>8,-(_fy>>8));if(_affine!.Ended)Finished=true;break;
                case "AnimSmallBubblePair":_angle=(_angle+11)&255;_fy=(short)(_fy+48);_offset=new((owner._script.SineTable[_angle]*4)>>8,-(_fy>>8));if(_remaining--==0)Finished=true;break;
                case "AnimThunderboltOrb":if(--_blink==-1){_hidden=!_hidden;_blink=_blinkDelay;}if(_remaining--<=0)Finished=true;break;
                case "AnimZapCannonSpark":MoveSpark(owner,true);break;
                case "AnimSparkElectricityFlashing":MoveSpark(owner,false);break;
                case "AnimAcidPoisonBubble":case "AnimAbsorptionOrb":case "AnimThrowProjectile":
                    if(_linear!.Step(out var arc)){Finished=true;break;}
                    _angle=(short)(_angle+_waveStep);_offset=new(arc.X,arc.Y+((owner._script.SineTable[(_angle>>8)&255]*_amplitude)>>8));break;
                default:if(_linear!.Step(out var offset))_state=2;else _offset=offset;break;
            }
            if(Finished)return;
            _affine?.Step();
            if(_animDelay>0)_animDelay--;
            else if(_animIndex+1<_animation.Length){_animIndex++;_animDelay=_animation[_animIndex].Duration-1;}
            else if(_loop>=0){_animIndex=_loop;_animDelay=_animation[_animIndex].Duration-1;}
            else _animationEnded=true;
        }
    }
}
