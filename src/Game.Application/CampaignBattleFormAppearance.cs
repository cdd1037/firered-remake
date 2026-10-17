using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Presentation inputs for source form art; original identity owns Transform palette shininess.</summary>
public sealed record CampaignBattleFormAppearance(PokemonInstance RenderPokemon,SourceCastformForm Form,bool Shiny,bool Transformed)
{
    public static CampaignBattleFormAppearance From(WildBattleCombatant body)
    {
        var render=body.Transform?.CopiedPokemon??body.Pokemon;
        var palette=body.Transform?.OriginalPokemon??body.Pokemon;
        return new(render,body.CalledOverlay?.CastformForm??SourceCastformForm.Normal,palette.IsShiny,body.Transform is not null);
    }
}
