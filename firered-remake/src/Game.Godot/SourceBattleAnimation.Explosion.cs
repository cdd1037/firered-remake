using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceExplosionAnimationCatalog
{

    public static bool Supports(int move)=>move is 120 or 153;
    public static (IReadOnlyDictionary<int,SourceAnimationScript> Scripts,SourcePixelAsset Explosion) Read(string folder)
    {
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,"manifest.json"));
        using var doc=JsonDocument.Parse(bytes);
        SourceAnimationCommand[] Commands(JsonElement row)=>row.EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray();
        var subs=doc.RootElement.GetProperty("subroutines").EnumerateObject().ToDictionary(p=>p.Name,p=>Commands(p.Value));
        var recipes=doc.RootElement.GetProperty("spriteRecipes").EnumerateArray().Select(x=>new SourceSpriteRecipe(x.GetProperty("template").GetString()!,x.GetProperty("asset").GetString()!,x.GetProperty("callback").GetString()!,x.GetProperty("blend").GetBoolean(),x.GetProperty("animations").EnumerateArray().Select(a=>a.EnumerateArray().Select(f=>new SourceSpriteAnimFrame(f.GetProperty("tile").GetInt32(),f.GetProperty("duration").GetInt32(),f.GetProperty("hFlip").GetBoolean(),f.GetProperty("vFlip").GetBoolean())).ToArray()).ToArray())).ToDictionary(x=>x.Template);
        var scripts=doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,Commands(r.GetProperty("commands"))){Subroutines=subs,SpriteRecipes=recipes}).ToDictionary(s=>s.MoveId);
        return (scripts,SourceHitAnimationCatalog.Asset(doc,folder,"explosion"));
    }
}
public sealed partial class SourceBattleAnimation
{
    private void ValidateSubroutines()
    {
        if(SourceExplosionAnimationCatalog.Supports(_script.MoveId))
        {
            if(_script.Subroutines.Count!=2||_script.Subroutines.Any(p=>p.Key is not ("SelfDestructExplode" or "Explosion1")||p.Value.Length!=16||p.Value[^1].Op!="return"||p.Value.Any(c=>c.Op=="call")))
                throw new InvalidDataException("Unadmitted explosion source subroutine.");
        }
        else if(_script.Subroutines.Count>1||_script.Subroutines.Any(p=>p.Key!="GrowthEffect"||p.Value.Length!=4||p.Value[^1].Op!="return"||p.Value.Any(c=>c.Op=="call")))
            throw new InvalidDataException("Unadmitted source subroutine.");
    }
    private bool ValidateExplosionOperand(SourceAnimationCommand c)
    {
        if(!SourceExplosionAnimationCatalog.Supports(_script.MoveId))return false;
        var a=c.Args;
        bool accepted=c.Op switch
        {
            "call"=>a.Length==1&&a[0]==(_script.MoveId==120?"SelfDestructExplode":"Explosion1"),
            "return"=>a.Length==0,
            "loadspritegfx"=>a.SequenceEqual(new[]{"ANIM_TAG_EXPLOSION"}),
            "playsewithpan"=>a.Length==2&&a[0] is "SE_M_SELF_DESTRUCT" or "SE_M_EXPLOSION"&&a[1]=="SOUND_PAN_ATTACKER",
            "createsprite" when a.FirstOrDefault()=="gExplosionSpriteTemplate"=>a.Length==7&&a[1]=="ANIM_ATTACKER"&&a[2]=="3"&&a[5]=="0"&&a[6]=="1"&&
                (a[3],a[4]) is ("0","0") or ("24","-24") or ("-16","16") or ("-24","-12") or ("16","16")&&
                _script.SpriteRecipes.TryGetValue(a[0],out var recipe)&&recipe.Callback=="AnimSpriteOnMonPos"&&recipe.Asset=="explosion",
            "createsprite" when a.FirstOrDefault()=="gComplexPaletteBlendSpriteTemplate"=>a.SequenceEqual(new[]{"gComplexPaletteBlendSpriteTemplate","ANIM_ATTACKER","2","F_PAL_BG","8","9","RGB(26, 8, 8)","8","RGB_BLACK","8"}),
            "createvisualtask" when a.FirstOrDefault()=="AnimTask_ShakeMon2"=>a.Length==7&&a[1]=="5"&&a[2] is "4" or "5" or "6" or "7" or "8"&&a[3]==(_script.MoveId==120?"6":"8")&&a[4]=="0"&&a[5]==(_script.MoveId==120?"38":"40")&&a[6]=="1",
            "createvisualtask" when a.FirstOrDefault()=="AnimTask_BlendBattleAnimPal"=>a.SequenceEqual(new[]{"AnimTask_BlendBattleAnimPal","10","F_PAL_ATTACKER","1","0","9","RGB_RED"})||
                a.SequenceEqual(new[]{"AnimTask_BlendBattleAnimPal","10","F_PAL_ATTACKER","1","9","0","RGB_RED"})||
                a.SequenceEqual(new[]{"AnimTask_BlendBattleAnimPal","10","F_PAL_BG","1","16","16","RGB_WHITE"})||
                a.SequenceEqual(new[]{"AnimTask_BlendBattleAnimPal","10","F_PAL_BG","3","16","0","RGB_WHITE"}),
            _=>false
        };
        if(accepted)return true;
        if(c.Op is "call" or "return" or "loadspritegfx" or "playsewithpan" or "createsprite" or "createvisualtask")Fail(c,"Unadmitted explosion source operand.");
        return false;
    }
    private static int PaletteNumber(string value)=>value switch
    {"F_PAL_BG | F_PAL_BATTLERS"=>31,"F_PAL_BG"=>1,"F_PAL_ATTACKER"=>2,"RGB_RED"=>31,"RGB_WHITE"=>32767,"RGB_WHITEALPHA"=>65535,"RGB_BLACK"=>0,"RGB(26, 8, 8)"=>26|(8<<5)|(8<<10),_=>Number(value)};
    // Original UnpackSelectedBattlePalettes and visible position extensions. The
    // selectors name source palette banks, independent of equal RGB colors.
    private uint SelectPalettes(int selector)
    {
        if(selector is <0 or >2047)throw new InvalidDataException("Palette selector exceeds source field.");
        uint mask=(selector&1)!=0?0xeu:0;
        if((selector&2)!=0)mask|=1u<<(16+_cue.Actor.Value);
        if((selector&4)!=0)mask|=1u<<(16+_cue.Target.Value);
        void Visible(int flag,int slot){if((selector&flag)!=0&&_battlers.TryGetValue(slot,out var b)&&b.Visible)mask|=1u<<(16+slot);}
        Visible(8,_cue.Actor.Value^2);Visible(16,_cue.Target.Value^2);
        if((selector&32)!=0)mask|=1u<<8;if((selector&64)!=0)mask|=1u<<9;
        Visible(128,0);Visible(256,2);Visible(512,1);Visible(1024,3);return mask;
    }
    private AnimationSlot? ResolveShakeParticipant(int participant,bool shake2)
    {
        int slot=participant switch{0=>_cue.Actor.Value,1=>_cue.Target.Value,2=>_cue.Actor.Value^2,3=>_cue.Target.Value^2,
            4 when shake2=>0,5 when shake2=>2,6 when shake2=>1,7 when shake2=>3,8 when shake2=>_cue.Actor.Value,_=>throw new InvalidDataException("Unadmitted shake participant.")};
        return _battlers.TryGetValue(slot,out var b)&&(b.Visible||participant==8)?new(slot):null;
    }
    // StartBlendAnimSpriteColor/AnimTask_BlendSpriteColor_Step2 owns this per-task
    // delay. It does not start a global palette fade or wait on its active flag.
    private sealed class BlendPaletteTask:VisualTask
    {
        private readonly uint _mask;private readonly int _delay,_target;private readonly ushort _color;
        private int _counter,_coefficient;
        public BlendPaletteTask(int[] args,SourceBattleAnimation owner)
        {_mask=owner.SelectPalettes(args[0]);_delay=args[1];_coefficient=args[2];_target=args[3];_color=(ushort)args[4];}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_counter==_delay)
            {
                _counter=0;owner.BlendPalettes(_mask,_coefficient,_color);
                if(_coefficient<_target)_coefficient++;else if(_coefficient>_target)_coefficient--;else Finished=true;
            }
            else _counter++;
        }
    }
}
