namespace Game.Application;

public sealed partial class CampaignSession
{
    /// <summary>Presentation may offer field Switch only at this authoritative stable boundary.</summary>
    public bool CanReorderFieldParty
    {
        get{var state=State;return _content.EarlyStarterEnabled&&state.Party.Length>1&&IsStableFieldForParty(state);}
    }

    private static bool IsStableFieldForParty(CampaignState state)=>
        state.Script.Completed&&!state.Script.Locked&&state.Script.Wait is null&&state.Script.Stack.IsEmpty&&state.Script.QuestLogState==0&&
        state.SeafoamMovement is null&&state.RuinValleyCut is null&&state.DottedHoleFall is null&&state.FourIslandMovement is null&&state.EntryPrograms.IsEmpty&&state.Dialogue is null&&state.SignMessage is null&&
        state.MoveTutor is null&&state.NameRater is null&&state.Nickname is null&&state.MonPicture is null&&!state.ScreenIsBlack&&state.PlayerVisible&&
        state.PresentationTicks==0&&state.Motions.IsEmpty&&state.ArrivalFacing is null&&
        state.Battle is null&&state.WildBattle is null&&state.ForestTrainerBattle is null&&state.DoubleBattle is null&&state.SafariBattle is null&&
        state.OldManTutorial is null&&state.FieldPoison is null&&state.Evolution is null&&state.RareCandy is null&&state.EscapeRope is null&&
        state.HealingEffect is null&&state.Shop is null&&state.ItemMenu is null&&!state.PcPoweredOn&&state.HelpContext=="map";

    private Decision<CampaignState> DecideFieldParty(CampaignState state,CampaignCommand command)
    {
        if(!_content.EarlyStarterEnabled)return new(CommandStatus.Rejected,state,"field_party_not_enabled");
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
