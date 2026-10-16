using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Read-only ordinary script primitives are admitted by their source instruction
    // and interacted map object. Existing quest-specific handlers keep their profiles.
    private bool TryApplyRegionalDialogueEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.RegionalFlavorEnabled || effect.Opcode != "checkplayergender") return false;
        const string script = "PokemonTower_1F_EventScript_Woman2";
        if (effect.Source.Path != "data/maps/PokemonTower_1F/scripts.inc" || effect.Source.Symbol != script)
            return false;
        if (!_content.SupportsRegionalFlavor(state.World.Player.MapId) ||
            state.World.Player.MapId.Value != "map.pokemon-tower-1f" ||
            effect.Source.Line != 24 || !effect.Args.IsEmpty ||
            state.LastTalked is not {} localId || !state.Actors.TryGetValue(localId, out var actor) ||
            actor.SourceLocalId != 4 || actor.ScriptId != script ||
            actor.GraphicsId != "OBJ_EVENT_GFX_WOMAN_3" || !actor.Visible ||
            actor.TemplateX != 7 || actor.TemplateY != 8 ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            state.PlayerGender is < 0 or > 1)
            throw new InvalidDataException("regional_dialogue_gender_context_invalid");
        next = state with
        {
            Script = state.Script with
            {
                Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender)
            }
        };
        return true;
    }
}
