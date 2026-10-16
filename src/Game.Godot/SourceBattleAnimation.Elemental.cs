using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceElementalAnimationCatalog
{
    public static bool Supports(int move)=>move is 7 or 52 or 53 or 56 or 190 or 324 or 341 or 84 or 86 or 9 or 55 or 85 or 121 or 152 or 192 or 48 or 103 or 319 or 71 or 72 or 202 or 105 or 51 or 61 or 92 or 141 or 145 or 83 or 250 or 328 or 44 or 128 or 116 or 203 or 8 or 58 or 181 or 82 or 126 or 172 or 225 or 204 or 343 or 231 or 342 or 223 or 75 or 129 or 200;
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
            return new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,commands){SoundEffects=r.TryGetProperty("soundEffects",out var sounds)?sounds.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt32()):new Dictionary<string,int>(),SineTable=shared.SineTable,SpriteRecipes=recipes,Subroutines=commands.Where(c=>c.Op=="call").Select(c=>c.Args[0]).Distinct().ToDictionary(name=>name,name=>subs[name])};
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
        "gGoldRingSpriteTemplate"=>"TranslateAnimSpriteToTargetMonLocation","gPsywaveRingSpriteTemplate"=>"AnimToTargetInSinWave","gOutrageFlameSpriteTemplate"=>"AnimOutrageFlame","gRazorLeafParticleSpriteTemplate"=>"AnimRazorLeafParticle","gRazorLeafCutterSpriteTemplate"=>"AnimTranslateLinearSingleSineWave","gSwiftStarSpriteTemplate"=>"AnimTranslateLinearSingleSineWave","gMagentaHeartSpriteTemplate"=>"AnimMagentaHeart","gDragonBreathFireSpriteTemplate"=>"AnimDragonFireToTarget","gDragonRageFireSpitSpriteTemplate"=>"AnimDragonFireToTarget","gDragonRageFirePlumeSpriteTemplate"=>"AnimDragonRageFirePlume","gFireBlastRingSpriteTemplate"=>"AnimFireRing","gFireBlastCrossSpriteTemplate"=>"AnimFireCross","gFireSpiralOutwardSpriteTemplate"=>"AnimFireSpiralOutward","gPowderSnowSnowballSpriteTemplate" or "gBlizzardIceCrystalSpriteTemplate"=>"AnimMoveParticleBeyondTarget","gIceCrystalSpiralInwardLarge"=>"AnimIcePunchSwirlingParticle","gIceCrystalSpiralInwardSmall"=>"AnimIcePunchSwirlingParticle","gIceBeamInnerCrystalSpriteTemplate"=>"AnimIceBeamParticle","gIceBeamOuterCrystalSpriteTemplate"=>"AnimIceBeamParticle","gIceCrystalHitLargeSpriteTemplate"=>"AnimIceEffectParticle","gIceCrystalHitSmallSpriteTemplate"=>"AnimIceEffectParticle","gEndureEnergySpriteTemplate"=>"AnimEndureEnergy","gSharpTeethSpriteTemplate" or "gClampJawSpriteTemplate"=>"AnimBite","gSwirlingDirtSpriteTemplate" or "gWhirlpoolSpriteTemplate" or "gFireSpinSpriteTemplate"=>"AnimParticleInVortex","gWaterBubbleProjectileSpriteTemplate"=>"AnimWaterBubbleProjectile","gPoisonBubbleSpriteTemplate" or "gWaterBubbleSpriteTemplate"=>"AnimBubbleEffect","gAcidPoisonBubbleSpriteTemplate"=>"AnimAcidPoisonBubble","gAcidPoisonDropletSpriteTemplate"=>"AnimAcidPoisonDroplet","gLeechLifeNeedleSpriteTemplate"=>"AnimLeechLifeNeedle","gLightningSpriteTemplate"=>"AnimLightning","gAbsorptionOrbSpriteTemplate"=>"AnimAbsorptionOrb","gPowerAbsorptionOrbSpriteTemplate"=>"AnimPowerAbsorptionOrb",_=>null
    };
    private bool ValidateElementalOperand(SourceAnimationCommand c)
    {
        if(SourceExplosionAnimationCatalog.Supports(_script.MoveId))return false;
        if(ValidateElectricOperand(c))return true;
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_SMALL_EMBER" or "ANIM_TAG_WATER_ORB" or "ANIM_TAG_BROWN_ORB" or "ANIM_TAG_GLOWY_RED_ORB" or "ANIM_TAG_GLOWY_GREEN_ORB" or "ANIM_TAG_GRAY_SMOKE" or "ANIM_TAG_BLACK_BALL" or "ANIM_TAG_WATER_IMPACT" or "ANIM_TAG_DUCK" or "ANIM_TAG_BLACK_BALL_2" or "ANIM_TAG_SHOCK_3" or "ANIM_TAG_LIGHTNING" or "ANIM_TAG_SMALL_BUBBLES" or "ANIM_TAG_LARGE_FRESH_EGG" or "ANIM_TAG_EXPLOSION" or "ANIM_TAG_ICE_CRYSTALS" or "ANIM_TAG_GOLD_RING" or "ANIM_TAG_PURPLE_RING" or "ANIM_TAG_METAL_SOUND_WAVES" or "ANIM_TAG_BLUE_STAR" or "ANIM_TAG_ORBS" or "ANIM_TAG_TOXIC_BUBBLE" or "ANIM_TAG_POISON_BUBBLE" or "ANIM_TAG_NEEDLE" or "ANIM_TAG_BUBBLE" or "ANIM_TAG_MUD_SAND" or "ANIM_TAG_SHARP_TEETH" or "ANIM_TAG_CLAMP" or "ANIM_TAG_FOCUS_ENERGY" or "ANIM_TAG_FIRE_PLUME" or "ANIM_TAG_MAGENTA_HEART" or "ANIM_TAG_ITEM_BAG" or "ANIM_TAG_EXPLOSION_6" or "ANIM_TAG_LEAF" or "ANIM_TAG_RAZOR_LEAF" or "ANIM_TAG_YELLOW_STAR" or "ANIM_TAG_BLUE_RING")return true;
        if(c.Op is "playsewithpan" or "loopsewithpan" or "waitplaysewithpan"&&a.FirstOrDefault() is "SE_M_EMBER" or "SE_M_FLAME_WHEEL" or "SE_M_FIRE_PUNCH" or "SE_M_SELF_DESTRUCT" or "SE_M_CRABHAMMER" or "SE_M_BUBBLE" or "SE_M_HYPER_BEAM" or "SE_M_TRI_ATTACK2" or "SE_M_ABSORB" or "SE_M_ABSORB_2" or "SE_M_BUBBLE3" or "SE_M_GIGA_DRAIN" or "SE_M_SCREECH" or "SE_M_SUPERSONIC" or "SE_M_MEGA_KICK" or "SE_M_TOXIC" or "SE_M_BUBBLE2" or "SE_M_SACRED_FIRE2" or "SE_M_SAND_TOMB" or "SE_M_WHIRLPOOL" or "SE_M_BITE" or "SE_M_VICEGRIP" or "SE_M_DRAGON_RAGE" or "SE_M_STRING_SHOT" or "SE_M_ICY_WIND" or "SE_M_GUST2" or "SE_M_FLAME_WHEEL2" or "SE_M_CHARM" or "SE_M_DIZZY_PUNCH" or "SE_M_HARDEN" or "SE_M_SWIFT" or "SE_M_POISON_POWDER" or "SE_M_RAZOR_WIND" or "SE_M_RAZOR_WIND2" or "SE_M_PSYBEAM" or "SE_M_PSYBEAM2" or "SE_M_TELEPORT")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:c.Op=="waitplaysewithpan"?3:2)||a[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER"))Fail(c,"Elemental sound shape differs.");
            if(a.Length==3&&Number(a[2]) is <0 or >255)Fail(c,"Elemental delayed sound differs.");
            if(a.Length==4&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Elemental sound loop differs.");return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_StartSinAnimTimer")
        {if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <1 or >32767)Fail(c,"Sine timer operands differ.");return true;}
        if(c.Op!="createsprite"||a.Length==0||ElementalCallback(a[0]) is not string callback)return false;
        int count=callback switch{"TranslateAnimSpriteToTargetMonLocation"=>9,"AnimEmberFlare"=>10,"AnimFireSpiralInward" or "AnimIcePunchSwirlingParticle"=>4,"AnimFireSpread" or "AnimWaterGunDroplet" or "AnimIceBeamParticle" or "AnimDragonFireToTarget" or "AnimFireCross"=>8,"AnimThrowProjectile" or "AnimBite" or "AnimOutrageFlame"=>9,"AnimZapCannonSpark"=>10,"AnimSparkElectricityFlashing" or "AnimMoveParticleBeyondTarget"=>11,"AnimLightning" or "AnimMagentaHeart"=>5,"AnimPowerAbsorptionOrb" or "AnimLeechLifeNeedle" or "AnimBubbleEffect" or "AnimIceEffectParticle" or "AnimDragonRageFirePlume" or "AnimFireRing" or "AnimRazorLeafParticle"=>6,"AnimAcidPoisonBubble"=>9,"AnimAcidPoisonDroplet"=>8,"AnimWaterBubbleProjectile" or "AnimParticleInVortex" or "AnimTranslateLinearSingleSineWave"=>10,_=>7};
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
        if(callback=="AnimOutrageFlame"&&(Number(a[5]) is <1 or >32767||Number(a[8]) is <1 or >32767))Fail(c,"Outrage lifetime/flicker divisor differs.");
        if(callback=="AnimTranslateLinearSingleSineWave"&&(Number(a[7]) is <1 or >32767||Number(a[9]) is <0 or >1))Fail(c,"Sine projectile duration/grouping differs.");
        if(callback=="AnimRazorLeafParticle"&&Number(a[5]) is <0 or >32767)Fail(c,"Leaf rise duration differs.");
        if(callback=="AnimDragonFireToTarget"&&(Number(a[7]) is <1 or >32767||_script.SpriteRecipes[a[0]].AffineAnimations.Length!=2||_script.SpriteRecipes[a[0]].Animations.Length!=2))Fail(c,"Dragon fire duration/animation mode differs.");
        if(callback=="AnimDragonRageFirePlume"&&Number(a[3]) is <0 or >1)Fail(c,"Fire plume participant differs.");
        if(callback=="AnimFireRing"&&Number(a[5]) is <0 or >255)Fail(c,"Fire ring phase differs.");
        if(callback=="AnimFireCross"&&Number(a[5]) is <1 or >32767)Fail(c,"Fire cross duration differs.");
        if(callback=="AnimFireSpiralOutward"&&(Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >32767))Fail(c,"Outward fire lifetime/delay differs.");
        if(callback=="AnimMoveParticleBeyondTarget"&&(Number(a[7]) is <1 or >32767||Number(a[10]) is <0 or >1))Fail(c,"Snowball speed/grouping differs.");
        if(callback=="AnimIceBeamParticle"&&Number(a[7]) is <1 or >32767)Fail(c,"Ice beam duration differs.");
        if(callback=="AnimIceEffectParticle"&&Number(a[5]) is <0 or >1)Fail(c,"Ice grouping differs.");
        if(callback=="AnimEndureEnergy"&&(Number(a[3]) is <0 or >1||Number(a[6]) is <0 or >32766))Fail(c,"Endure participant/delay differs.");
        if(callback=="AnimBite"&&(Number(a[5]) is <0 or >7||Number(a[8]) is <1 or >32767||_script.SpriteRecipes[a[0]].AffineAnimations.Length!=8))Fail(c,"Bite affine index/duration differs.");
        if(callback=="AnimParticleInVortex"&&(Number(a[6]) is <1 or >32767||Number(a[9]) is <0 or >1))Fail(c,"Vortex lifetime/participant differs.");
        if(callback=="AnimSmallBubblePair"&&(Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >1))Fail(c,"Small bubbles operands differ.");
        if(callback is "AnimZapCannonSpark" or "AnimSparkElectricityFlashing")
        {
            if(Number(a[6]) is <1 or >32767||Number(a[7]) is <0 or >255||Number(a[8]) is <0 or >255||Number(a[9]) is <0 or >2)Fail(c,"Circling spark operands differ.");
            if(callback=="AnimSparkElectricityFlashing"&&(Number(a[10])&32767)==0)Fail(c,"Zero spark flash divisor has no admitted retail semantics.");
        }
        if(callback=="AnimThunderboltOrb"&&(Number(a[3]) is <1 or >32767||Number(a[6]) is <0 or >255))Fail(c,"Electric orb lifetime differs.");
        if(callback=="AnimSpriteOnMonPos"&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1))Fail(c,"Positioned sprite coordinate mode differs.");
        if(callback=="AnimHitSplatBasic"&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >3))Fail(c,"Water hit affine index differs.");
        if(callback is "AnimFireSpiralInward" or "AnimIcePunchSwirlingParticle"&&Number(a[3]) is <0 or >255)Fail(c,"Spiral angle differs.");return true;
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
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private PixelPoint _position;private PixelPoint _offset;private PhysicalLinear? _linear;
        private bool _animationEnded,_hidden;private readonly SourceSpriteAffineProgram? _affine;
        private int _waveStep,_tileOffset,_blink,_blinkDelay;
        private int _remaining,_angle,_amplitudeChange,_fx,_fy,_vx,_vy,_phase,_amplitude,_state;
        public ElementalSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot createdAt,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);
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
                case "AnimTranslateLinearSingleSineWave":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var sineEnd=a[6]==0?target.Center(owner._doubles,true):owner.AverageElementalTarget(true);
                    _linear=new(sineEnd.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,sineEnd.Y+a[3]-_position.Y,a[4]);_waveStep=32768/a[4];_amplitude=a[5];_remaining=actor.Slot.IsPlayer==target.Slot.IsPlayer?1:0;break;
                case "AnimOutrageFlame":center=actor.Center(owner._doubles,true);_position=new(center.X+(actor.Slot.IsPlayer?a[0]:-a[0]),center.Y+a[1]);_remaining=a[2];_vx=actor.Slot.IsPlayer?a[3]:-a[3];_vy=actor.Slot.IsPlayer?a[4]:-a[4];_phase=a[5];_hidden=true;break;
                case "AnimRazorLeafParticle":_position=actor.Center(owner._doubles,true);_vx=a[0];_vy=a[1];_remaining=a[2];break;
                case "AnimMagentaHeart":
                    center=actor.Base(owner._doubles);_position=new(center.X+direction*a[0],center.Y+a[1]);MoveHeart(owner);break;
                case "AnimDragonFireToTarget":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+(actor.Slot.IsPlayer?a[0]:-a[1]),center.Y+a[1]);
                    _affine=new(recipe.AffineAnimations[actor.Slot.IsPlayer?0:1]);_frames=new(recipe,actor.Slot.IsPlayer?1:0);
                    var dragonEnd=target.Center(owner._doubles,true);_linear=new(dragonEnd.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,dragonEnd.Y+a[3]-_position.Y,a[4]);break;
                case "AnimDragonRageFirePlume":
                    center=(a[0]==0?actor:target).Base(owner._doubles);_position=new(center.X+direction*a[1],center.Y+a[2]);break;
                case "AnimFireSpiralOutward":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);_remaining=a[2];_phase=a[3];_hidden=true;break;
                case "AnimFireRing":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);_angle=a[2];break;
                case "AnimFireCross":_position=new(center.X+a[0],center.Y+a[1]);_remaining=a[2];_vx=a[3];_vy=a[4];break;
                case "AnimEndureEnergy":
                    center=(a[0]==0?actor:target).Base(owner._doubles);_position=new(center.X+a[1],center.Y+a[2]);_remaining=a[3];break;
                case "AnimBite":
                    _position=new(center.X+a[0],center.Y+a[1]);_affine=new(recipe.AffineAnimations[a[2]]);_vx=a[3];_vy=a[4];_remaining=a[5];break;
                case "AnimParticleInVortex":
                    center=(a[6]==0?actor:target).Base(owner._doubles);_position=new(center.X+direction*a[0],center.Y+a[1]);_vy=a[2];_remaining=a[3];_waveStep=a[4];_amplitude=a[5];break;
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
                case "AnimIcePunchSwirlingParticle":case "AnimFireSpiralInward":_angle=a[0];_remaining=30;MoveSpiral(owner);break;
                case "AnimFireSpread":_position=new(center.X+direction*a[0],center.Y+a[1]);_remaining=a[4];_vx=a[2];_vy=a[3];break;
                case "AnimMoveParticleBeyondTarget":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);
                    var snowEnd=a[7]==0?target.Center(owner._doubles,true):owner.AverageElementalTarget(true);
                    int snowDx=snowEnd.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,snowDy=snowEnd.Y+a[3]-_position.Y;
                    _remaining=(Math.Abs(snowDx)<<4)/a[4];if(_remaining==0)throw new InvalidDataException("Zero source fast-linear duration is not admitted.");
                    _vx=(((ushort)(Math.Abs(snowDx)<<4)/_remaining)&~1)|(snowDx<0?1:0);_vy=(((ushort)(Math.Abs(snowDy)<<4)/_remaining)&~1)|(snowDy<0?1:0);
                    int backX=0,backY=0;PixelPoint backward=default;
                    for(int step=0;;step++)
                    {
                        if(step==65536)throw new InvalidDataException("Source reverse snowball translation did not leave the screen.");
                        backX=(ushort)(backX+(_vx^1));backY=(ushort)(backY+(_vy^1));backward=new(((_vx^1)&1)!=0?-(backX>>4):backX>>4,((_vy^1)&1)!=0?-(backY>>4):backY>>4);
                        if(_position.X+backward.X is < -16 or >256||_position.Y+backward.Y is < -16 or >160)break;
                    }
                    _position=new(_position.X+backward.X,_position.Y+backward.Y);_amplitude=a[5];_waveStep=a[6];break;
                case "AnimIceEffectParticle":
                    center=a[2]==0?target.Center(owner._doubles,true):owner.AverageElementalTarget(true);_position=new(center.X+(a[2]==0?direction:actor.Slot.IsPlayer?1:-1)*a[0],center.Y+a[1]);break;
                case "AnimIceBeamParticle":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);
                    var iceEnd=target.Center(owner._doubles,true);_linear=new(iceEnd.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,iceEnd.Y+a[3]-_position.Y,a[4]);break;
                case "TranslateAnimSpriteToTargetMonLocation":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);
                    var destination=a[5]==0?target.Center(owner._doubles,true):target.Base(owner._doubles);
                    _linear=new(destination.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,destination.Y+a[3]-_position.Y,a[4]);break;
                case "AnimEmberFlare":
                    var mon=a[5]==0?actor:target;
                    center=a[6]==0?mon.Center(owner._doubles,true):target.Base(owner._doubles);
                    // AnimTravelDiagonally initializes the chosen mon, then initializes
                    // target again. TRUE retains that position and applies offsets twice;
                    // FALSE resets to the target base before applying one offset.
                    _position=new(center.X+direction*a[0]*(a[6]==0?2:1),center.Y+a[1]*(a[6]==0?2:1));
                    var end=a[6]==0?mon.Center(owner._doubles,true):mon.Base(owner._doubles);
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
                return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile+_tileOffset,!_hidden,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){VFlip=_frames.Frame.VFlip,Width=_recipe.Width,Height=_recipe.Height,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=matrix.A,MatrixB=matrix.B,MatrixC=matrix.C,MatrixD=matrix.D};
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
        private void MoveHeart(SourceBattleAnimation owner)
        {_offset=new((owner._script.SineTable[_angle]*8)>>8,_fy>>8);_angle=(_angle+7)&255;_fy=(short)(_fy-128);if(++_phase==60)Finished=true;}
        private void FireCircle(SourceBattleAnimation owner)
        {_offset=new((owner._script.SineTable[_angle]*28)>>8,(owner._script.SineTable[_angle+64]*28)>>8);_angle=(_angle+20)&255;}
        private void MoveFireRing(SourceBattleAnimation owner)
        {
            if(_state==0)
            {
                FireCircle(owner);if(++_phase==18){var end=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);_linear=new(end.X-_position.X,end.Y-_position.Y,25);_state=1;}
            }
            else if(_state==1)
            {
                if(_linear!.Step(out var linear)){_position=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);_state=3;_phase=1;FireCircle(owner);}
                else{FireCircle(owner);_offset=new(linear.X+_offset.X,linear.Y+_offset.Y);}
            }
            else{FireCircle(owner);if(++_phase==31)Finished=true;}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==2){Finished=true;return;}
            switch(_recipe.Callback)
            {
                case "AnimLightning":case "AnimSpriteOnMonPos":if(_animationEnded||_affine?.Ended==true)Finished=true;break;
                case "AnimIcePunchSwirlingParticle":case "AnimFireSpiralInward":if(_remaining>0)MoveSpiral(owner);else _state=2;break;
                case "AnimOutrageFlame":if(_remaining>0){--_remaining;_offset=new(_fx>>8,_fy>>8);_fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);if(_remaining%_phase==0)_hidden=!_hidden;}else _state=2;break;
                case "AnimFireSpread":if(_remaining-->0){_fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);_offset=new(_fx>>8,_fy>>8);}else _state=2;break;
                case "AnimToTargetInSinWave":MoveWave(owner);break;
                case "AnimTranslateLinearSingleSineWave":
                    _linear!.StepContinuing(out var sineLinear);int oldAngle=_angle;_angle=(short)(_angle+_waveStep);_offset=new(sineLinear.X,sineLinear.Y+((owner._script.SineTable[(_angle>>8)&255]*_amplitude)>>8));
                    if(oldAngle>200&&_angle<56&&_blink==0)_blink=1;
                    if(_blink!=0&&_remaining!=0){_hidden=!_hidden;if(++_blink==30)Finished=true;}
                    if(_position.X+_offset.X is < -16 or >256||_position.Y+_offset.Y is < -16 or >160)Finished=true;break;
                case "AnimRazorLeafParticle":
                    if(_state==0){if(_remaining==0){_angle=(_vy&1)!=0?128:0;_state=1;}else{_remaining--;_position=new((short)(_position.X+_vx),(short)(_position.Y+_vy));}}
                    else{_offset=new(((owner._script.SineTable[_angle]*25)>>8)*(owner._cue.Actor.IsPlayer?1:-1),_offset.Y+((++_phase&1)==0?1:0));_angle=(_angle+2)&255;if(_phase>80)Finished=true;}break;
                case "AnimMagentaHeart":MoveHeart(owner);break;
                case "AnimDragonRageFirePlume":if(_animationEnded)_state=2;break;
                case "AnimFireCross":if(_remaining-->0)_offset=new((short)(_offset.X+_vx),(short)(_offset.Y+_vy));else _state=2;break;
                case "AnimFireRing":MoveFireRing(owner);break;
                case "AnimFireSpiralOutward":
                    if(_state==0){if(_phase--==0)_state=1;break;}
                    _hidden=false;_offset=new((owner._script.SineTable[_angle]*(_amplitude>>8))>>8,(owner._script.SineTable[_angle+64]*(_amplitude>>8))>>8);_angle=(_angle+10)&255;_amplitude=(short)(_amplitude+208);if(--_remaining==-1)Finished=true;break;
                case "AnimMoveParticleBeyondTarget":
                    if(_remaining!=0){_fx=(ushort)(_fx+_vx);_fy=(ushort)(_fy+_vy);_offset=new((_vx&1)!=0?-(_fx>>4):_fx>>4,(_vy&1)!=0?-(_fy>>4):_fy>>4);_remaining--;}
                    if(_remaining==0)_remaining=1;_offset=new(_offset.X,_offset.Y+((owner._script.SineTable[_angle]*_amplitude)>>8));_angle=(_angle+_waveStep)&255;
                    if(_remaining==1&&(_position.X+_offset.X is < -16 or >256||_position.Y+_offset.Y is < -16 or >160))Finished=true;break;
                case "AnimIceEffectParticle":
                    if(_state==0){if(_affine!.Ended)_state=1;}else{_hidden=!_hidden;if(++_phase==20)Finished=true;}break;
                case "AnimEndureEnergy":
                    if(++_phase>_remaining){_phase=0;_position=new(_position.X,(short)(_position.Y-1));}
                    _position=new(_position.X,(short)(_position.Y-_phase));if(_animationEnded)Finished=true;break;
                case "AnimBite":
                    _fx=(short)(_fx+(_state==0?_vx:-_vx));_fy=(short)(_fy+(_state==0?_vy:-_vy));_offset=new(_fx>>8,_fy>>8);
                    if(_state==0){if(++_phase==_remaining)_state=1;}else if(--_phase==0)Finished=true;break;
                case "AnimParticleInVortex":_fy=(short)(_fy+_vy);_offset=new((owner._script.SineTable[_angle]*_amplitude)>>8,-(_fy>>8));_angle=(_angle+_waveStep)&255;if(--_remaining==-1)Finished=true;break;
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
            _frames.Step();_animationEnded=_frames.Ended;
        }
    }
}
