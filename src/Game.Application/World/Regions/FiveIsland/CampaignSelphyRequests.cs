using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string SelphyHouseMap = "map.five-island-resort-gorgeous-house";
    private const string SelphyEntry = "FiveIsland_ResortGorgeous_House_EventScript_Selphy";
    private static readonly ImmutableArray<string> SelphyRewardSymbols =
        SelphyRequestRules.DeluxeRewards.Add("ITEM_LUXURY_BALL");

    // The original script variables remain the sole persistent request state.
    private SelphyRequestState SelphyRequest(CampaignState state)
    {
        int requested = state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RequestedSpeciesVariable);
        int steps = state.Script.Variables.GetValueOrDefault(SelphyRequestRules.StepCounterVariable);
        int rewardId = state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RewardVariable);
        if (requested is < 0 or > ushort.MaxValue || steps is < 0 or > ushort.MaxValue)
            throw new InvalidDataException("selphy_variable_range");
        string reward = rewardId == 0 ? "ITEM_NONE" : SelphyRewardSymbols.SingleOrDefault(symbol =>
            _content.TryConstant(symbol, out int sourceId) && sourceId == rewardId)
            ?? throw new InvalidDataException("selphy_reward_identity");
        var result = new SelphyRequestState((ushort)requested, (ushort)steps, reward);
        SelphyRequestRules.Validate(result);
        return result;
    }

    private CampaignState WriteSelphyRequest(CampaignState state, SelphyRequestState request)
    {
        if (!_content.TryConstant(request.RewardItemSymbol, out int reward))
            throw new InvalidDataException("selphy_reward_constant");
        return state with { Script = state.Script with { Variables = state.Script.Variables
            .SetItem(SelphyRequestRules.RequestedSpeciesVariable, request.RequestedSpecies)
            .SetItem(SelphyRequestRules.StepCounterVariable, request.StepCounter)
            .SetItem(SelphyRequestRules.RewardVariable, reward) } };
    }

    private CampaignState ApplySelphyWalkingStep(CampaignState state)
    {
        var before = SelphyRequest(state);
        var after = SelphyRequestRules.AdvanceEligibleStep(before);
        return after == before ? state : WriteSelphyRequest(state, after);
    }

    private string? ValidateSelphyRequest(CampaignState state)
    {
        try
        {
            var request = SelphyRequest(state);
            if (request.StepCounter >= SelphyRequestRules.ExpirySteps) return "save_selphy_step_counter";
            if (request.RequestedSpecies is not (0 or SelphyRequestRules.ExpiredSpecies) &&
                (!_content.IsValidPokedexSourceSpecies(request.RequestedSpecies) ||
                 !state.SeenSpecies.Contains(request.RequestedSpecies))) return "save_selphy_requested_species";
            if (request.RequestedSpecies != 0 && state.Script.Flags.GetValueOrDefault(SelphyRequestRules.HiddenInsideFlag))
                return "save_selphy_request_before_rescue";
            return null;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException)
        { return "save_selphy_request_invalid"; }
    }

    private bool InSelphyRequestScript(CampaignState state)
    {
        if (state.World.Player.MapId.Value != SelphyHouseMap ||
            state.Script.Completed || !state.Script.Locked || state.Script.QuestLogState != 0 ||
            state.Script.Flags.GetValueOrDefault(SelphyRequestRules.HiddenInsideFlag) ||
            state.LastTalked is not {} id || !state.Actors.TryGetValue(id, out var actor) ||
            !actor.Active || actor.Invisible || actor.ScriptId != SelphyEntry || actor.GraphicsId != "OBJ_EVENT_GFX_WOMAN_2") return false;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.SingleOrDefault(a => a.LocalId == id);
        return definition is { IsClone: false, X: 4, Y: 4, Elevation: 3, GraphicsId: "OBJ_EVENT_GFX_WOMAN_2" } &&
            definition.SourceLocalId == actor.SourceLocalId && definition.ScriptId == SelphyEntry &&
            definition.HideFlag == SelphyRequestRules.HiddenInsideFlag;
    }

    private bool TryApplySelphyRequestEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (TryApplyLostCaveFieldEffect(state, effect, out next)) return true;
        if (effect.Source.Path != SelphyRequestRules.ScriptPath ||
            effect.Opcode is not ("special" or "specialvar")) return false;
        bool sample = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "SampleResortGorgeousMonAndReward" });
        bool contains = effect.Opcode == "specialvar" && effect.Args.SequenceEqual(new[] { "VAR_RESULT", "DoesPlayerPartyContainSpecies" });
        // QuestLog_CutRecording is already supplied by the authenticated shared item service.
        if (!sample && !contains) return false;
        if (!InSelphyRequestScript(state) || state.Script.Wait?.Kind != StoryWaitKind.NativeEffect || state.Script.Stack.IsEmpty)
            throw new InvalidDataException("selphy_special_context");
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length ||
            program.Instructions[index] is not {} instruction || instruction.Source != effect.Source ||
            instruction.Opcode != effect.Opcode || !instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("selphy_special_source");
        var request = SelphyRequest(state);
        if (sample)
        {
            if (effect.Source.Symbol is not ("FiveIsland_ResortGorgeous_House_EventScript_RequestMon" or
                "FiveIsland_ResortGorgeous_House_EventScript_ShowRequestedMon")) throw new InvalidDataException("selphy_sample_caller");
            var result = SelphyRequestRules.SampleRequest(request, state.SeenSpecies.ToImmutableHashSet(), state.World.Rng);
            next = WriteSelphyRequest(state, result.State);
            next = next with { World = next.World with { Rng = result.Rng }, TextBuffers = next.TextBuffers.SetItem("STR_VAR_1",
                _content.SourceSpeciesName(result.State.RequestedSpecies) ?? throw new InvalidDataException("selphy_requested_name")) };
        }
        else
        {
            if (effect.Source.Symbol != "FiveIsland_ResortGorgeous_House_EventScript_CheckForRequestedMon" ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != request.RequestedSpecies ||
                request.RequestedSpecies is 0 or SelphyRequestRules.ExpiredSpecies) throw new InvalidDataException("selphy_party_caller");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT",
                SelphyRequestRules.PartyContainsRequestedSpecies(state.Party, request.RequestedSpecies) ? 1 : 0) } };
        }
        return true;
    }

    private bool TryApplyLostCaveFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        const string room10 = "data/maps/FiveIsland_LostCave_Room10/scripts.inc";
        if (effect.Source.Path == room10 && effect.Opcode == "call_if_defeated")
        {
            const string destination = "FiveIsland_LostCave_Room10_EventScript_StopSelphySceneTrigger";
            if (state.World.Player.MapId.Value != "map.five-island-lost-cave-room10" ||
                effect.Source.Symbol != "FiveIsland_LostCave_Room10_OnResume" ||
                !effect.Args.SequenceEqual(new[] { "TRAINER_LADY_SELPHY", destination }))
                throw new InvalidDataException("lost_cave_resume_source");
            if (state.BattledTrainers.Contains("pret:TRAINER_LADY_SELPHY"))
                next = state with { Script = state.Script with { Stack = state.Script.Stack.Add(new(destination, 0)) } };
            return true;
        }
        if (effect.Source.Path == room10 && effect.Opcode == "settrainerflag")
            throw new NotSupportedException("lost_cave_quest_log_playback_not_enabled");
        if (effect.Source.Path != SelphyRequestRules.ScriptPath || effect.Opcode != "copyobjectxytoperm") return false;
        const string butler = "LOCALID_RESORT_GORGEOUS_HOUSE_BUTLER";
        if (!InSelphyRequestScript(state) || effect.Source.Symbol != "FiveIsland_ResortGorgeous_House_EventScript_ShowRequestedMon" ||
            !effect.Args.SequenceEqual(new[] { butler }) || !state.Actors.TryGetValue(butler, out var actor) || !actor.Active ||
            actor.GraphicsId != "OBJ_EVENT_GFX_GENTLEMAN") throw new InvalidDataException("selphy_butler_source");
        next = state with { Actors = state.Actors.SetItem(butler, actor with { TemplateX = actor.X, TemplateY = actor.Y }) };
        return true;
    }
}
