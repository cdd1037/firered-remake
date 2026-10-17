namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static string? MultiStrikeCallback(string template)=>template switch
    {"gArmThrustHandSpriteTemplate"=>"AnimArmThrustHit","gFurySwipesSpriteTemplate"=>"AnimFurySwipes",_=>null};
    private bool ValidateMultiStrikeOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op!="createsprite"||MultiStrikeCallback(a.FirstOrDefault()??"") is not {} callback)return false;
        bool arm=callback=="AnimArmThrustHit";
        if(a.Length!=(arm?7:6)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127)Fail(c,"Multi-strike sprite operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!=callback||recipe.Asset!=(arm?"hands-and-feet":"swipe")||recipe.Width!=32||recipe.Height!=32||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=0||recipe.Animations.Length!=(arm?5:2))Fail(c,"Multi-strike sprite recipe differs.");
        if(arm&&(Number(a[5]) is <0 or >32767||Number(a[6]) is <0 or >3)||!arm&&Number(a[5]) is <0 or >1)Fail(c,"Multi-strike frame selection needs reviewed duration and frame index.");
        if(arm&&_validatingInvokedProgram&&_cue.MoveTurn is null)Fail(c,"Arm Thrust requires its committed hit ordinal.");
        return true;
    }
    private bool TryCreateMultiStrikeSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(MultiStrikeCallback(c.Args[0]) is null)return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Multi-strike graphics have not been loaded.");
        _sprites.Add(new ElementalSprite(slot,priority,recipe,Resolve(c.Args[1]),c.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
}
