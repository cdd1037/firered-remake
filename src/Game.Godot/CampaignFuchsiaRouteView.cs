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

    private bool FuchsiaTransformCuePending(CampaignWildBattle battle) => WildCurrentEvent(battle) is WildBattleTransformedEvent && _wildElapsed < 49.0 / 60;

    private void ProcessFuchsiaTransformAudio()
    {
        if (PresentedWildBattle is not { Phase: CampaignBattlePhase.Events } battle || WildCurrentEvent(battle) is not WildBattleTransformedEvent transformed) return;
        string key = $"{battle.Id}:transform:{transformed.Sequence}";
        if (_audioTrainerOneShots.Add(key + ":start")) SourceSound("SE_M_TELEPORT");
        if (_wildElapsed >= 48.0 / 60 && _audioTrainerOneShots.Add(key + ":48")) SourceSound("SE_M_MINIMIZE");
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

    private bool IsFuchsiaLedgeStep(CampaignState? before, CampaignState after) => before is not null&&
        CampaignFieldMovementTiming.Resolve(before,new("move",after.World.Player.Facing),_catalog,after).SourceAction.StartsWith("jump_2_",StringComparison.Ordinal);

    // pret event_object_movement.c: JUMP_DISTANCE_FAR uses 32 one-pixel frames and sJumpY_High[timer >> 1].
    // Keep y2 separate from the world/camera anchor, just as the original sprite does.
    private static int FuchsiaLedgeDrawYOffset(double elapsed)
    {
        ReadOnlySpan<int> high = [-4, -6, -8, -10, -11, -12, -12, -12, -11, -10, -9, -8, -6, -4, 0, 0];
        int frame = (int)Math.Floor(elapsed * 60);
        return frame >= 32 ? 0 : high[Math.Clamp(frame >> 1, 0, 15)];
    }
}
