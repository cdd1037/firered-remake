using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The bounded Four Island successor through the original Dotted Hole theft.</summary>
public sealed partial class CampaignSession
{
    public const string SixIslandRulesVersion = "firered-six-island-campaign-v1";
    public bool SixIslandEnabled => _content.SixIslandEnabled;
    private const string SixIslandRuinMap = "map.six-island-ruin-valley";
    private const string SixIslandSapphireMap = "map.six-island-dotted-hole-sapphire-room";
    private const string SixIslandSapphireSource = "data/maps/SixIsland_DottedHole_SapphireRoom/scripts.inc";
    private const string SixIslandSapphireScene = "SixIsland_DottedHole_SapphireRoom_EventScript_Sapphire";
    private const string SixIslandPasswordFlag = "FLAG_LEARNED_YES_NAH_CHANSEY";
    private static bool SixIslandMap(MapId map) => map.Value == "map.six-island" || map.Value.StartsWith("map.six-island-", StringComparison.Ordinal);
    private static bool DottedHoleMap(MapId map) => map.Value.StartsWith("map.six-island-dotted-hole-", StringComparison.Ordinal);
    private static bool SixIslandSource(string path) => path.StartsWith("data/maps/SixIsland", StringComparison.Ordinal);
    private bool InSixIslandScript(CampaignState state) => _content.SixIslandEnabled && state.Script.Stack.Any(f =>
        _content.FindProgram(f.ProgramId)?.Instructions.Any(i => SixIslandSource(i.Source.Path)) == true);

    private static void ValidateSixIslandCatalog(ICampaignCatalog content)
    {
        if (!content.SixIslandEnabled)
        {
            if (content.SixIslandOwnership is not null || content.SixIslandOpponents is not null || content.SixIslandTrainers is not null)
                throw new InvalidDataException("six_island_profile_missing");
            return;
        }
        if (!content.FourIslandEnabled || content.SixIslandOwnership is not {} owned ||
            content.SixIslandOpponents is not {} opponents || content.SixIslandTrainers is not {} trainers ||
            !ReferenceEquals(owned.Predecessor, content.FourIslandOwnership) || !ReferenceEquals(content.FiveIslandOwnership?.Predecessor.Catalog ?? content.PokemonContent?.Catalog, owned.Catalog) ||
            !ReferenceEquals(opponents.Owned, owned) || !ReferenceEquals(opponents.Predecessor, content.FourIslandOpponents) ||
            (CelioFiveMachines(content)?.Predecessor ?? SixIslandMachineCatalog.For(content.PewterMachines)) is not {} machines || !ReferenceEquals(machines.Pokemon, owned) ||
            (FiveIslandShopCatalog.For(content.EarlyShop)?.Predecessor ?? SixIslandShopCatalog.For(content.EarlyShop)) is null)
            throw new InvalidDataException("six_island_source_profile_missing");
        SixIslandTrainerPartyFactory.ValidateCatalog(trainers);
    }

    private string? ValidateSixIslandProgress(CampaignState state)
    {
        bool Flag(string name) => state.Script.Flags.GetValueOrDefault(name);
        int center = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_SIX_ISLAND_POKEMON_CENTER_1F");
        bool cut = Flag(RuinValleyCutRules.PermanentFlag), password = Flag(SixIslandPasswordFlag);
        if (!_content.SixIslandEnabled)
            return center != 0 || cut || password || SixIslandMap(state.World.Player.MapId) || state.RuinValleyCut is not null || state.DottedHoleFall is not null
                ? "save_six_island_profile_missing" : null;
        if (center is < 0 or > 1 || center == 1 && (!Flag("FLAG_HIDE_SIX_ISLAND_POKECENTER_RIVAL") ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_FOUR_ISLAND") != 1)) return "save_six_island_center_scene";
        if ((center != 0 || cut || password || SixIslandMap(state.World.Player.MapId)) &&
            (!NationalDexEnabledIn(state) || !RainbowQuestComplete(state) ||
             OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)) return "save_six_island_rainbow_gate";
        if (password != Flag("FLAG_HIDE_SAPPHIRE") || password && (!cut || !Flag("FLAG_HIDE_DOTTED_HOLE_SCIENTIST")) ||
            (cut || DottedHoleMap(state.World.Player.MapId)) && !Flag("FLAG_HIDE_RUIN_VALLEY_SCIENTIST"))
            return "save_six_island_theft_progress";
        if (!_content.FiveIslandEnabled && (Flag("FLAG_UNLOCKED_ROCKET_WAREHOUSE") || Flag("FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE") ||
            Flag("FLAG_RECOVERED_SAPPHIRE"))) return "save_five_island_not_admitted";
        return ValidateSixIslandField(state);
    }

    private bool SixIslandBrailleWait(CampaignState state) => InSixIslandScript(state) && state.Script.Locked &&
        state.Script.Wait?.Kind == StoryWaitKind.ButtonPress && state.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) == true;
    private Decision<CampaignState> DecideSixIslandButton(CampaignState state, CampaignCommand command)
    {
        if (!SixIslandBrailleWait(state) || command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "six_island_button_required");
        return new(CommandStatus.Accepted, Pump(Advance(state with { PresentationEffects = [], Motions = [], PresentationTicks = 0 },
            StoryRules.Continue(state.Script, state.Script.Wait!.CorrelationId, _content))));
    }

    private bool TryApplySixIslandEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SixIslandEnabled) return false;
        if (TryApplySixIslandBrailleEffect(state, effect, out next)) return true;
        if (!SixIslandSource(effect.Source.Path)) return false;
        if (effect.Opcode == "setmetatile" && effect.Source.Path == "data/maps/SixIsland_RuinValley/scripts.inc" &&
            effect.Source.Symbol == "SixIsland_RuinValley_EventScript_OpenDottedHoleDoor")
        {
            if (state.World.Player.MapId.Value != SixIslandRuinMap || !state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag) ||
                !effect.Args.SequenceEqual(new[] { "24", "24", "METATILE_SeviiIslands67_DottedHoleDoor_Open", "0" }))
                throw new InvalidDataException("six_island_door_source_context");
            next = state with { MapEdits = SixIslandDoorEdits(true) }; return true;
        }
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "ShakeScreen" }) &&
            effect.Source.Path == SixIslandSapphireSource && effect.Source.Symbol == SixIslandSapphireScene)
        {
            if (!state.Script.Locked || state.World.Player.MapId.Value != SixIslandSapphireMap ||
                !new[] { "VAR_0x8004", "VAR_0x8005", "VAR_0x8006", "VAR_0x8007" }.Select(v => state.Script.Variables.GetValueOrDefault(v)).SequenceEqual(new[] { 3, 0, 12, 3 }))
                throw new InvalidDataException("six_island_shake_source_context");
            return true; // The retained effect and exact following delay own presentation.
        }
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        { if (state.Script.QuestLogState != 0) throw new InvalidDataException("quest_log_recording_not_supported"); return true; }
        return false;
    }

    private bool TryApplySixIslandBrailleEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!InSixIslandScript(state)) return false;
        bool own = effect.Source.Path is "data/maps/SixIsland_RuinValley/scripts.inc" or
            "data/maps/SixIsland_DottedHole_1F/scripts.inc" or SixIslandSapphireSource;
        bool cursor = effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "EventScript_BrailleCursorWaitButton";
        if (effect.Opcode == "waitbuttonpress" && (own || cursor))
        {
            if (effect.Args.Length != 0 || !SixIslandBrailleWait(state)) throw new InvalidDataException("six_island_braille_wait");
            return true;
        }
        if (own && effect.Opcode is "braillemessage" or "getbraillestringwidth")
        {
            if (!state.Script.Locked || effect.Args.Length != 1 || !effect.Args[0].StartsWith("Braille_Text_", StringComparison.Ordinal))
                throw new InvalidDataException("six_island_braille_source_context");
            var pages = _content.TextPages(effect.Args[0]);
            if (pages.Length != 1 || pages[0].Any(c => !(c is >= 'A' and <= 'Z' or ' ' or ',' or '.')))
                throw new InvalidDataException("six_island_braille_characters");
            next = effect.Opcode == "braillemessage" ? state with { Dialogue = new(effect.Args[0], pages, 0, false, false) } :
                state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", checked(pages[0].Length * 16)) } };
            return true;
        }
        if (cursor && effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "BrailleCursorToggle" }))
        {
            if (state.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) != true ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8006") is not (0 or 1) || state.Script.Variables.GetValueOrDefault("VAR_0x8005") != 130)
                throw new InvalidDataException("six_island_braille_cursor_context");
            return true;
        }
        return false;
    }
}
