using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The original five draws, retained independently of later movement/battle state.</summary>
public sealed record CampaignCelioRoamerInitialization(int StarterVariable, RngState RngBefore,
    RngState RngAfter, RoamerState InitialState, ImmutableArray<RandomTrace> Trace);

/// <summary>The source handoff's ordered effects. Incomplete receipts cannot be player saves.</summary>
public sealed record CampaignCelioHandoff(int SapphireQuantityBefore, bool SapphireRemovalSucceeded,
    long RemovalSequence, bool SapphirePlaced = false, bool SapphireRedrawn = false,
    bool MachineRedrawn = false, long PostgameFlagsSequence = 0, long InitializationSequence = 0,
    CampaignCelioRoamerInitialization? Initialization = null);

public sealed partial class CampaignSession
{
    private const string CelioHandoffScript = "OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire";
    private const string CelioSapphireScript = "OneIsland_PokemonCenter_1F_EventScript_SetSapphireMetatile";
    private const string CelioMachineScript = "OneIsland_PokemonCenter_1F_EventScript_SetNetworkMachineOn";
    private const string CelioLinkFlag = "FLAG_SYS_CAN_LINK_WITH_RS";
    private const string CelioCaveGuardFlag = "FLAG_HIDE_CERULEAN_CAVE_GUARD";
    private const string CelioLoreleiFlag = "FLAG_HIDE_LORELEI_IN_HER_HOUSE";


    private static void ValidateCelioCatalog(ICampaignCatalog content)
    {
        if (content.CelioRoamerMaps is not {} maps || maps.Count != content.Maps.Count ||
            !maps.Keys.ToHashSet().SetEquals(content.Maps.Keys) || maps.Values.Distinct().Count() != maps.Count ||
            maps.Values.Any(m => m.Group is < 0 or > 255 || m.Number is < 0 or > 255))
            throw new InvalidDataException("celio_source_profile_missing");
        foreach (string special in new[] { "SetPostgameFlags", "InitRoamer" })
        {
            var instruction = content.FindProgram(CelioHandoffScript)?.Instructions.SingleOrDefault(i =>
                i.Opcode == "special" && i.Args.SequenceEqual(new[] { special }));
            if (instruction is not { Op: StoryOp.Special } || instruction.Source.Path != RubyCenterSource ||
                instruction.Source.Symbol != CelioHandoffScript)
                throw new InvalidDataException("celio_handoff_source_missing");
        }
    }

    private bool CelioCompleted(CampaignState state) => state.CelioHandoff?.Initialization is not null && state.Roamer?.Individual is not null &&
        state.Script.Flags.GetValueOrDefault(CelioLinkFlag) && state.Script.Variables.GetValueOrDefault(RubyCenterScene) is 6 or 7;

    private bool RainbowQuestComplete(CampaignState state) =>
        state.Script.Variables.GetValueOrDefault(RubyCenterScene) == 5 || CelioCompleted(state);

    private string? ValidateCelioProgress(CampaignState state)
    {
        bool Flag(string key) => state.Script.Flags.GetValueOrDefault(key);
        int scene = state.Script.Variables.GetValueOrDefault(RubyCenterScene);
        bool linked = Flag(CelioLinkFlag);
        try
        {
            if (state.Roamer is {} roamer)
            {
                SourceRoamerRules.Validate(roamer);
                if (roamer.Runtime.PlayerHistory.Any(m => m != default && !_content.CelioRoamerMaps!.Values.Contains(m)))
                    return "save_celio_roamer_player_history";
            }
            if (state.CelioHandoff is not {} receipt)
                return linked || scene > 5 || Flag(CelioCaveGuardFlag) || state.SpecialSaveWarpFlags != 0 ||
                    state.Roamer?.Individual is not null ? "save_celio_handoff_missing" : null;
            if (scene is not (6 or 7) || !linked || !Flag(CelioCaveGuardFlag) || !Flag(CelioLoreleiFlag) ||
                !Flag(FiveIslandSapphireFlag) || !Flag(FiveIslandRocketsFlag) || !Flag(SixIslandPasswordFlag) ||
                !state.BattledTrainers.Contains("pret:TRAINER_SCIENTIST_GIDEON") || !NationalDexEnabledIn(state) ||
                OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], SapphireItem) != 0 ||
                state.SpecialSaveWarpFlags != 0x80 || state.GcnLinkFlags != 0x803f)
                return "save_celio_handoff_progress";
            if (receipt.SapphireQuantityBefore is < 0 or > 1 || receipt.SapphireRemovalSucceeded != (receipt.SapphireQuantityBefore == 1) ||
                !receipt.SapphirePlaced || !receipt.SapphireRedrawn || !receipt.MachineRedrawn || receipt.RemovalSequence <= 0 ||
                receipt.PostgameFlagsSequence <= receipt.RemovalSequence || receipt.InitializationSequence <= receipt.PostgameFlagsSequence ||
                receipt.InitializationSequence > state.Script.EffectSequence || receipt.Initialization is not {} initial ||
                initial.StarterVariable != state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") || initial.Trace.IsDefault)
                return "save_celio_handoff_receipt";
            var expected = SourceRoamerRules.Initialize(RoamerState.Empty, initial.StarterVariable, initial.RngBefore);
            if (initial.RngAfter != expected.Rng || !initial.Trace.SequenceEqual(expected.Trace) ||
                CanonicalJson.Hash(initial.InitialState) != CanonicalJson.Hash(expected.State) ||
                state.World.Rng.AlgorithmId != initial.RngAfter.AlgorithmId || state.World.Rng.Cursor < initial.RngAfter.Cursor ||
                state.World.Rng.Cursor == initial.RngAfter.Cursor && state.World.Rng != initial.RngAfter)
                return "save_celio_roamer_initialization";
            var actual = state.Roamer?.Individual;
            var original = expected.State.Individual!;
            if (actual is null || actual with { CurrentHp = original.CurrentHp, Status = original.Status, Active = original.Active } != original)
                return "save_celio_roamer_identity";
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { return "save_celio_roamer_invalid"; }
    }
}
