namespace Game.Application;

public sealed partial class CampaignSession
{
    /// <summary>Presentation may offer field Switch only at this authoritative stable boundary.</summary>
    public bool CanReorderFieldParty
    {
        get{var state=State;return state.Party.Length>1&&IsStableFieldForParty(state);}
    }

    // One modal-free boundary shared by party changes and field-move entrypoints.
    // TanobyCompletion is permanent progress, not a callback that owns the field.
    private static bool IsStableFieldForParty(CampaignState state)=>
        IsIdleFieldCallback(state) &&
        state.MailComposer is null &&
        state.TeachyTv is null &&
        state.VsSeekerEffect is null &&
        state.FameChecker is null &&
        state.FlyEffect is null &&
        state.NoncombatFieldEffect is null &&
        state.SeafoamMovement is null &&
        state.RuinValleyCut is null &&
        state.DottedHoleFall is null &&
        state.FourIslandMovement is null &&
        state.MoveTutor is null &&
        state.MoveReminder is null &&
        state.MoveDeleter is null &&
        state.SizeRecord is null &&
        state.Itemfinder is null &&
        state.FieldPickup is null &&
        state.FieldEffectArguments is null &&
        state.Seagallop is null &&
        state.SeagallopMenu is null &&
        state.SilphPad is null &&
        state.RocketSpin is null &&
        state.CinnabarFall is null &&
        state.PokemonLeagueCompletion is null &&
        state.TrainerTowerMenu is null &&
        !state.TrainerTowerRecordsVisible &&
        state.DeoxysRockEffect is null &&
        state.MailInteraction is null &&
        state.FossilMenu is null &&
        state.NpcTrade is null &&
        state.UltimateTutor is null &&
        state.PrizeExchange is null &&
        state.CoinsBox is null &&
        !state.ScreenIsWhite;

    private Decision<CampaignState> DecideFieldParty(CampaignState state,CampaignCommand command)
    {
        if(!IsStableFieldForParty(state))return new(CommandStatus.Rejected,state,"field_party_event_active");
        if(state.Party.IsEmpty)return new(CommandStatus.Rejected,state,"field_party_empty");
        if(command.Choice is not {} source||source<0||source>=state.Party.Length||command.PartyIndex<0||command.PartyIndex>=state.Party.Length)
            return new(CommandStatus.Rejected,state,"party_swap_slot_invalid");
        if(source==command.PartyIndex)return new(CommandStatus.Rejected,state,"party_swap_same_slot");
        // Source party_menu.c SwitchPartyMon exchanges complete records, with no RNG, healing,
        // battle switch, or identity regeneration. The UI owns selection and cancel presentation.
        var party=state.Party.SetItem(source,state.Party[command.PartyIndex]).SetItem(command.PartyIndex,state.Party[source]);
        return new(CommandStatus.Accepted,state with{Party=party});
    }
}
