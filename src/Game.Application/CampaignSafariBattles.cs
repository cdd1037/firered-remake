using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Safari;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>A Safari target and its presentation, with no ordinary player combatant or rewards.</summary>
public sealed record CampaignSafariBattle(string Id, SafariBattleState State, string HeldItem,
    CampaignWildOrigin Origin, CampaignBattlePhase Phase, ImmutableArray<SafariBattleEvent> Events,
    ImmutableArray<string> Pages, int PageIndex, ImmutableArray<RandomTrace> RandomTrace)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CampaignPendingCapture? Capture { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FishingEncounterOrigin? Fishing { get; init; }
}

public sealed partial class CampaignSession
{
    // Generation and original held-item assignment belong to the caller. Start consumes only the
    // Safari turn-number draw. Completing the candidate never regenerates this permanent identity.
    private CampaignState BeginSafariBattle(CampaignState state, PokemonInstance generated,
        RngState rng, ImmutableArray<RandomTrace> generationTrace, string heldItem, WaterEncounterState? waterEncounter=null, FishingEncounterOrigin? fishingOrigin=null)
    {
        var exploration = state.Exploration;
        var pending = exploration?.EarlyEncounter?.Pending;
        if (!_content.SafariEnabled || !_content.CaptureEnabled || !_content.PokemonStorageEnabled ||
            _content.PresetDoubleMechanics is null || state.SafariVisit is not { Active: true, Balls: > 0 and <= 30, Steps: > 0 and <= 600 } visit ||
            !state.Script.Flags.GetValueOrDefault(SafariFlag) || !InSafariArea(state) ||
            state.SafariBattle is not null || state.WildBattle is not null || state.Battle is not null ||
            state.ForestTrainerBattle is not null || state.DoubleBattle is not null || state.Party.IsEmpty ||
            exploration is null || exploration.Captured is not { IsDefault: false } ||
            exploration.NextWildBattleId is < 1 or long.MaxValue || state.NextPokemonId is < 2 or long.MaxValue ||
            long.MaxValue - Revision < 256 || (waterEncounter is null&&fishingOrigin is null&&pending is null) || generationTrace.IsDefault)
            throw new InvalidDataException("safari_battle_context_invalid");
        var table = waterEncounter is null && fishingOrigin is null ? _content.LandEncounters![state.World.Player.MapId] : null;
        var waterTable=waterEncounter is null?null:_content.WaterEncounters![state.World.Player.MapId];
        var candidateMap=fishingOrigin?.Map??waterEncounter?.Pending?.Candidate.Map??pending!.Candidate.Map;
        var candidateSpecies=fishingOrigin?.Species??waterEncounter?.Pending?.Candidate.Species??pending!.Candidate.Species;
        var candidateLevel=fishingOrigin?.Level??waterEncounter?.Pending?.Candidate.Level??pending!.Candidate.Level;
        if (fishingOrigin is not null && (waterEncounter is not null || pending is not null ||
            _content.SafariFishing?.SourceSlot(fishingOrigin) is null)) throw new InvalidDataException("safari_fishing_candidate_mismatch");
        if (candidateMap != state.World.Player.MapId || candidateSpecies != generated.Species || candidateLevel != generated.Level)
            throw new InvalidDataException("safari_generated_candidate_mismatch");
        var started = SafariBattleRules.Start(_content.PresetDoubleMechanics, generated, visit.Balls, rng);
        if (!started.Succeeded) throw new InvalidDataException(started.Fault);
        var origin = new CampaignWildOrigin(_content.MapDefinition(state.World.Player.MapId).RegionMapSection,
            generated.Level, _content.PokemonContent!.SpeciesNames[generated.SourceSpeciesId]);
        // Check the owned projection before accepting a battle whose caught receipt we could not deliver.
        var owned = new PartyPokemon(state.NextPokemonId, generated with { OriginalTrainerId = state.TrainerId },
            origin.DefaultName, state.PlayerName, state.PlayerGender, 2, origin.MetLocation,
            origin.MetLevel, 4, "ITEM_SAFARI_BALL", heldItem);
        var error = PokemonStateValidator.ValidateParty(_content.PokemonContent, owned);
        if (error is not null) throw new InvalidDataException(error);
        var battle = new CampaignSafariBattle("wild-battle:" + exploration.NextWildBattleId, started.State!,
            heldItem, origin, CampaignBattlePhase.Events, [], [$"Wild {origin.DefaultName} appeared!"], 0,
            [.. generationTrace, .. started.RandomTrace]) { Fishing = fishingOrigin };
        state = ResetFieldPoisonCounterForBattle(state);
        return state with
        {
            SafariBattle = battle,
            Exploration = exploration with
            {
                EarlyEncounter = fishingOrigin is not null ? exploration.EarlyEncounter : waterEncounter is null?EarlyLandEncounterRules.CompleteCandidate(exploration.EarlyEncounter!, table!):
                    SharedWaterProcess(exploration.EarlyEncounter!,WaterEncounterRules.CompleteCandidate(waterEncounter,waterTable!)),
                NextWildBattleId = checked(exploration.NextWildBattleId + 1)
            },
            World = state.World with { Rng = battle.State.Rng },
            SeenSpecies = state.SeenSpecies.Add(generated.SourceSpeciesId), Motions = [], PresentationTicks = 0
        };
    }

    private Decision<CampaignState> DecideSafariBattle(CampaignState state, CampaignCommand command)
    {
        var battle = state.SafariBattle!;
        if (battle.Capture is not null) return DecideSafariCapture(state, command);
        if (command.Kind == "ack" && battle.Phase == CampaignBattlePhase.Events)
        {
            if (battle.PageIndex + 1 < battle.Pages.Length)
                return new(CommandStatus.Accepted, state with { SafariBattle = battle with { PageIndex = battle.PageIndex + 1 } });
            if (battle.State.Outcome == SafariBattleOutcome.Active)
                return new(CommandStatus.Accepted, state with { SafariBattle = battle with { Phase = CampaignBattlePhase.ChooseMove, Pages = [], PageIndex = 0 } });
            if (battle.State.Outcome == SafariBattleOutcome.Caught)
                return new(CommandStatus.Accepted, BeginSafariCapture(state));
            var history = SafariHistory(battle, WildBattleOutcome.Escaped);
            if (state.Exploration!.Battles.Any(h => h.Id == battle.Id))
                throw new InvalidDataException("safari_settlement_duplicate");
            return new(CommandStatus.Accepted, ReturnFromSafariBattle(state with
            { Exploration = state.Exploration with { Battles = state.Exploration.Battles.Add(history) } }));
        }
        if (battle.Phase != CampaignBattlePhase.ChooseMove)
            return new(CommandStatus.Rejected, state, "safari_presentation_pending");
        SafariAction? action = command.Kind != "safari_action" ? null : command.Text switch
        { "ball" => SafariAction.Ball, "bait" => SafariAction.Bait, "rock" => SafariAction.Rock, "run" => SafariAction.Run, _ => null };
        if (action is null) return new(CommandStatus.Rejected, state, "safari_action_required");
        if (state.SafariVisit is not { Active: true } visit || visit.Balls != battle.State.BallsRemaining)
            throw new InvalidDataException("safari_visit_ball_mismatch");
        if (action == SafariAction.Ball && (state.NextPokemonId is < 2 or long.MaxValue || long.MaxValue - Revision < 256))
            return new(CommandStatus.Rejected, state, "capture_counter_headroom_required");
        var result = SafariBattleRules.Apply(_content.PresetDoubleMechanics!, battle.State, action.Value,
            CanReceiveSafariPokemon(state));
        if (!result.Succeeded) return new(CommandStatus.Rejected, state, result.Fault);
        return new(CommandStatus.Accepted, state with
        {
            SafariBattle = battle with { State = result.State!, Events = result.Events,
                Pages = SafariActionPages(state.PlayerName, battle.Origin.DefaultName, result.Events),
                PageIndex = 0, Phase = CampaignBattlePhase.Events, RandomTrace = result.RandomTrace },
            SafariVisit = visit with { Balls = result.State!.BallsRemaining },
            World = state.World with { Rng = result.State.Rng }
        });
    }

    private static ImmutableArray<string> SafariActionPages(string player, string species,
        ImmutableArray<SafariBattleEvent> events)
    {
        var pages = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in events)
        {
            switch (entry)
            {
                case SafariActionEvent { Action: SafariAction.Ball }: pages.Add($"{player} used\nSAFARI BALL!"); break;
                case SafariActionEvent { Action: SafariAction.Bait }: pages.Add($"{player} threw some BAIT\nat the {species}!"); break;
                case SafariActionEvent { Action: SafariAction.Rock }: pages.Add($"{player} threw a ROCK\nat the {species}!"); break;
                case SafariCaptureEvent capture:
                    pages.Add(capture.Capture.Outcome == CaptureOutcome.Caught ? $"Gotcha!\n{species} was caught!" : capture.Capture.ChecksPassed switch
                    { 0 => "Oh, no!\nThe POKéMON broke free!", 1 => "Aww!\nIt appeared to be caught!", 2 => "Aargh!\nAlmost had it!", _ => "Shoot!\nIt was so close, too!" });
                    break;
                case SafariReactionEvent reaction:
                    pages.Add(reaction.Reaction switch { SafariReaction.Eating => $"{species} is eating!",
                        SafariReaction.Angry => $"{species} is angry!", _ => $"{species} is watching\ncarefully!" });
                    break;
                case SafariBattleEndedEvent { Outcome: SafariBattleOutcome.Ran }: pages.Add("Got away safely!"); break;
                case SafariBattleEndedEvent { Outcome: SafariBattleOutcome.Fled }: pages.Add($"Wild {species} fled!"); break;
                case SafariBattleEndedEvent { Outcome: SafariBattleOutcome.OutOfBalls }:
                    pages.Add("ANNOUNCER: You're out of\nSAFARI BALLS! Game over!"); break;
            }
        }
        if (pages.Count == 0) throw new InvalidDataException("safari_presentation_empty");
        return pages.ToImmutable();
    }

    private CampaignState BeginSafariCapture(CampaignState state)
    {
        var battle = state.SafariBattle!;
        if (battle.State.Outcome != SafariBattleOutcome.Caught || battle.Capture is not null ||
            CaptureCapacityError(state) is not null || !battle.Events.OfType<SafariCaptureEvent>().Any(c => c.Capture.Outcome == CaptureOutcome.Caught))
            throw new InvalidDataException("safari_capture_context_invalid");
        bool first = !state.CaughtSpecies.Contains(battle.State.Target.SourceSpeciesId);
        var capture = new CampaignPendingCapture(battle.State.Target, battle.HeldItem, 0, battle.Origin,
            first, first ? CampaignCaptureStage.DexNotice : CampaignCaptureStage.NicknameChoice) { Ball = "ITEM_SAFARI_BALL" };
        return state with { CaughtSpecies = state.CaughtSpecies.Add(battle.State.Target.SourceSpeciesId),
            SafariBattle = battle with { Capture = capture, Phase = CampaignBattlePhase.Results, Pages = [], PageIndex = 0 } };
    }

    private Decision<CampaignState> DecideSafariCapture(CampaignState state, CampaignCommand command)
    {
        var battle = state.SafariBattle!;
        var capture = battle.Capture!;
        if (command.Kind == "ack" && capture.Stage == CampaignCaptureStage.SentToPc)
        {
            var pages = _content.TextPages(capture.MessageSymbol!);
            return new(CommandStatus.Accepted, capture.MessagePage + 1 < pages.Length
                ? state with { SafariBattle = battle with { Capture = capture with { MessagePage = capture.MessagePage + 1 } } }
                : ReturnFromSafariBattle(state with { SafariBattle = battle with { Capture = null } }));
        }
        if (command.Kind == "ack" && capture.Stage is CampaignCaptureStage.DexNotice or CampaignCaptureStage.DexEntry)
            return new(CommandStatus.Accepted, state with { SafariBattle = battle with { Capture = capture with
            { Stage = capture.Stage == CampaignCaptureStage.DexNotice ? CampaignCaptureStage.DexEntry : CampaignCaptureStage.NicknameChoice } } });
        if (command.Kind == "capture_choice" && capture.Stage == CampaignCaptureStage.NicknameChoice)
        {
            if (command.Choice == 0) return new(CommandStatus.Accepted, DeliverSafariCapture(state, capture.Origin.DefaultName));
            if (command.Choice == 1) return new(CommandStatus.Accepted, state with
            { SafariBattle = battle with { Capture = capture with { Stage = CampaignCaptureStage.Naming } } });
            return new(CommandStatus.Rejected, state, "capture_choice_required");
        }
        if (command.Kind == "nickname" && capture.Stage == CampaignCaptureStage.Naming)
        {
            if (command.Text is null) return new(CommandStatus.Rejected, state, "nickname_text_required");
            var alphabet = _content.PokemonContent!.NicknameCharacters;
            var error = PokemonNicknameRules.Validate(command.Text, alphabet);
            if (error is not null) return new(CommandStatus.Rejected, state, error);
            return new(CommandStatus.Accepted, DeliverSafariCapture(state,
                PokemonNicknameRules.Apply(capture.Origin.DefaultName, command.Text, alphabet)));
        }
        return new(CommandStatus.Rejected, state, "capture_presentation_pending");
    }

    private CampaignState DeliverSafariCapture(CampaignState state, string nickname)
    {
        var battle = state.SafariBattle!;
        var capture = battle.Capture!;
        var exploration = state.Exploration!;
        var captures = exploration.Captured!.Value;
        if (battle.State.Outcome != SafariBattleOutcome.Caught || capture.Stage is not (CampaignCaptureStage.Naming or CampaignCaptureStage.NicknameChoice) ||
            capture.Ball != "ITEM_SAFARI_BALL" || CaptureCapacityError(state) is not null || state.NextPokemonId is < 2 or long.MaxValue ||
            captures.Any(c => c.BattleId == battle.Id) || exploration.Battles.Any(h => h.Id == battle.Id))
            throw new InvalidDataException("safari_capture_already_committed_or_full");
        var pokemon = capture.Pokemon with { OriginalTrainerId = state.TrainerId };
        var owned = new PartyPokemon(state.NextPokemonId, pokemon, nickname, state.PlayerName, state.PlayerGender,
            2, capture.Origin.MetLocation, capture.Origin.MetLevel, 4, "ITEM_SAFARI_BALL", capture.HeldItem) { Status = capture.Status };
        var received = PokemonStorageRules.Receive(_content.PokemonContent, state.Party, state.Storage!, owned);
        if (!received.Succeeded) throw new InvalidDataException(received.Diagnostic!.Code);
        var receipt = new CampaignCapturedPokemon(owned.InstanceId, battle.Id, pokemon.SourceSpeciesId,
            pokemon.Personality, pokemon.Ivs, capture.Origin.MetLevel, capture.Origin.MetLocation) { Ball = "ITEM_SAFARI_BALL", Fishing = battle.Fishing };
        var next = state with { Party = received.Party, Storage = received.Storage, NextPokemonId = checked(state.NextPokemonId + 1),
            Exploration = exploration with { Captured = captures.Add(receipt), Battles = exploration.Battles.Add(SafariHistory(battle, WildBattleOutcome.Caught)) },
            SafariBattle = battle with { Capture = null } };
        if (received.Outcome != PokemonStorageOutcome.ReceivedInBox) return ReturnFromSafariBattle(next);

        // Use the ordinary source PC strings, including current-box rollover and Bill's flag.
        int oldBox = state.Script.Variables.GetValueOrDefault("VAR_PC_BOX_TO_SEND_MON"), box = received.Address!.BoxIndex;
        var flags = state.Script.Flags;
        if (box != oldBox) flags = flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE", false);
        bool wasFull = state.Storage!.CurrentBox != box && !flags.GetValueOrDefault("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE");
        if (wasFull) flags = flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE", true);
        bool bill = flags.GetValueOrDefault("FLAG_SYS_NOT_SOMEONES_PC");
        string symbol = wasFull ? (bill ? "Text_MonSentToBoxBillsBoxFull" : "Text_MonSentToBoxSomeonesBoxFull")
            : (bill ? "Text_MonSentToBoxInBillsPC" : "Text_MonSentToBoxInSomeonesPC");
        return next with { Script = next.Script with { Flags = flags, Variables = next.Script.Variables.SetItem("VAR_PC_BOX_TO_SEND_MON", box) },
            TextBuffers = next.TextBuffers.SetItem("STR_VAR_1", "BOX" + (box + 1)).SetItem("STR_VAR_2", nickname).SetItem("STR_VAR_3", "BOX" + (oldBox + 1)),
            SafariBattle = battle with { Capture = capture with { Stage = CampaignCaptureStage.SentToPc,
                Address = received.Address, MessageSymbol = symbol } } };
    }

    private static CampaignWildHistory SafariHistory(CampaignSafariBattle battle, WildBattleOutcome outcome) =>
        new(battle.Id, battle.State.Target.SourceSpeciesId, battle.State.Target.Level, outcome, 0) { Safari = true, Fishing = battle.Fishing };

    private CampaignState ReturnFromSafariBattle(CampaignState state)
    {
        var battle = state.SafariBattle!;
        if (battle.State.Outcome == SafariBattleOutcome.Active || battle.Capture is not null ||
            !state.Exploration!.Battles.Any(h => h.Id == battle.Id && h.Safari && h.ExperienceGained == 0))
            throw new InvalidDataException("safari_return_unsettled");
        var next = state with { SafariBattle = null, Music = FieldTravelMusic(state),
            Exploration = ResetEncounterModifiers(state.Exploration) };
        // Failed final ball announces game over in battle, then scene 3/setwarp. A caught final
        // ball reaches the field's out-of-balls message only after Dex, naming and PC delivery.
        if (battle.State.Outcome == SafariBattleOutcome.OutOfBalls)
            return Pump(StartStory(next, "SafariZone_EventScript_OutOfBallsMidBattle"));
        if (battle.State.Outcome == SafariBattleOutcome.Caught && battle.State.BallsRemaining == 0)
            return Pump(StartStory(next, "SafariZone_EventScript_OutOfBalls"));
        return next;
    }

    private bool SafariOriginSupported(int species, int level, string? location = null) =>
        SurfOriginSupported(species,level,location,safariOnly:true) ||
        _content.SafariEnabled && (location is null || location == "MAPSEC_KANTO_SAFARI_ZONE") &&
        _content.LandEncounters!.Values.Any(table => _content.MapDefinition(table.Map).SourceMapSymbol.StartsWith("MAP_SAFARI_ZONE_", StringComparison.Ordinal) &&
            table.Slots.Any(slot => _content.PokemonContent!.Catalog.Species[slot.Species].SourceSpeciesId == species && level >= slot.MinimumLevel && level <= slot.MaximumLevel));

    private bool IsSafariHistory(CampaignWildHistory history) => history.Fishing is not null ? IsSafariFishingHistory(history) : history.Safari && !history.ScriptedMarowak &&
        history.ScriptedSnorlaxRoute == 0 && history.ExperienceGained == 0 &&
        history.Outcome is WildBattleOutcome.Caught or WildBattleOutcome.Escaped && SafariOriginSupported(history.SourceSpeciesId, history.Level);

    private bool IsSafariCapture(CampaignState state, CampaignCapturedPokemon capture) =>
        capture.Fishing is not null ? IsSafariFishingCapture(state, capture) : capture.Ball == "ITEM_SAFARI_BALL" && capture.MetLocation == "MAPSEC_KANTO_SAFARI_ZONE" &&
        SafariOriginSupported(capture.SourceSpeciesId, capture.MetLevel, capture.MetLocation) &&
        state.Exploration!.Battles.Any(history => history.Id == capture.BattleId && IsSafariHistory(history) &&
            history.Outcome == WildBattleOutcome.Caught && history.SourceSpeciesId == capture.SourceSpeciesId && history.Level == capture.MetLevel);

    public string? VisibleSafariMessage
    {
        get
        {
            if (State.SafariBattle is not { } battle) return null;
            if (battle.Capture is { Stage: CampaignCaptureStage.SentToPc, MessageSymbol: { } symbol } capture)
            {
                var text = _content.TextPages(symbol)[capture.MessagePage];
                foreach (var buffer in State.TextBuffers) text = text.Replace("{" + buffer.Key + "}", buffer.Value, StringComparison.Ordinal);
                return text;
            }
            return battle.PageIndex < battle.Pages.Length ? battle.Pages[battle.PageIndex] : null;
        }
    }
}
