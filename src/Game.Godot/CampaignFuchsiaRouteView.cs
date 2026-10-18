using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Source presentation for the admitted southern route. No authoritative movement or battle mutation.</summary>
public partial class CampaignView
{

    private void LoadFuchsiaRoutePresentation()
    {
        var art = _catalog.FuchsiaRouteArt ?? throw new InvalidDataException("Fuchsia route art catalog missing.");
        RegisterPokemonArt(art.Sprites, art.Icons, art.Footprints, art.Files);

    }

    private ForestMessage? FuchsiaRouteWildMessage(WildBattleEvent current) => current switch
    {
        WildBattleTransformedEvent transformed => new("sText_PkmnTransformedInto",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(transformed.Actor)),
            ("B_BUFF1", PresentedBattleSpeciesName(PresentedSpeciesId(transformed.AppearanceSpecies)))),
        WildBattleMoveFailedEvent { Reason: "target-transformed" or "semi-invulnerable" } => new("sText_ButItFailed"),
        _ => null
    };

    private (int Species, bool Shiny) FuchsiaBattleAppearance(CampaignWildBattle battle, WildBattleSide side, PokemonInstance identity)
    {
        // The display projection advances at event boundaries; the final turn state can be ahead of the screen.
        // A withdrawing player still uses the previous combatant's source appearance.
        if (side == WildBattleSide.Player && WildWithdrawing(battle) && WildCurrentEvent(battle) is WildBattlePlayerSwitchedEvent switched)
        {
            var old = switched.Previous.Transform?.CopiedPokemon ?? switched.Previous.Pokemon;
            return PresentedAppearance(old);
        }
        var appearance = side == WildBattleSide.Player ? battle.DisplayPlayerAppearance : battle.DisplayOpponentAppearance;
        if (appearance is null) return PresentedAppearance(identity);
        var species = PresentedSpeciesId(appearance.Species);
        return SevenIslandRenderKeys.Appearance(species, appearance.Personality, appearance.OriginalTrainerId);
    }

    private bool IsLedgeStep(CampaignState? before, CampaignState after) => before is not null&&
        CampaignFieldMovementTiming.Resolve(before,new("move",after.World.Player.Facing),_catalog,after).SourceAction.StartsWith("jump_2_",StringComparison.Ordinal);
}
