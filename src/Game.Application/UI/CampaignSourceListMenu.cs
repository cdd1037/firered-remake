using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplySourceListMenuEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode == "special" && effect.Args.Length == 1)
        {
            if (effect.Args[0] == "ListMenu")
            {
                // Other native lists (including the Silph elevator) retain their own contracts.
                var definition = _content.FindListMenu(state.Script.Variables.GetValueOrDefault("VAR_0x8004"));
                if (definition is null) return false;
                if (state.Script.ListMenu is not { Suspended: false } opened || opened.MenuId != definition.Id)
                    throw new InvalidDataException("source_list_menu_open_context");
                return true;
            }
            if (effect.Args[0] == "ReturnToListMenu" && state.Script.ListMenu is { Suspended: false } &&
                StoryListMenuRules.Display(state.Script, _content) is not null) return true;
        }
        return effect.Opcode == "waitstate" && effect.Args.IsEmpty &&
            StoryListMenuRules.Pending(state.Script, _content) is not null;
    }
}
