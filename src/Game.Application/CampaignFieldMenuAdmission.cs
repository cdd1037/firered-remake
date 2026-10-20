namespace Game.Application;

public sealed partial class CampaignSession
{
    /// <summary>Application admission only. The host still owns fades, tracks and input focus.</summary>
    public bool CanOpenFieldMenu => CanOpenFieldMenuIn(State);

    private bool CanOpenFieldMenuIn(CampaignState state)
    {
        // Safari deliberately permits its start menu while refusing player saves. Keep
        // the native menu's existing callback contract, not the stricter party-action one.
        if (state.SafariVisit is { Active: true, Balls: > 0, Steps: > 0 } &&
            state.Script.Flags.GetValueOrDefault(SafariFlag))
            return IsIdleFieldCallback(state);

        if (state.SafariVisit is not null || state.Script.Flags.GetValueOrDefault(SafariFlag) ||
            InSafariArea(state) || !IsStableFieldForParty(state)) return false;

        // These callback payloads can outlive their visible text. Menu admission asks
        // only who owns the field, never revalidates inventory, creature history or a
        // serialized save. Save/load retain their independent full integrity contract.
        return state.Script.ListMenu is null && state.DaisyGrooming is null &&
            state.Diploma is null && state.SlotMachine is null && state.ProfileEditor is null &&
            state.DayCareInteraction is null && state.EggHatch is null && state.FieldItemService is null &&
            state.LostelleHypnoPreparation is null && state.RockSmashPreparation is null &&
            state.TowerWildPreparation is null && state.SnorlaxPreparation is null && state.SnorlaxReturn is null &&
            state.ArticunoPreparation is null && state.ArticunoReturn is null &&
            state.PowerPlantPreparation is null && state.PowerPlantReturn is null &&
            state.MewtwoPreparation is null && state.MewtwoReturn is null &&
            state.SourceScriptedPreparation is null && state.SourceScriptedReturn is null &&
            state.EventIslandPreparation is null && state.EventIslandReturn is null &&
            state.OakRatingHelpContextBackup is null && !state.Script.Flags.GetValueOrDefault(OakPcFlag) &&
            state.MessagePointer is null && !state.BerryPowderVendor &&
            !state.Actors.ContainsKey(BillSourcePresentation.CameraActor) &&
            state.ObjectSlots?.Values.Contains(BillSourcePresentation.CameraActor) != true &&
            state.Exploration?.EarlyEncounter?.Pending is null && state.IcefallIce?.CanSave != false &&
            state.RocketElevator is not ({ FloorWindowVisible: true } or { MenuDefault: not null }) &&
            state.SilphElevator is not ({ FloorWindowVisible: true } or { MenuDefault: not null }) &&
            state.DepartmentElevator is not ({ FloorWindowVisible: true } or { MenuDefault: not null }) &&
            state.TrainerTowerElevator is not ({ FloorWindowVisible: true } or { MenuDefault: not null });
    }

    // Shared callback boundary for the native Safari start menu and stricter ordinary
    // party/field actions. Neither save admissibility nor preflight ownership is implied.
    private static bool IsIdleFieldCallback(CampaignState state) =>
        !state.SafariExitPending &&
        state.Script.Completed &&
        !state.Script.Locked &&
        state.Script.Wait is null &&
        state.Script.Stack.IsEmpty &&
        state.Script.QuestLogState == 0 &&
        state.EntryPrograms.IsEmpty &&
        state.Dialogue is null &&
        state.SignMessage is null &&
        state.NameRater is null &&
        state.Nickname is null &&
        state.MonPicture is null &&
        !state.ScreenIsBlack &&
        state.PlayerVisible &&
        state.PresentationTicks == 0 &&
        state.Motions.IsEmpty &&
        state.ArrivalFacing is null &&
        state.Battle is null &&
        state.SafariBattle is null &&
        state.WildBattle is null &&
        state.ForestTrainerBattle is null &&
        state.DoubleBattle is null &&
        state.OldManTutorial is null &&
        state.FieldPoison is null &&
        state.Evolution is null &&
        state.RareCandy is null &&
        state.EscapeRope is null &&
        state.HealingEffect is null &&
        state.Shop is null &&
        state.ItemMenu is null &&
        state.Pc is null &&
        !state.PcPoweredOn &&
        state.Fishing is null &&
        state.MoneyBox is null &&
        state.GiftDelivery is null &&
        state.TownMap is null &&
        state.Repel is null &&
        state.PokeFlute is null &&
        state.CutEffect is null &&
        state.RockSmashEffect is null &&
        state.FlashEffect is null &&
        state.StrengthEffect is null &&
        state.SurfEffect is null &&
        state.HelpContext == "map";
}
