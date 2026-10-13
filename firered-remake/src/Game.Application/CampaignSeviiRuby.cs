using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

/// <summary>The first Celio gem quest. Original scripts own its ordering and rewards.</summary>
public sealed partial class CampaignSession
{
    public const string SeviiRubyRulesVersion = "firered-sevii-ruby-campaign-v1";
    public bool SeviiRubyEnabled => _content.SeviiRubyEnabled;
    private const string RubyCenterMap = "map.one-island-pokemon-center-1f";
    private const string RubyExteriorMap = "map.mt-ember-exterior";
    private const string RubyCenterSource = "data/maps/OneIsland_PokemonCenter_1F/scripts.inc";
    private const string RubyExteriorSource = "data/maps/MtEmber_Exterior/scripts.inc";
    private const string RubyCenterScene = "VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F";
    private static readonly ItemId RubyItem = new("pret:ITEM_RUBY"), RainbowPassItem = new("pret:ITEM_RAINBOW_PASS");
    private static bool SeviiRubyMap(MapId map) => map.Value.StartsWith("map.mt-ember-", StringComparison.Ordinal) ||
        map.Value is "map.one-island-kindle-road" or "map.one-island-kindle-road-ember-spa";
    private static bool SeviiRubyEditedMap(MapId map) => map.Value is RubyCenterMap or RubyExteriorMap;
    private static bool SeviiRubySource(string path) => path.StartsWith("data/maps/MtEmber_", StringComparison.Ordinal) ||
        path.StartsWith("data/maps/OneIsland_KindleRoad", StringComparison.Ordinal) || path == RubyCenterSource;
    private bool InSeviiRubyScript(CampaignState state) => _content.SeviiRubyEnabled && state.Script.Stack.Any(f =>
        _content.FindProgram(f.ProgramId)?.Instructions.Any(i => SeviiRubySource(i.Source.Path)) == true);

    private static void ValidateSeviiRubyCatalog(ICampaignCatalog content)
    {
        if (!content.SeviiRubyEnabled)
        {
            if (content.SeviiRubyOwnership is not null || content.SeviiRubyOpponents is not null || content.SeviiRubyTrainers is not null)
                throw new InvalidDataException("sevii_ruby_profile_missing");
            return;
        }
        if (!content.NationalDexEnabled || content.Maps.Count != 229 || content.SeviiRubyOwnership is not { } owned ||
            content.SeviiRubyOpponents is not { } opponents || content.SeviiRubyTrainers is not { } trainers ||
            !ReferenceEquals(owned.Predecessor, content.CinnabarOwnership) || !ReferenceEquals(content.PokemonContent?.Catalog, owned.Catalog) ||
            !ReferenceEquals(opponents.Owned, owned) || !ReferenceEquals(opponents.Predecessor, content.PokemonLeagueOpponents) ||
            SeviiRubyMachineCatalog.For(content.PewterMachines) is not { } machines || !ReferenceEquals(machines.Pokemon, owned) ||
            SeviiRubyShopCatalog.For(content.EarlyShop) is null) throw new InvalidDataException("sevii_ruby_source_profile_missing");
        SeviiRubyTrainerPartyFactory.ValidateCatalog(trainers);
    }

    private ImmutableSortedDictionary<string, FieldMetatileEdit> SeviiRubyExpectedEdits(CampaignState state)
    {
        string map = state.World.Player.MapId.Value;
        int scene = state.Script.Variables.GetValueOrDefault(RubyCenterScene);
        string? program = map == RubyExteriorMap && scene >= 4 ? "MtEmber_Exterior_EventScript_OpenCave" :
            map == RubyCenterMap && scene >= 5 ? "OneIsland_PokemonCenter_1F_EventScript_SetRubyMetatile" : null;
        var result = ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty;
        if (program is null) return result;
        var instruction = _content.FindProgram(program)?.Instructions.SingleOrDefault(i => i.Opcode == "setmetatile") ??
            throw new InvalidDataException("sevii_ruby_metatile_source_missing");
        var a = instruction.Args;
        int x = SeviiValue(state, a[0]), y = SeviiValue(state, a[1]), tile = SeviiValue(state, a[2]);
        var cell = _content.Maps[state.World.Player.MapId].GetTile(x, y) ?? throw new InvalidDataException("sevii_ruby_metatile_cell");
        if (_content.MetatileDefinition(state.World.Player.MapId, tile) is null) throw new InvalidDataException("sevii_ruby_metatile_definition");
        return result.Add(x + ":" + y, new(x, y, tile, SeviiValue(state, a[3]) == 0 ? 0 : 3, cell.Elevation));
    }

    private string? ValidateSeviiRubyProgress(CampaignState state)
    {
        bool Flag(string key) => state.Script.Flags.GetValueOrDefault(key);
        int Var(string key) => state.Script.Variables.GetValueOrDefault(key);
        int Count(ItemId item) => OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], item);
        int scene = Var(RubyCenterScene), exterior = Var("VAR_MAP_SCENE_MT_EMBER_EXTERIOR");
        bool got = Flag("FLAG_GOT_RUBY"), rainbow = Flag("FLAG_SYS_SEVII_MAP_4567");
        if (!_content.SeviiRubyEnabled) return got || rainbow || exterior != 0 || scene > 3 || Count(RubyItem) != 0 || Count(RainbowPassItem) != 0 ||
            Flag("FLAG_GOT_HM06") ? "save_sevii_ruby_profile_missing" : null;
        if (Flag("FLAG_RECOVERED_SAPPHIRE") || Flag("FLAG_SYS_CAN_LINK_WITH_RS") || scene > 5 || exterior is < 0 or > 2)
            return "save_sevii_sapphire_not_admitted";
        if ((scene >= 4 || got || rainbow || exterior != 0) && !NationalDexEnabledIn(state)) return "save_sevii_ruby_national_gate";
        if (scene < 4 && (exterior != 0 || got || rainbow || Count(RubyItem) != 0 || Count(RainbowPassItem) != 0) ||
            scene >= 4 && exterior == 0 || rainbow != (scene == 5) || Count(RainbowPassItem) != (scene == 5 ? 1 : 0) ||
            Count(RubyItem) > (got && scene == 4 ? 1 : 0) || scene == 5 && !got)
            return "save_sevii_ruby_quest_progress";
        // The source sets GOT_RUBY and removes the object before AddBagItem. A full
        // Key Items pocket can therefore legally retain the flag without the item.
        if (Flag("FLAG_HIDE_RUBY") != got || SeviiRubyEscapeRopeRules.IsRubyCave(state.World.Player.MapId) && scene < 4)
            return "save_sevii_ruby_visibility_or_access";
        bool bothGrunts = state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_43") &&
            state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_GRUNT_44");
        if (got && !bothGrunts || Flag("FLAG_HIDE_MT_EMBER_EXTERIOR_ROCKETS") != bothGrunts) return "save_sevii_ruby_grunts_required";
        if (OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], SeviiRubyMachineCatalog.Hm06Id) > 0 && !Flag("FLAG_GOT_HM06"))
            return "save_sevii_ruby_hm06_flag";
        if (SeviiRubyEditedMap(state.World.Player.MapId) && !(state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty)
            .SequenceEqual(SeviiRubyExpectedEdits(state))) return "save_sevii_ruby_metatiles";
        return null;
    }

    private bool TryApplySeviiRubyEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SeviiRubyEnabled) return false;
        if (TryApplyRubyBrailleEffect(state, effect, out next)) return true;
        if (TryApplySeviiRubyItemEffect(state, effect, out next)) return true;
        if (TryApplyRainbowFerryEffect(state, effect, out next)) return true;
        bool own = SeviiRubySource(effect.Source.Path);
        if (!own) return false;
        if (effect.Opcode == "goto_if_defeated" && effect.Source.Path == RubyExteriorSource)
        {
            var source = _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i => i.Source == effect.Source);
            if (!state.Script.Locked || state.World.Player.MapId.Value != RubyExteriorMap || effect.Args.Length != 2 ||
                effect.Args[0] is not ("TRAINER_TEAM_ROCKET_GRUNT_43" or "TRAINER_TEAM_ROCKET_GRUNT_44") ||
                source is null || source.Opcode != effect.Opcode || !source.Args.SequenceEqual(effect.Args) ||
                state.Script.Stack.LastOrDefault()?.ProgramId != effect.Source.Symbol || _content.FindProgram(effect.Args[1]) is null)
                throw new InvalidDataException("sevii_ruby_defeated_branch_source");
            // This macro branches on trainer history; it does not write VAR_RESULT.
            if (state.BattledTrainers.Contains("pret:" + effect.Args[0])) next = state with { Script = state.Script with {
                Stack = state.Script.Stack.SetItem(state.Script.Stack.Length - 1, new(effect.Args[1], 0)) } };
            return true;
        }
        if (effect.Opcode == "setmetatile")
        {
            bool cave = effect.Source.Path == RubyExteriorSource && effect.Source.Symbol == "MtEmber_Exterior_EventScript_OpenCave";
            bool machine = effect.Source.Path == RubyCenterSource && effect.Source.Symbol == "OneIsland_PokemonCenter_1F_EventScript_SetRubyMetatile";
            if (!cave && !machine) return false;
            var source = _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i => i.Source == effect.Source);
            if (source is null || !source.Args.SequenceEqual(effect.Args) ||
                state.World.Player.MapId.Value != (cave ? RubyExteriorMap : RubyCenterMap) ||
                state.Script.Variables.GetValueOrDefault(RubyCenterScene) < 4 ||
                machine && !state.Script.Flags.GetValueOrDefault("FLAG_GOT_RUBY")) throw new InvalidDataException("sevii_ruby_metatile_context");
            int x = SeviiValue(state, effect.Args[0]), y = SeviiValue(state, effect.Args[1]), id = SeviiValue(state, effect.Args[2]);
            var cell = _content.Maps[state.World.Player.MapId].GetTile(x, y) ?? throw new InvalidDataException("sevii_ruby_metatile_cell");
            if (_content.MetatileDefinition(state.World.Player.MapId, id) is null) throw new InvalidDataException("sevii_ruby_metatile_definition");
            next = state with { MapEdits = (state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty)
                .SetItem(x + ":" + y, new(x, y, id, SeviiValue(state, effect.Args[3]) == 0 ? 0 : 3, cell.Elevation)) };
            return true;
        }
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "DrawWholeMapView" }) && effect.Source.Path == RubyCenterSource &&
            effect.Source.Symbol == "OneIsland_PokemonCenter_1F_EventScript_GiveCelioRuby")
        {
            if (!state.Script.Locked || state.World.Player.MapId.Value != RubyCenterMap || state.MapEdits?.Count != 1)
                throw new InvalidDataException("sevii_ruby_redraw_context");
            return true;
        }
        if (effect.Opcode == "fadescreen" && effect.Source.Path == "data/maps/OneIsland_KindleRoad_EmberSpa/scripts.inc" &&
            effect.Source.Symbol == "OneIsland_KindleRoad_EmberSpa_EventScript_SpaHeal")
        {
            if (effect.Args.Length != 1 || effect.Args[0] is not ("FADE_TO_WHITE" or "FADE_FROM_WHITE")) throw new InvalidDataException("sevii_spa_fade_source");
            next = state with { ScreenIsBlack = effect.Args[0] == "FADE_TO_WHITE", PresentationTicks = 16 }; return true;
        }
        return false;
    }
}

public static partial class CampaignSaveUpgrade
{
    public static CampaignSaveUpgradeResult NationalDexToSeviiRuby(byte[] bytes, ICampaignCatalog source, ICampaignCatalog target)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var before = new CampaignSession(source); var after = new CampaignSession(target);
        if (!source.NationalDexEnabled || source.SeviiRubyEnabled || !target.SeviiRubyEnabled ||
            before.RulesVersion != CampaignSession.NationalDexRulesVersion || after.RulesVersion != CampaignSession.SeviiRubyRulesVersion)
            throw new InvalidDataException("save_upgrade_profile_pair_unsupported");
        var loaded = SaveCodec.Decode<CampaignState>(bytes, before.RulesVersion, source.ContentVersion, validate: before.ValidatePlayerSave);
        var envelope = JsonSerializer.Deserialize<SaveEnvelope>(bytes)!;
        if (CanonicalJson.Hash(loaded.State) != envelope.PayloadHash) throw new InvalidDataException("save_upgrade_payload_would_change");
        var old = loaded.State; var exploration = old.Exploration ?? throw new InvalidDataException("save_exploration_missing");
        var encounter = exploration.EarlyEncounter ?? throw new InvalidDataException("save_encounter_missing");
        var changed = old with { Exploration = exploration with { EarlyEncounter = encounter with { RulesVersion = EarlyLandEncounterRules.SeviiRubyRulesVersion } } };
        if (CanonicalJson.Hash(changed with { Exploration = exploration }) != envelope.PayloadHash) throw new InvalidDataException("save_upgrade_changed_unapproved_payload");
        if (after.ValidatePlayerSave(changed) is { } error) throw new InvalidDataException("save_upgrade_target_invalid: " + error);
        var output = SaveCodec.Encode(changed, loaded.Generation, after.RulesVersion, target.ContentVersion);
        return new(output, loaded.Generation, CanonicalJson.Hash(changed), before.RulesVersion, source.ContentVersion, after.RulesVersion, target.ContentVersion);
    }
}
