using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceElementalAnimationCatalog
{
    public static bool Supports(int move)=>move is 7 or 52 or 53;
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
        "gEmberSpriteTemplate"=>"TranslateAnimSpriteToTargetMonLocation",
        "gEmberFlareSpriteTemplate"=>"AnimEmberFlare",
        "gFireSpiralInwardSpriteTemplate"=>"AnimFireSpiralInward",
        "gFireSpreadSpriteTemplate"=>"AnimFireSpread",
        "gFlamethrowerFlameSpriteTemplate"=>"AnimToTargetInSinWave",_=>null
    };
    private bool ValidateElementalOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0]=="ANIM_TAG_SMALL_EMBER")return true;
        if(c.Op is "playsewithpan" or "loopsewithpan"&&a.FirstOrDefault() is "SE_M_EMBER" or "SE_M_FLAME_WHEEL" or "SE_M_FIRE_PUNCH")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:2)||a[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER"))Fail(c,"Elemental sound shape differs.");
            if(a.Length==4&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Elemental sound loop differs.");return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_StartSinAnimTimer")
        {if(a.Length!=3||Number(a[1]) is <0 or >255||Number(a[2]) is <1 or >32767)Fail(c,"Sine timer operands differ.");return true;}
        if(c.Op!="createsprite"||a.Length==0||ElementalCallback(a[0]) is not string callback)return false;
        int count=callback switch{"TranslateAnimSpriteToTargetMonLocation"=>9,"AnimEmberFlare"=>10,"AnimFireSpiralInward"=>4,"AnimFireSpread"=>8,_=>7};
        if(a.Length!=count||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!=callback||recipe.Asset!="small-ember"||recipe.Width!=32||recipe.Height!=32||_script.SineTable.Length!=320)Fail(c,"Elemental sprite recipe/operands differ.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Elemental sprite participant/priority differs.");
        foreach(var arg in a.Skip(3))if(Number(arg) is < -32768 or >32767)Fail(c,"Elemental source argument outside s16.");
        if(callback is "TranslateAnimSpriteToTargetMonLocation" or "AnimEmberFlare" or "AnimFireSpread"&&Number(a[7]) is <1 or >32767)Fail(c,"Elemental duration differs.");
        if(callback=="TranslateAnimSpriteToTargetMonLocation"&&Number(a[8]) is not (0 or 1))Fail(c,"Unimplemented projectile coordinate mode.");
        if(callback=="AnimEmberFlare"&&(Number(a[8]) is <0 or >1||Number(a[9]) is <0 or >1))Fail(c,"Diagonal coordinate mode differs.");
        if(callback=="AnimFireSpiralInward"&&Number(a[3]) is <0 or >255)Fail(c,"Spiral angle differs.");return true;
    }
    private bool TryCreateElementalSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(ElementalCallback(c.Args[0]) is null)return false;
        if(!_loaded.Contains("ANIM_TAG_SMALL_EMBER"))Fail(c,"Elemental graphics have not been loaded.");
        _sprites.Add(new ElementalSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private bool TryCreateElementalTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_StartSinAnimTimer")return false;
        _elementalSinTimer=0;_tasks.Add(new ElementalSinTimer(Number(c.Args[2])));return true;
    }
    private sealed class ElementalSinTimer(int duration):VisualTask
    {
        private int _remaining=duration;
        public override void Step(SourceBattleAnimation owner){owner._elementalSinTimer=(owner._elementalSinTimer+3)&255;if(--_remaining==0)Finished=true;}
    }
    private sealed class ElementalSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteAnimFrame[] _animation;private readonly int _loop;
        private readonly PixelPoint _position;private PixelPoint _offset;private PhysicalLinear? _linear;
        private int _remaining,_angle,_amplitudeChange,_fx,_fy,_vx,_vy,_phase,_amplitude,_animIndex,_animDelay,_state;
        public ElementalSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot createdAt,int[] a,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_animation=recipe.Animations[0];_loop=recipe.LoopStarts[0];_animDelay=_animation[0].Duration-1;
            var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];int direction=owner.InitialXDirection();
            var center=owner._battlers[createdAt.Value].Center(owner._doubles,true);_position=center;
            switch(recipe.Callback)
            {
                case "AnimFireSpiralInward":_angle=a[0];_remaining=30;MoveSpiral(owner);break;
                case "AnimFireSpread":_position=new(center.X+direction*a[0],center.Y+a[1]);_remaining=a[4];_vx=a[2];_vy=a[3];break;
                case "TranslateAnimSpriteToTargetMonLocation":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);
                    var destination=a[5]==0?target.Center(owner._doubles):target.Base(owner._doubles);
                    _linear=new(destination.X+(actor.Slot.IsPlayer?a[2]:-a[2])-_position.X,destination.Y+a[3]-_position.Y,a[4]);break;
                case "AnimEmberFlare":
                    center=a[6]==0?target.Center(owner._doubles,true):target.Base(owner._doubles);
                    // The source deliberately initializes the target position twice; when
                    // picture offsets are disabled each initialization resets the base.
                    _position=new(center.X+direction*a[0]*(a[6]==0&&a[5]==1?2:1),center.Y+a[1]*(a[6]==0&&a[5]==1?2:1));
                    var mon=a[5]==0?actor:target;var end=a[6]==0?mon.Center(owner._doubles):mon.Base(owner._doubles);
                    int dx=a[2];if(actor.Slot.IsPlayer==target.Slot.IsPlayer&&actor.Slot.Value>=2)dx=-dx;
                    _linear=new(end.X+(actor.Slot.IsPlayer?dx:-dx)-_position.X,end.Y+a[3]-_position.Y,a[4]);break;
                case "AnimToTargetInSinWave":
                    center=actor.Center(owner._doubles,true);_position=new(center.X+direction*a[0],center.Y+a[1]);var targetCenter=target.Center(owner._doubles);
                    _linear=new(targetCenter.X-_position.X,targetCenter.Y-_position.Y,30);_phase=(owner._elementalSinTimer>127?owner._elementalSinTimer-127:owner._elementalSinTimer)*256;_amplitude=owner._elementalSinTimer>127?-a[3]:a[3];MoveWave(owner);break;
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_animation[_animIndex].Tile,true,_animation[_animIndex].HFlip,false,256,_recipe.Asset,Subpriority){VFlip=_animation[_animIndex].VFlip};
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
                case "AnimFireSpiralInward":if(_remaining>0)MoveSpiral(owner);else _state=2;break;
                case "AnimFireSpread":if(_remaining-->0){_fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);_offset=new(_fx>>8,_fy>>8);}else _state=2;break;
                case "AnimToTargetInSinWave":MoveWave(owner);break;
                default:if(_linear!.Step(out var offset))_state=2;else _offset=offset;break;
            }
            if(_animDelay>0)_animDelay--;
            else if(_animIndex+1<_animation.Length){_animIndex++;_animDelay=_animation[_animIndex].Duration-1;}
            else if(_loop>=0){_animIndex=_loop;_animDelay=_animation[_animIndex].Duration-1;}
        }
    }
}
