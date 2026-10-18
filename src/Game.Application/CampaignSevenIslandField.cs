using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Seven fixed source switches; zero means unpressed, 1–7 identifies the boulder that pressed it.</summary>
public sealed record CampaignTanobyKeyVisit(bool SolvedOnEntry, ImmutableArray<int> SwitchBoulderIds);
public sealed record CampaignTanobyCompletion(ImmutableArray<int> SwitchBoulderIds);

public sealed partial class CampaignSession
{
    private const string TanobyKeySource = "data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc";
    private const string TanobyKeyPrefix = "SevenIsland_SevaultCanyon_TanobyKey_EventScript_";
    private const string TanobyUnlockFlag = "FLAG_SYS_UNLOCKED_TANOBY_RUINS";
    private static bool SevenIslandChamber(Game.Domain.Ids.MapId map) => map.Value is
        "map.seven-island-tanoby-ruins-monean-chamber" or "map.seven-island-tanoby-ruins-liptoo-chamber" or
        "map.seven-island-tanoby-ruins-weepth-chamber" or "map.seven-island-tanoby-ruins-dilford-chamber" or
        "map.seven-island-tanoby-ruins-scufib-chamber" or "map.seven-island-tanoby-ruins-rixy-chamber" or
        "map.seven-island-tanoby-ruins-viapois-chamber";
    private CampaignState ApplySevenIslandMapEntry(CampaignState state) => state with
    {
        FieldWeather = null,
        TanobyKeyVisit = state.World.Player.MapId.Value == TanobyKeyMap
            ? new(state.Script.Flags.GetValueOrDefault(TanobyUnlockFlag), ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0)) : null
    };
    private string? ValidateSevenIslandField(CampaignState state)
    {
        bool unlocked = state.Script.Flags.GetValueOrDefault(TanobyUnlockFlag);
        if (_content.SevenIslandField is not {} field || field.TanobyKey.RequiredCount != 7 || field.TanobyKey.Switches.Length != 7 ||
            field.TanobyKey.Boulders.Length != 7 || field.TanobyKey.StrengthBehaviorId != 0x20 || field.TanobyKey.SolvedFlag != TanobyUnlockFlag)
            return "save_tanoby_source_missing";
        if (unlocked != (state.TanobyCompletion is not null) || state.TanobyCompletion is {} completion &&
            (completion.SwitchBoulderIds.IsDefault ||
             completion.SwitchBoulderIds.Length != 7 || completion.SwitchBoulderIds.Any(i => i is < 1 or > 7)))
            return "save_tanoby_unlock_receipt";
        string? expectedWeather = SevenIslandChamber(state.World.Player.MapId) && !unlocked ? "WEATHER_SHADE" : null;
        if (!(state.World.Player.MapId.Value == NavelSummitMap) && state.FieldWeather != expectedWeather) return "save_seven_island_weather";
        if (state.World.Player.MapId.Value == "map.seven-island-house-room1" &&
            (state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") != 0 || state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_SEVEN_ISLAND_HOUSE_ROOM1") != 0))
            return "save_seven_island_ereader_not_admitted";
        if (state.World.Player.MapId.Value != TanobyKeyMap)
            return state.TanobyKeyVisit is not null ? "save_tanoby_visit_map" : null;
        if (state.TanobyKeyVisit is not {} visit || visit.SwitchBoulderIds.IsDefault || visit.SwitchBoulderIds.Length != 7 ||
            visit.SwitchBoulderIds.Any(i => i is < 0 or > 7) || visit.SolvedOnEntry && !unlocked)
            return "save_tanoby_visit_invalid";
        int pressed = 0;
        foreach (var button in field.TanobyKey.Switches)
        {
            int value = state.Script.Variables.GetValueOrDefault(button.TemporaryVariable);
            if (value != 0 && value != button.PressedValue || (value == button.PressedValue) != (visit.SwitchBoulderIds[button.Index - 1] != 0))
                return "save_tanoby_switch_progress";
            if (value == button.PressedValue) pressed++;
        }
        if (state.Script.Variables.GetValueOrDefault(field.TanobyKey.CountVariable) != pressed || pressed == 7 && !unlocked ||
            !visit.SolvedOnEntry && unlocked && pressed != 7 || pressed > 0 && !state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag))
            return "save_tanoby_switch_count";
        if (state.MapEdits is { Count: > 0 }) return "save_tanoby_unexpected_metatile_edit";
        return null;
    }
    private bool TryApplySevenIslandFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool key = state.World.Player.MapId.Value == TanobyKeyMap && effect.Source.Path == TanobyKeySource;
        if (key && effect.Opcode == "addvar")
        {
            var visit = state.TanobyKeyVisit ?? throw new InvalidDataException("tanoby_visit_missing");
            int count = state.Script.Variables.GetValueOrDefault("VAR_TEMP_8");
            if (!state.Script.Locked || effect.Source.Symbol != TanobyKeyPrefix + "SwitchPressed" ||
                !effect.Args.SequenceEqual(new[] { "VAR_TEMP_8", "1" }) || count is < 0 or >= 7 ||
                visit.SwitchBoulderIds.Count(i => i != 0) != count + 1)
                throw new InvalidDataException("tanoby_switch_increment_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_TEMP_8", count + 1) } };
            return true;
        }
        if (key && effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "ShakeScreen" }))
        {
            int[] values = [.. new[] { "VAR_0x8004", "VAR_0x8005", "VAR_0x8006", "VAR_0x8007" }.Select(v => state.Script.Variables.GetValueOrDefault(v))];
            bool normal = effect.Source.Symbol == TanobyKeyPrefix + "SwitchPressed" && values.SequenceEqual(new[] { 1, 1, 8, 3 });
            bool final = effect.Source.Symbol == TanobyKeyPrefix + "PuzzleSolvedShakeScreen" &&
                (values.SequenceEqual(new[] { 2, 2, 10, 3 }) || values.SequenceEqual(new[] { 3, 3, 12, 2 })) &&
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_8") == 7;
            if (!state.Script.Locked || !normal && !final) throw new InvalidDataException("tanoby_shake_source_context");
            // Snapshot native arguments before later script instructions reuse these variables.
            next = state with { PresentationEffects = state.PresentationEffects.Add(new(effect.Sequence, "TanobyShakeResolved", state.World.Player.MapId,
                values.Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToImmutableArray())) };
            return true;
        }
        if (key && effect.Opcode == "setobjectxyperm")
        {
            var boulder = _content.SevenIslandField!.TanobyKey.Boulders.SingleOrDefault(b => b.SourceLocalId == effect.Args[0]);
            if (effect.Source.Symbol != TanobyKeyPrefix + "MoveBouldersToSolvedPos" || boulder is null || state.TanobyKeyVisit?.SolvedOnEntry != true ||
                !state.Script.Flags.GetValueOrDefault(TanobyUnlockFlag) || state.TanobyCompletion is null ||
                !effect.Args.SequenceEqual(new[] { boulder.SourceLocalId, boulder.SolvedX.ToString(), boulder.SolvedY.ToString() }))
                throw new InvalidDataException("tanoby_solved_reentry_context");
            var actor = state.Actors.Values.Single(a => a.SourceLocalId == boulder.LocalId);
            if (actor.Active) throw new InvalidDataException("tanoby_solved_reentry_already_spawned");
            next = state with { Actors = state.Actors.SetItem(actor.LocalId, actor with { X = boulder.SolvedX, Y = boulder.SolvedY, TemplateX = boulder.SolvedX, TemplateY = boulder.SolvedY }) };
            return true;
        }
        if (key && effect.Opcode == "releaseall" && effect.Source.Symbol == TanobyKeyPrefix + "PuzzleSolved")
        {
            var visit = state.TanobyKeyVisit ?? throw new InvalidDataException("tanoby_completion_visit_missing");
            if (!state.Script.Flags.GetValueOrDefault(TanobyUnlockFlag) || !state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag) ||
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_8") != 7 || visit.SwitchBoulderIds.Length != 7 ||
                visit.SwitchBoulderIds.Any(i => i is < 1 or > 7) ||
                _content.SevenIslandField!.TanobyKey.Switches.Any(s => state.Script.Variables.GetValueOrDefault(s.TemporaryVariable) != s.PressedValue))
                throw new InvalidDataException("tanoby_completion_context");
            next = state with { TanobyCompletion = state.TanobyCompletion ?? new(visit.SwitchBoulderIds),
                Dialogue = null, SignMessage = null, MessagePointer = null };
            return true;
        }
        if (effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "EventScript_TryDarkenRuins" &&
            effect.Opcode is "setweather" or "doweather")
        {
            if (!SevenIslandChamber(state.World.Player.MapId) || state.Script.Flags.GetValueOrDefault(TanobyUnlockFlag) ||
                effect.Opcode == "setweather" && !effect.Args.SequenceEqual(new[] { "WEATHER_SHADE" }) ||
                effect.Opcode == "doweather" && (!effect.Args.IsEmpty || state.FieldWeather != "WEATHER_SHADE"))
                throw new InvalidDataException("seven_island_weather_source_context");
            next = state with { FieldWeather = "WEATHER_SHADE" }; return true;
        }
        if (effect.Source.Path == "data/maps/SevenIsland_House_Room1/scripts.inc" && effect.Opcode == "special" &&
            effect.Args.SequenceEqual(new[] { "ValidateEReaderTrainer" }))
        {
            if (state.World.Player.MapId.Value != "map.seven-island-house-room1" ||
                effect.Source.Symbol is not ("SevenIsland_House_Room1_OnTransition" or "SevenIsland_House_Room1_EventScript_OldWoman"))
                throw new InvalidDataException("seven_island_ereader_source_context");
            // ValidateEReaderTrainer returns 1 for the all-zero/invalid record. A valid
            // visiting trainer and its alternate layout are outside this admitted route.
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } }; return true;
        }
        return false;
    }
}
