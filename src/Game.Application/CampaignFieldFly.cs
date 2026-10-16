using Game.Domain.Ids;
using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient party-menu flight, correlated to one owner, origin, destination and presentation callback.</summary>
public sealed record CampaignFlyEffect(int PartyIndex, long InstanceId, int SourceSpeciesId, string Nickname,
    TilePosition Origin, TilePosition? Destination, string OriginSection, string? DestinationSection, string Phase, string WaitId);

public static class FieldFlySourcePresentation
{
    public const string Choose = "choose", Out = "out", In = "in";
}

public sealed partial class CampaignSession
{
    public bool CanUseFieldFly(int partyIndex) => FieldFlyCommandError(State, partyIndex) is null;
    public ImmutableArray<FieldFlyDestination> FieldFlyDestinations =>
        State.FlyEffect is { Phase: FieldFlySourcePresentation.Choose } fly && _content.FieldFly is { } catalog
            ? catalog.AvailableDestinations(fly.OriginSection, State.Script.Flags).Where(row => FieldFlyArrival(row) is not null).ToImmutableArray()
            : [];

    private string? FieldFlyCommandError(CampaignState state, int partyIndex)
    {
        if (_content.FieldFly is null) return "field_fly_not_enabled";
        if (!IsStableFieldForParty(state) || state.FlyEffect is not null || state.TownMap is not null || state.Pc is not null ||
            state.SurfEffect is not null || state.FlashEffect is not null || state.StrengthEffect is not null || state.CutEffect is not null ||
            state.Fishing is not null || state.PokeFlute is not null || state.Itemfinder is not null || state.Repel is not null ||
            state.FieldPickup is not null || state.FieldEffectArguments is not null || state.Seagallop is not null || state.SeagallopMenu is not null ||
            state.SilphPad is not null ||
            state.RocketSpin is not null || state.CinnabarFall is not null || state.PokemonLeagueCompletion is not null ||
            state.TrainerTowerMenu is not null || state.TrainerTowerRecordsVisible ||
            state.TanobyCompletion is not null || state.DeoxysRockEffect is not null || state.MailInteraction is not null || state.FossilMenu is not null ||
            state.NpcTrade is not null || state.UltimateTutor is not null || state.GiftDelivery is not null || state.PrizeExchange is not null ||
            state.CoinsBox is not null || state.MoneyBox is not null || state.SafariExitPending || state.ScreenIsWhite)
            return "field_fly_event_active";
        if (partyIndex < 0 || partyIndex >= state.Party.Length) return "field_fly_party_index_invalid";
        if (!FieldFlySourceRules.KnowsFly(state.Party[partyIndex])) return "field_fly_move_missing";
        if (!state.Script.Flags.GetValueOrDefault(FieldFlySourceRules.BadgeFlag)) return "field_fly_badge_required";
        var map = _content.MapDefinition(state.World.Player.MapId);
        if (!FieldFlySourceRules.MapTypeAllowsFly(map.MapType) || _content.FieldFly.RegionForSection(map.RegionMapSection) is null)
            return "field_fly_cannot_use_here";
        return null;
    }

    private TilePosition? FieldFlyArrival(FieldFlyDestination destination)
    {
        // Fly menu sections for Route4/10 Centers differ from their exterior route section.
        // The authenticated destination fixes the exact map/coordinate; region identity is the map-level invariant.
        if (_content.FieldFly is not {} fly || !_content.Maps.TryGetValue(destination.MapId, out var map) ||
            _content.MapDefinition(destination.MapId) is not { } definition || definition.SourceMapSymbol != destination.MapSymbol ||
            fly.RegionForSection(definition.RegionMapSection) != destination.Region || !FieldFlySourceRules.MapTypeAllowsFly(definition.MapType) ||
            map.GetTile(destination.X, destination.Y) is not { Blocked: false } tile)
            return null;
        return new(destination.MapId, destination.X, destination.Y, tile.Elevation, FieldFlySourceRules.ArrivalFacing);
    }

    private Decision<CampaignState> StartFieldFly(CampaignState state, CampaignCommand command)
    {
        if (FieldFlyCommandError(state, command.PartyIndex) is { } error) return new(CommandStatus.Rejected, state, error);
        var mon = state.Party[command.PartyIndex];
        if (command.TargetInstanceId is { } instance && instance != mon.InstanceId)
            return new(CommandStatus.Rejected, state, "field_fly_owner_mismatch");
        var source = _content.MapDefinition(state.World.Player.MapId);
        return new(CommandStatus.Accepted, state with
        {
            TownMap = new("fly"),
            FlyEffect = new(command.PartyIndex, mon.InstanceId, mon.Pokemon.SourceSpeciesId, mon.Nickname,
                state.World.Player, null, source.RegionMapSection, null, FieldFlySourcePresentation.Choose,
                "field-fly:" + checked(Revision + 1) + ":choose")
        });
    }

    private bool FieldFlyOwnerMatches(CampaignState state, CampaignFlyEffect effect) =>
        effect.PartyIndex >= 0 && effect.PartyIndex < state.Party.Length &&
        state.Party[effect.PartyIndex].InstanceId == effect.InstanceId &&
        state.Party[effect.PartyIndex].Pokemon.SourceSpeciesId == effect.SourceSpeciesId &&
        FieldFlySourceRules.KnowsFly(state.Party[effect.PartyIndex]) &&
        state.Script.Flags.GetValueOrDefault(FieldFlySourceRules.BadgeFlag);

    private Decision<CampaignState> DecideFieldFly(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.FlyEffect is not { } fly || _content.FieldFly is not { } catalog) return No("field_fly_not_active");
        if (fly.Phase == FieldFlySourcePresentation.Choose)
        {
            if (state.TownMap?.Origin != "fly" || state.World.Player != fly.Origin || !FieldFlyOwnerMatches(state, fly))
                return No("field_fly_context_changed");
            if (command.Kind == "town_map_close")
                return new(CommandStatus.Accepted, state with { TownMap = null, FlyEffect = null });
            if (command.Kind != "field_fly_choose") return No("field_fly_destination_pending");
            if (command.Text is not { } section || !catalog.CanTravel(fly.OriginSection, section, state.Script.Flags))
                return No("field_fly_destination_unavailable");
            var destination = catalog.DestinationForSection(section)!;
            if (FieldFlyArrival(destination) is not { } arrival) return No("field_fly_destination_invalid");
            return new(CommandStatus.Accepted, state with
            {
                TownMap = null,
                FlyEffect = fly with { Destination = arrival, DestinationSection = section, Phase = FieldFlySourcePresentation.Out,
                    WaitId = "field-fly:" + checked(Revision + 1) + ":out" }
            });
        }
        if (command.Kind != "field_fly_ack") return No("field_fly_event_active");
        if (command.Text != fly.WaitId) return No("field_fly_callback_mismatch");
        if (state.TownMap is not null || !FieldFlyOwnerMatches(state, fly) || fly.DestinationSection is not { } target ||
            !catalog.CanTravel(fly.OriginSection, target, state.Script.Flags) ||
            catalog.DestinationForSection(target) is not { } row || FieldFlyArrival(row) is not { } landing || landing != fly.Destination)
            return No("field_fly_context_changed");
        if (fly.Phase == FieldFlySourcePresentation.Out)
        {
            if (state.World.Player != fly.Origin || _content.MapDefinition(fly.Origin.MapId).RegionMapSection != fly.OriginSection ||
                !FieldFlySourceRules.MapTypeAllowsFly(_content.MapDefinition(fly.Origin.MapId).MapType))
                return No("field_fly_origin_changed");
            var flags = state.Script.Flags;
            foreach (string flag in FieldFlySourceRules.ClearedFlags) flags = flags.SetItem(flag, false);
            var variables = state.Script.Variables;
            foreach (string variable in FieldFlySourceRules.ResetVariables) variables = variables.SetItem(variable, 0);
            var next = state with
            {
                FlyEffect = fly with { Phase = FieldFlySourcePresentation.In, WaitId = "field-fly:" + checked(Revision + 1) + ":in" },
                Script = state.Script with { Flags = flags, Variables = variables },
                World = state.World with { Revision = checked(state.World.Revision + 1), Player = landing },
                IsSurfing = false, IsCycling = false, SafariVisit = null, SafariExitPending = false,
                Music = _content.MapDefinition(landing.MapId).Music, SavedMusic = "MUS_DUMMY",
                Exploration = state.Exploration is null ? null : ResetEncounterModifiers(state.Exploration)
            };
            // LoadMapFromWarp clears saved music; FlyIntoMap plays the destination map theme.
            // The ordinary entry owner handles actors, temp state, visited flags and the roamer RNG transition.
            // Flight neither heals nor registers a Pokemon Center; only source entry programs may change those values.
            return new(CommandStatus.Accepted, Pump(EnterMap(next, FieldFlySourceRules.ArrivalFacing)));
        }
        if (fly.Phase != FieldFlySourcePresentation.In || state.World.Player != landing)
            return No("field_fly_arrival_changed");
        // Entry programs may have their own dialogue/presentation wait. Preserve it for the ordinary script dispatcher.
        return new(CommandStatus.Accepted, Pump(state with { FlyEffect = null }));
    }

    private string? ValidateFieldFlyProgress(CampaignState state)
    {
        if (state.FlyEffect is not null) return "save_event_active";
        bool received = state.Script.Flags.GetValueOrDefault(FieldFlySourceRules.GiftFlag);
        int count = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], new ItemId("pret:ITEM_HM02"));
        if (_content.FieldFly is null) return received || count != 0 ? "save_field_fly_profile_missing" : null;
        return count == (received ? 1 : 0) ? null : "save_field_fly_hm02_reward_mismatch";
    }

    private bool TryApplyFieldFlyGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (_content.FieldFly is null || effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        var a = effect.Args;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n
            : _content.TryConstant(token, out n) ? n : throw new InvalidDataException("field_fly_gift_operand: " + token);
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != Value("ITEM_HM02")) return false;
        if (state.World.Player.MapId.Value != FieldFlySourceRules.GiftMap || state.LastTalked is null ||
            !state.Actors.TryGetValue(state.LastTalked, out var actor) || actor.SourceLocalId != FieldFlySourceRules.GiftActorLocalId ||
            actor.ScriptId != FieldFlySourceRules.GiftScript || !actor.Active || actor.Invisible || !state.Script.Locked || state.Script.QuestLogState != 0 ||
            effect.Source.Path is not ("data/maps/Route16_House/scripts.inc" or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("field_fly_gift_context");
        CampaignState Result(CampaignState basis, int value) => basis with
        { Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                throw new InvalidDataException("field_fly_gift_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "HM02") }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(state, Value("POCKET_TM_CASE")); return true; }
        if (effect.Source.Path != "data/maps/Route16_House/scripts.inc" || effect.Source.Symbol != FieldFlySourceRules.GiftScript ||
            a.Length > 2 || (a.Length == 2 ? Value(a[1]) : 1) != 1 || state.Script.Flags.GetValueOrDefault(FieldFlySourceRules.GiftFlag))
            throw new InvalidDataException("field_fly_gift_reward_context");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
        if (OpeningInventoryRules.Quantity(inventory.Machines ?? [], new ItemId("pret:ITEM_HM02")) != 0)
            throw new InvalidDataException("field_fly_gift_duplicate_hm02");
        var added = MachineRules.AddMachine(_content.Machines!, inventory, new ItemId("pret:ITEM_HM02"));
        if (effect.Opcode == "checkitemspace") { next = Result(state, added.Succeeded ? 1 : 0); return true; }
        if (!added.Succeeded) throw new InvalidDataException("field_fly_gift_failed: " + added.Error);
        next = Result(state with { Inventory = added.State }, 1); return true;
    }
}
