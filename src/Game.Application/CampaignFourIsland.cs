using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The Rainbow Pass successor through Lorelei's original Icefall Cave scene.</summary>
public sealed partial class CampaignSession
{
    public const string FourIslandRulesVersion = "firered-four-island-campaign-v1";
    public bool FourIslandEnabled => _content.FourIslandEnabled;
    private const string FourIslandBackMap = "map.four-island-icefall-cave-back";
    private const string FourIslandBackSource = "data/maps/FourIsland_IcefallCave_Back/scripts.inc";
    private const string FourIslandLoreleiScene = "FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene";
    private const string FourIslandSceneVariable = "VAR_MAP_SCENE_ICEFALL_CAVE_BACK";
    private static bool FourIslandMap(MapId map) => map.Value == "map.four-island" || map.Value.StartsWith("map.four-island-", StringComparison.Ordinal);
    private static bool FourIslandSource(string path) => path.StartsWith("data/maps/FourIsland", StringComparison.Ordinal);
    private bool InFourIslandScript(CampaignState state) => _content.FourIslandEnabled && state.Script.Stack.Any(f =>
        _content.FindProgram(f.ProgramId)?.Instructions.Any(i => FourIslandSource(i.Source.Path)) == true);
    private static void ValidateFourIslandCatalog(ICampaignCatalog content)
    {
        if (!content.FourIslandEnabled)
        {
            if (content.FourIslandOwnership is not null || content.FourIslandOpponents is not null || content.FourIslandTrainers is not null)
                throw new InvalidDataException("four_island_profile_missing");
            return;
        }
        if (!content.SeviiRubyEnabled || content.FourIslandOwnership is not {} owned ||
            content.FourIslandOpponents is not {} opponents || content.FourIslandTrainers is not {} trainers ||
            !ReferenceEquals(owned, content.PokemonContent?.Pokemon) || !ReferenceEquals(owned, content.PokemonContent?.Pokemon) ||
            !ReferenceEquals(opponents.Owned, owned) || !ReferenceEquals(opponents, content.PokemonContent?.Pokemon.Opponents) ||
            content.PewterMachines is not {} machines || !content.Inventory.Items.ContainsKey(ItemIds.NeverMeltIceId))
            throw new InvalidDataException("four_island_source_profile_missing");
        FourIslandTrainerPartyFactory.ValidateCatalog(trainers);
    }

    private string? ValidateFourIslandProgress(CampaignState state)
    {
        bool Flag(string name) => state.Script.Flags.GetValueOrDefault(name);
        int town = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_FOUR_ISLAND");
        int scene = state.Script.Variables.GetValueOrDefault(FourIslandSceneVariable);
        bool completed = Flag("FLAG_HIDE_RUIN_VALLEY_SCIENTIST");
        bool won = state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_45");
        bool hm = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], new ItemId("pret:ITEM_HM07")) != 0;
        if (!_content.FourIslandEnabled) return state.IcefallIce is not null || state.FourIslandMovement is not null || state.FourIslandMovementDirection is not null || town != 0 || scene != 0 || completed || won || hm || FourIslandMap(state.World.Player.MapId)
            ? "save_four_island_profile_missing" : null;
        if (town is < 0 or > 1 || scene is < 0 or > 1 || town == 1 && !Flag("FLAG_HIDE_FOUR_ISLAND_RIVAL")) return "save_four_island_scene";
        if ((town != 0 || scene != 0 || hm || FourIslandMap(state.World.Player.MapId)) &&
            (!NationalDexEnabledIn(state) || !RainbowQuestComplete(state) ||
             OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)) return "save_four_island_rainbow_gate";
        if (completed != (scene == 1) || won != completed || Flag("FLAG_HIDE_ICEFALL_CAVE_ROCKETS") != completed ||
            completed && Flag("FLAG_HIDE_LORELEI_IN_HER_HOUSE") && !CelioCompleted(state) || Flag("FLAG_HIDE_ICEFALL_CAVE_LORELEI") && !completed)
            return "save_four_island_lorelei_progress";
        if (hm && !Flag("FLAG_HIDE_FOUR_ISLAND_ICEFALL_CAVE_1F_HM07")) return "save_four_island_hm07_progress";
        if (!_content.FourIslandDayCareEnabled && Flag("FLAG_PENDING_DAYCARE_EGG")) return "save_four_island_daycare_not_admitted";
        return ValidateFourIslandField(state);
    }

    private bool FourIslandButtonWait(CampaignState state) => _content.FourIslandEnabled && state.Script.Locked &&
        state.World.Player.MapId.Value == FourIslandBackMap && state.Script.Wait?.Kind == StoryWaitKind.ButtonPress &&
        state.Script.Stack.LastOrDefault()?.ProgramId == FourIslandLoreleiScene &&
        state.Dialogue?.TextSymbol == "FourIsland_IcefallCave_Back_Text_LoreleiWellDeepFreezeYou";
    private Decision<CampaignState> DecideFourIslandButton(CampaignState state, CampaignCommand command)
    {
        if (!FourIslandButtonWait(state) || command.Text is not ("A" or "B")) return new(CommandStatus.Rejected,state,"four_island_button_required");
        return new(CommandStatus.Accepted,Pump(Advance(state with { PresentationEffects=[], Motions=[], PresentationTicks=0 },
            StoryRules.Continue(state.Script,state.Script.Wait!.CorrelationId,_content))));
    }
    private bool TryApplyFourIslandEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next=state;
        if (!_content.FourIslandEnabled) return false;
        if (TryApplyIcefallEffect(state,effect,out next)) return true;
        if (!FourIslandSource(effect.Source.Path)) return false;
        if (effect.Opcode == "playmoncry" && effect.Source.Path == FourIslandBackSource && effect.Source.Symbol == FourIslandLoreleiScene)
        {
            if (!effect.Args.SequenceEqual(new[]{"SPECIES_LAPRAS","CRY_MODE_ENCOUNTER"}) || !state.Script.Locked ||
                !state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_45") ||
                !_content.TryConstant("CRY_MODE_ENCOUNTER",out int mode) || mode != 2)
                throw new InvalidDataException("four_island_lapras_cry_context");
            return true;
        }
        if (effect.Opcode == "waitbuttonpress" && effect.Source.Path == FourIslandBackSource && effect.Source.Symbol == FourIslandLoreleiScene)
        {
            if (!FourIslandButtonWait(state) || effect.Args.Length != 0) throw new InvalidDataException("four_island_button_source_context");
            return true;
        }
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[]{"QuestLog_CutRecording"}))
        { if(state.Script.QuestLogState!=0)throw new InvalidDataException("quest_log_recording_not_supported");return true; }
        return false;
    }
}
