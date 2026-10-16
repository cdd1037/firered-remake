using System.Globalization;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public const string NationalDexRulesVersion = "firered-national-dex-postgame-v1";
    private const string PalletSource = "data/maps/PalletTown/scripts.inc";
    private const string PalletDexScene = "PalletTown_EventScript_OakRatingScene";
    private const string LabDexScene = "PalletTown_ProfessorOaksLab_EventScript_NationalDexScene";
    public bool NationalPokedexEnabled => NationalDexEnabledIn(State);
    public bool NationalDexScenePending => NationalDexScenePendingIn(State);
    private bool NationalDexScenePendingIn(CampaignState state) => state.HallOfFame is not null && state.PokemonLeagueCompletion is null &&
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR") &&
        state.World.Player.MapId.Value == "map.pallet-town" &&
        state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_OAK") == 2 &&
        state.Script.Completed && state.Script.Wait is null && !state.Script.Locked &&
        state.EntryPrograms.IsEmpty && state.Dialogue is null && state.PresentationTicks == 0;


    // Original event_data.c gate: none of these three values alone unlocks National mode.
    private static bool NationalDexEnabledIn(CampaignState state) => state.NationalDexMagic == 0xB9 &&
        state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX") == 0x6258 &&
        state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX");

    private string? ValidateNationalDexProgress(CampaignState state)
    {
        bool enabled = NationalDexEnabledIn(state);
        bool any = state.NationalDexMagic != 0 || state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX") != 0 ||
            state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX");
        if (any != enabled) return "save_national_dex_gate_inconsistent";
        int lab = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB");
        bool cleared = state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR");
        if (!cleared) return enabled || lab >= 7 ? "save_national_dex_clear_required" : null;
        int pallet = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_OAK");
        if (pallet is not (2 or 3) || (enabled ? lab is not (8 or 9) || pallet != 3 : lab != 6))
            return "save_national_dex_scene";
        if (enabled && (!state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_ONE_ISLAND") ||
            _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < 60))
            return "save_national_dex_prerequisites";
        if (state.Script.Flags.GetValueOrDefault("FLAG_OAK_SAW_DEX_COMPLETION") &&
            (!enabled || !_content.EvaluateNationalDex(state.SeenSpecies, state.CaughtSpecies).Complete))
            return "save_national_dex_completion_inconsistent";
        return null;
    }

    private bool TryApplyNationalDexEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool outside = effect.Source.Path == PalletSource && effect.Source.Symbol == PalletDexScene;
        bool grant = effect.Source.Path == OakLabSource && effect.Source.Symbol == LabDexScene &&
            effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "EnableNationalPokedex" });
        if (!grant && !(outside && (effect.Opcode == "buffernumberstring" || effect.Opcode == "specialvar" &&
            effect.Args.SequenceEqual(new[] { "VAR_RESULT", "GetPokedexCount" })))) return false;
        var instruction = _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i =>
            i.Source == effect.Source && i.Opcode == effect.Opcode);
        if (instruction is null || !instruction.Args.Select((arg,index) => arg.StartsWith("VAR_",StringComparison.Ordinal) &&
            !(effect.Opcode == "specialvar" && index == 0) ? state.Script.Variables.GetValueOrDefault(arg).ToString(CultureInfo.InvariantCulture) : arg)
            .SequenceEqual(effect.Args) || !state.Script.Locked ||
            state.Script.QuestLogState != 0 || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR") || state.HallOfFame is null)
            throw new InvalidDataException("national_dex_source_context");
        var counts = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies);
        if (grant)
        {
            int lab = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB");
            if (state.World.Player.MapId.Value != "map.professor-oaks-lab" || lab is not (6 or 7) ||
                !state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_ONE_ISLAND") || counts.Caught < 60 ||
                state.NationalDexMagic != 0 || state.Script.Variables.GetValueOrDefault("VAR_NATIONAL_DEX") != 0 ||
                state.Script.Flags.GetValueOrDefault("FLAG_SYS_NATIONAL_DEX"))
                throw new InvalidDataException("national_dex_grant_prerequisites");
            next = state with { NationalDexMagic = 0xB9, Script = state.Script with {
                Variables = state.Script.Variables.SetItem("VAR_NATIONAL_DEX", 0x6258),
                Flags = state.Script.Flags.SetItem("FLAG_SYS_NATIONAL_DEX", true) } };
            return true;
        }
        if (state.World.Player.MapId.Value != "map.pallet-town" ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_OAK") != 2)
            throw new InvalidDataException("national_dex_outdoor_context");
        if (effect.Opcode == "specialvar")
        {
            if (state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 0)
                throw new InvalidDataException("national_dex_outdoor_count_selector");
            next = state with { Script = state.Script with { Variables = state.Script.Variables
                .SetItem("VAR_RESULT", NationalDexEnabledIn(state) ? 1 : 0)
                .SetItem("VAR_0x8005", counts.Seen).SetItem("VAR_0x8006", counts.Caught) } };
        }
        else
        {
            if (effect.Args.Length != 2 || effect.Args[0] is not ("STR_VAR_1" or "STR_VAR_2") ||
                !int.TryParse(effect.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value is < 0 or > 386)
                throw new InvalidDataException("national_dex_outdoor_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(effect.Args[0], value.ToString(CultureInfo.InvariantCulture)) };
        }
        return true;
    }
}
