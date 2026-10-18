using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

/// <summary>Complete resolved materials for positive generated-script composition tests.
/// Missing-resource rejection tests deliberately construct the compositor directly.</summary>
internal static class AnimationCompositionFixture
{
    public static SourceAnimationScript Script(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var script=resources.Resolve(move).Script;
        // Resolved recipes are owned by the disposable catalog; primitive fixtures
        // retain immutable recipe values, not its cleared dictionary.
        return script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value)};
    }
    public static SourceBattleFrameComposer Create(SourceAnimationResources resources,
        SourceResolvedAnimation resolved,IReadOnlyDictionary<string,SourcePixelAsset> assets,
        AnimationBattler[] battlers,SourceAnimationBackgroundSurface? surface=null)
        =>new(assets,battlers,resolved.StatMask,resolved.Metallic,
            surface??resources.CreateBackgroundSurface(0),resolved.Backgrounds,overlays:resolved.Overlays);
}
