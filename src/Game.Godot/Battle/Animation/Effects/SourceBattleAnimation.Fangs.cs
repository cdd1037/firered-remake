namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private static string? FangSpriteCallback(string template)=>template switch
    {"gFangSpriteTemplate"=>"AnimFang","gSuperFangSpriteTemplate"=>"AnimSuperFang",_=>null};
    private bool ValidateFangOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op!="createsprite"||FangSpriteCallback(a.FirstOrDefault()??"") is not {} callback)return false;
        if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Fang sprite operands differ.");
        bool affine=callback=="AnimFang";
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback!=callback||recipe.Asset!="fang-attack"||recipe.Width!=32||recipe.Height!=32||recipe.Blend||recipe.AffineDoubleSize!=affine||recipe.Animations.Length!=1||recipe.Animations[0].Length!=4||recipe.AffineAnimations.Length!=(affine?1:0))Fail(c,"Fang sprite recipe differs.");
        return true;
    }
    private bool TryCreateFangSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(FangSpriteCallback(c.Args[0]) is null)return false;
        if(!_loaded.Contains("ANIM_TAG_FANG_ATTACK"))Fail(c,"Fang graphics have not been loaded.");
        // Both source callbacks retain Cmd_createsprite's target-center position.
        // Their only behavior is shared frame/affine playback and END handoff.
        _sprites.Add(new ElementalSprite(slot,priority,_script.SpriteRecipes[c.Args[0]],Resolve(c.Args[1]),[],this));return true;
    }
}
