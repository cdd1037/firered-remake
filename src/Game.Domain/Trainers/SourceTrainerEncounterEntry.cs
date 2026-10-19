using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Opaque, one-entry setup result. The caller gives Rng to its authenticated regional
/// factory exactly once; Begin verifies that the factory continued this stream.</summary>
public sealed class SourceTrainerEntryPreparation
{
    public RngState InitialRng { get; }
    public RngState Rng { get; }
    public ImmutableArray<RandomTrace> Trace { get; }
    public string? Fault { get; }
    public bool Succeeded => Fault is null;
    internal SourceTrainerEntryPreparation(RngState initial, RngState rng, ImmutableArray<RandomTrace> trace, string? fault)
    { InitialRng = initial; Rng = rng; Trace = trace; Fault = fault; }
}

public sealed record SourceTrainerEncounterEntryResult(SourceBattleFrame? State, RngState Rng,
    ImmutableArray<SourceBattleEvent> Events, ImmutableArray<RandomTrace> Trace, string? Fault)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<SourceBattleSlot> EntrySlots { get; init; } = [];
    public bool Succeeded => Fault is null;
}
public sealed record SourceTrainerStartedEvent(long Sequence, int Turn, TrainerId Trainer, int OpponentCount)
    : SourceBattleEvent(Sequence, Turn);

/// <summary>Encounter initialization only; turns and entry recipes remain owned by the shared lifecycle.</summary>
public static class SourceTrainerEncounterEntry
{
    public static SourceTrainerEntryPreparation Prepare(RngState initial, string sourceTracePrefix)
    {
        if (initial.AlgorithmId != Gen3Rng.AlgorithmId || string.IsNullOrWhiteSpace(sourceTracePrefix))
            return new(initial, initial, [], "source_trainer_entry_setup");
        try
        {
            var rng = initial; var trace = ImmutableArray.CreateBuilder<RandomTrace>(4);
            for (int i = 0; i < 4; i++)
            {
                var draw = Gen3Rng.Next(rng, $"{sourceTracePrefix}.entry.ai-setup.slot-{i}", 16);
                rng = draw.State; trace.Add(draw.Trace);
            }
            return new(initial, rng, trace.MoveToImmutable(), null);
        }
        catch (OverflowException) { return new(initial, initial, [], "source_trainer_entry_setup_rng_exhausted"); }
    }

    public static SourceTrainerEncounterEntryResult Begin(PokemonCatalog catalog,
        SourceTrainerEntryPreparation setup, TrainerPartyCreationResult created,
        ImmutableArray<WildBattleCombatant> playerParty, int playerIndex,
        ForestTrainerBattleStyle style = ForestTrainerBattleStyle.Shift,
        WildBattleTerrain terrain = WildBattleTerrain.Plain, GamePokemonCatalog? owned = null,
        SourceFourRewardState? rewards = null)
    {
        SourceTrainerEncounterEntryResult Fail(string code) => new(null, setup?.InitialRng ?? new RngState(0), [], [], code);
        if (setup?.Succeeded != true || created is null) return Fail("source_trainer_entry_setup");
        var context = SourceTrainerEncounterPolicy.CreateContext(catalog, created, style);
        if (!context.Succeeded) return Fail(context.Fault!);
        // The private factory receipt already authenticated the complete trace. Bind its first
        // actual OT draw to the four setup draws, without rerunning generation or any RNG.
        if (created.Trace.IsDefaultOrEmpty || created.Trace[0].Before != setup.Rng.Seed ||
            created.Trace[0].Cursor - 1 != setup.Rng.Cursor) return Fail("source_trainer_entry_rng_chain");
        if (!Enum.IsDefined(terrain) || playerParty.IsDefaultOrEmpty || playerParty.Length > 6 ||
            playerIndex < 0 || playerIndex >= playerParty.Length || playerParty.Any(m => m?.Pokemon is null) ||
            playerParty[playerIndex].Pokemon.CurrentHp == 0 || playerParty[playerIndex].IsEgg) return Fail("source_trainer_entry_player_party");
        foreach (var mon in playerParty)
        {
            var permanent = new WildBattleCombatant(mon.Pokemon, HeldItem: mon.HeldItem, Status: mon.Status,
                AttackBadgeBoost: mon.AttackBadgeBoost) { DefenseBadgeBoost = mon.DefenseBadgeBoost,
                SpeedBadgeBoost = mon.SpeedBadgeBoost, SpecialBadgeBoost = mon.SpecialBadgeBoost, IsEgg = mon.IsEgg };
            if (mon != permanent || WildBattleSourceFieldEffects.IsToxic(mon.Status) && mon.Status != 0x80)
                return Fail("source_trainer_entry_player_volatile");
        }
        if (owned is not null && (!ReferenceEquals(owned.Mechanics.Catalog, catalog) ||
            SourceBattleAdmission.ValidateOwnedEntry(owned, playerParty, playerIndex) is { } ||
            created.Party.Any(m => !owned.Opponents.Catalog.Species.ContainsKey(m.Pokemon.Species))))
            return Fail("source_trainer_entry_campaign_admission");
        var friendship = SourceBattleFriendshipRules.TrainerEntry(created.Trainer, playerParty, rewards);
        if (friendship.Fault is { } friendshipFault) return Fail(friendshipFault);
        playerParty = friendship.Party;
        try
        {
            var players = playerParty.Select(m => new WildBattleCalledPartyMember(m.Pokemon, m.HeldItem, m.Status, m.IsEgg)).ToImmutableArray();
            var enemies = created.Party.Select(m => new WildBattleCalledPartyMember(m.Pokemon, m.HeldItem)).ToImmutableArray();
            var frame = new SourceBattleFrame {
                Layout = SourceBattleLayout.Singles, KoReplacementTiming = KoReplacementTiming.SourceImmediate,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = owned?.Opponents.RulesVersion, Campaign = owned,
                BattleKind = WildBattleCalledBattleKind.Trainer, TrainerEncounter = context.Context,
                Rng = created.Rng, SourceAbsentMask = 12, TurnStartAbsentMask = 12,
                Field = new() { Terrain = terrain }, Rewards = rewards ?? new(),
                Parties = [new(BattleSide.Player, players), new(BattleSide.Opponent, enemies)],
                Slots = [new(new(0), new(BattleSide.Player, playerIndex), 1, playerParty[playerIndex] with { FirstTurnCounter = 2 }),
                    new(new(1), new(BattleSide.Opponent, 0), 1, new(created.Party[0].Pokemon, HeldItem: created.Party[0].HeldItem) { FirstTurnCounter = 2 }),
                    new(new(2), null, 0, null), new(new(3), null, 0, null)]
            };
            if (SourceTrainerEncounterPolicy.Validate(catalog, frame) is { } invalid) return Fail(invalid);
            var trainer = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceTrainerStartedEvent(n, t, created.Trainer.Id, enemies.Length));
            var start = SourceBattleEventHooks.Publish(trainer.State, (n, t) => new SourceStartedEvent(n, t));
            var entrySlots = start.State.Slots;
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, start.State, start.State.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var draw = Gen3Rng.Next(entry.State.Rng, "wild-battle.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = draw.State, RandomTurnNumber = draw.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, draw.Value));
            return new(ready.State, ready.State.Rng, [trainer.Event, start.Event, .. entry.Events, ready.Event],
                [.. setup.Trace, .. created.Trace, .. entry.Trace, draw.Trace], null) { EntrySlots = entrySlots };
        }
        catch (OverflowException) { return Fail("source_trainer_entry_counter_exhausted"); }
        catch (ArgumentException) { return Fail("source_trainer_entry_invalid_state"); }
        catch (NotSupportedException) { return Fail("source_trainer_entry_unsupported"); }
    }
}
