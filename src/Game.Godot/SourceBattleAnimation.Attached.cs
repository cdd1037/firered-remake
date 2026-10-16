namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static string? AttachedSpriteCallback(string template)=>template switch
    {"gEyeSparkleSpriteTemplate"=>"AnimEyeSparkle","gAngerMarkSpriteTemplate"=>"AnimAngerMark",_=>null};
    private bool ValidateAttachedSpriteOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_EYE_SPARKLE" or "ANIM_TAG_ANGER")return true;
        if(c.Op is "playsewithpan" or "loopsewithpan"&&a.FirstOrDefault() is "SE_M_DETECT" or "SE_M_SWAGGER2")
        {
            if(a.Length!=(c.Op=="playsewithpan"?2:4)||SoundPan(a[1]) is < -64 or >63)Fail(c,"Attached sprite sound operands differ.");
            if(c.Op=="loopsewithpan"&&(Number(a[2]) is <0 or >255||Number(a[3]) is <0 or >255))Fail(c,"Attached sprite sound loop differs.");
            return true;
        }
        if(c.Op!="createsprite"||AttachedSpriteCallback(a.FirstOrDefault()??"") is not {} callback)return false;
        bool anger=callback=="AnimAngerMark";
        if(a.Length!=(anger?6:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Attached sprite operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!=callback||recipe.Asset!=(anger?"anger":"eye-sparkle")||recipe.Width!=16||recipe.Height!=16||recipe.Blend||recipe.AffineDoubleSize||recipe.Animations.Length!=1||recipe.AffineAnimations.Length!=(anger?1:0))Fail(c,"Attached sprite recipe differs.");
        if(anger&&Number(a[3]) is <0 or >1)Fail(c,"Anger mark battler differs.");
        int coordinates=anger?4:3;
        if(Number(a[coordinates]) is < -64 or >64||Number(a[coordinates+1]) is < -64 or >64)Fail(c,"Attached sprite coordinates differ.");
        return true;
    }
    private bool TryCreateAttachedSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(AttachedSpriteCallback(c.Args[0]) is null)return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Attached sprite graphics have not been loaded.");
        _sprites.Add(new ElementalSprite(slot,priority,recipe,Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
}
