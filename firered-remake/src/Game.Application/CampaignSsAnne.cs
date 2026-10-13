using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Game.Domain.Trainers;

namespace Game.Application;

/// <summary>Finite source task schedule. Presentation timing never advances gameplay RNG.</summary>
public static class SsAnneSourceDeparture
{
    public const string Exterior = "map.ssanne-exterior";
    public const string Boat = "LOCALID_SS_ANNE";
    public const int InitFrames = 50, FinishFrames = 40;
    public static int RunFrames(int initialSpriteX)
    {
        if (initialSpriteX is < 0 or > 240) throw new ArgumentOutOfRangeException(nameof(initialSpriteX));
        // The task tests the previous x2 before assigning -(++counter / 5).
        return checked((initialSpriteX + 121) * 5 + 1);
    }
    public static int TotalFrames(int initialSpriteX) => InitFrames + RunFrames(initialSpriteX) + FinishFrames;
    public static int OffsetAt(int frame, int initialSpriteX)
    {
        if (frame < InitFrames) return 0;
        return -Math.Min(Math.Max(0, frame - InitFrames), RunFrames(initialSpriteX) - 1) / 5;
    }
}

public sealed partial class CampaignSession
{
    private const string SsAnneCaptainSource = "data/maps/SSAnne_CaptainsOffice/scripts.inc";
    private const string SsAnneExteriorSource = "data/maps/SSAnne_Exterior/scripts.inc";
    private const string SsAnneCaptainEntry = "SSAnne_CaptainsOffice_EventScript_Captain";
    private static readonly ItemId Hm01 = new("pret:ITEM_HM01");

    private bool TryApplySsAnneEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SsAnneEnabled) return false;
        var a = effect.Args;
        if(effect.Opcode=="bufferstdstring"&&a.Length==2&&a[1]=="STDSTRING_BERRY_POUCH")
        {
            // The shared obtain-item script formats both field pickups and NPC gifts.
            // Its switch macro has already replaced VAR_0x8000 with the pocket
            // selector. Inventory mutation authenticated the item before this text.
            if(a[0]!="STR_VAR_3"||effect.Source.Path!="data/scripts/obtain_item.inc"||effect.Source.Symbol!="EventScript_BufferBerryPouch"||
                !_content.TryConstant("POCKET_BERRY_POUCH",out int pocket)||state.Script.Variables.GetValueOrDefault("VAR_0x8000")!=pocket)
                throw new InvalidDataException("berry_pouch_text_context");
            next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_3",_content.TextPages("gText_BerryPouch_2")[0])};return true;
        }
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
            : _content.TryConstant(token, out n) ? n : throw new InvalidDataException("ssanne_operand_missing: " + token);
        CampaignState Result(CampaignState basis, int result) => basis with { Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_RESULT", result) } };
        void Exterior()
        {
            if (effect.Source.Path != SsAnneExteriorSource || state.World.Player.MapId.Value != SsAnneSourceDeparture.Exterior ||
                effect.Source.Symbol != "SSAnne_Exterior_ExitSSAnne" ||
                state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VERMILION_CITY") != 1 || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM01"))
                throw new InvalidDataException("ssanne_departure_source_context");
        }
        if (effect.Opcode == "checkplayergender" && effect.Source.Path == "data/maps/SSAnne_1F_Room6/scripts.inc")
        {
            if (!a.IsEmpty || state.World.Player.MapId.Value != "map.ssanne-1f-room-6") throw new InvalidDataException("ssanne_healer_gender_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
            return true;
        }
        if (effect.Opcode == "getplayerxy" && effect.Source.Path == SsAnneExteriorSource)
        {
            Exterior();
            if (!a.SequenceEqual(new[]{"VAR_0x8004", "VAR_0x8005"})) throw new InvalidDataException("ssanne_playerxy_operands");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(a[0], state.World.Player.X).SetItem(a[1], state.World.Player.Y) } };
            return true;
        }
        if (effect.Opcode == "fadenewbgm" && effect.Source.Path == SsAnneExteriorSource)
        {
            Exterior();
            if (!a.SequenceEqual(new[]{"MUS_SURF"})) throw new InvalidDataException("ssanne_departure_music");
            next = state with { Music = a[0] }; return true;
        }
        if (effect.Opcode == "special" && a.Length == 1 && a[0] == "DoSSAnneDepartureCutscene")
        {
            Exterior();
            if (!state.Actors.TryGetValue(SsAnneSourceDeparture.Boat, out var boat) || !boat.Active || boat.Invisible ||
                boat.GraphicsId != "OBJ_EVENT_GFX_SS_ANNE" || state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SS_ANNE"))
                throw new InvalidDataException("ssanne_departure_boat_missing");
            int x = 120 + (boat.X - state.World.Player.X) * 16;
            next = state with
            {
                PresentationTicks = SsAnneSourceDeparture.TotalFrames(x),
                PresentationEffects = state.PresentationEffects.Add(new(effect.Sequence,"ssanne_departure",state.World.Player.MapId,
                    [x.ToString(CultureInfo.InvariantCulture), SsAnneSourceDeparture.TotalFrames(x).ToString(CultureInfo.InvariantCulture)]))
            };
            return true;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != Value("ITEM_HM01")) return false;
        if (state.World.Player.MapId.Value != "map.ssanne-captains-office" ||
            effect.Source.Path is not (SsAnneCaptainSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s") ||
            !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET") || state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM01") ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_S_S_ANNE_2F_CORRIDOR") != 1)
            throw new InvalidDataException("ssanne_hm_reward_context");
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                throw new InvalidDataException("ssanne_hm_buffer_context");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "HM01") }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(next, Value("POCKET_TM_CASE")); return true; }
        if (a.Length > 2 || (a.Length == 2 ? Value(a[1]) : 1) != 1 || effect.Source.Path != SsAnneCaptainSource || effect.Source.Symbol != SsAnneCaptainEntry)
            throw new InvalidDataException("ssanne_hm_reward_operands");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var error = ValidateCampaignInventory(inventory); if (error is not null) throw new InvalidDataException(error);
        if (effect.Opcode == "checkitemspace") { next = Result(next, PewterMachineRules.CanAddMachine(_content.PewterMachines!, inventory, Hm01) ? 1 : 0); return true; }
        if (OpeningInventoryRules.Quantity(inventory.Machines ?? [], Hm01) != 0) throw new InvalidDataException("ssanne_hm_duplicate");
        var added = PewterMachineRules.AddMachine(_content.PewterMachines!, inventory, Hm01);
        if (!added.Succeeded) throw new InvalidDataException("ssanne_hm_add_failed: " + added.Error);
        next = state with { Inventory = added.State };
        next = Result(next, 1); return true;
    }

    private string? ValidateSsAnneProgress(CampaignState state)
    {
        if (!_content.SsAnneEnabled) return null;
        int scene = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VERMILION_CITY");
        int rival = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_S_S_ANNE_2F_CORRIDOR");
        int trigger = state.Script.Variables.GetValueOrDefault("VAR_VERMILION_CITY_TICKET_CHECK_TRIGGER");
        bool got = state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM01");
        bool gone = state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SS_ANNE");
        int hm = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], Hm01);
        if (state.Script.Flags.GetValueOrDefault("FLAG_SYS_GOT_BERRY_POUCH") != ((state.Inventory?.KeyItems??[]).Any(s=>s.Item==SsAnneItemRules.BerryPouchId))) return "save_ssanne_berry_pouch_flag";
        if (scene is not (0 or 1 or 3) || rival is < 0 or > 1 || trigger is < 0 or > 1 ||
            got != (scene != 0) || got != (hm == 1) || hm is < 0 or > 1 || gone != (scene == 3) || got && rival != 1)
            return "save_ssanne_progress_inconsistent";
        var history = (state.ForestTrainerHistory ?? []).Where(h => IsSsAnneRival(new(h.TrainerId)) && h.Outcome == WildBattleOutcome.Won).ToArray();
        if (history.Length != rival || history.Any(h => h.TrainerId != SsAnneRivalId(state))) return "save_ssanne_rival_history";
        if (state.World.Player.MapId.Value.StartsWith("map.ssanne-", StringComparison.Ordinal) &&
            (scene == 3 || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET"))) return "save_ssanne_entry_not_available";
        return null;
    }
    private static string SsAnneRivalId(CampaignState state) => "pret:TRAINER_RIVAL_SS_ANNE_" +
        (state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") switch { 2 => "SQUIRTLE", 1 => "BULBASAUR", 0 => "CHARMANDER", _ => throw new InvalidDataException("ssanne_starter_branch") });
}
