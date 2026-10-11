using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Only bounded battle bookkeeping; Parties remains the permanent Pokemon authority.</summary>
public sealed record SourceFourRewardState
{
    public byte CompletedOpponentMask { get; init; }
    public ImmutableArray<byte> Participation { get; init; } = [0, 0, 0, 0, 0, 0];
    public SourceRewardContext? Context { get; init; }
    public ImmutableArray<SourceRewardMetadata> Metadata { get; init; } = [];
    public SourceRewardRecipientCursor? Pending { get; init; }
}
public sealed record SourceRewardRecipientCursor(OccupantToken Defeated, long FaintEventSequence,
    int NextPartyIndex, PartyMemberKey Recipient, ImmutableArray<WildPendingMove> Moves);
public sealed record SourceRewardLearningChoice(PartyMemberKey Recipient, int Level, MoveId Move, int? ReplaceSlot);
public enum SourceRewardLearningOutcome { Declined, Added, Replaced, AlreadyKnown }

/// <summary>Settlement and actual learning for the four-slot owner. No reward receipt supplied by a caller is trusted.</summary>
public static class SourceFourRewards
{
    internal static bool SupportsRewardHeldItem(string item) => item is "ITEM_NONE" or "ITEM_EXP_SHARE" or "ITEM_LUCKY_EGG" or "ITEM_MACHO_BRACE" or "ITEM_SOOTHE_BELL";

    internal static SourceBattleFrame ObserveParticipation(SourceBattleFrame frame)
    {
        if (frame.Layout != SourceBattleLayout.PresetDoubles || frame.Rewards is not { } rewards) return frame;
        byte players = 0;
        foreach (var cell in frame.Slots.Where(c => c.Slot.Side == BattleSide.Player && frame.CanAct(c.Slot)))
            players |= (byte)(1 << cell.Member!.Value.Index);
        var masks = rewards.Participation;
        foreach (var cell in frame.Slots.Where(c => c.Slot.Side == BattleSide.Opponent && frame.CanAct(c.Slot)))
        {
            int index = cell.Member!.Value.Index;
            masks = masks.SetItem(index, (byte)(masks[index] | players));
        }
        return frame with { Rewards = rewards with { Participation = masks } };
    }

    internal static string? Validate(SourceBattleFrame frame)
    {
        if (frame.Rewards is not { } rewards) return frame.FourTurn is null ? null : "source_four_reward_state";
        int opponentCount = frame.Party(BattleSide.Opponent).Members.Length;
        int playerCount = frame.Party(BattleSide.Player).Members.Length;
        if (frame.Layout != SourceBattleLayout.PresetDoubles || rewards.Participation.IsDefault || rewards.Participation.Length != 6 ||
            (rewards.CompletedOpponentMask >> opponentCount) != 0 || rewards.Metadata.IsDefault ||
            rewards.Participation.Where((mask, i) => (mask >> playerCount) != 0 || i >= opponentCount && mask != 0).Any() ||
            Enumerable.Range(0, opponentCount).Any(i => (rewards.CompletedOpponentMask & (1 << i)) != 0 && frame.Party(BattleSide.Opponent).Members[i].Pokemon.CurrentHp != 0) ||
            (rewards.Context is null ? !rewards.Metadata.IsEmpty : rewards.Metadata.Length != playerCount) ||
            rewards.Metadata.Where((m, i) => m is null || m.Key != new PartyMemberKey(BattleSide.Player, i)).Any())
            return "source_four_reward_state";
        if (frame.FourTurn is { } four && four.Defeats.Any(d => d.RequiresExperience && (rewards.CompletedOpponentMask & (1 << d.Occupant.Member.Index)) != 0))
            return "source_four_reward_already_completed";
        if (rewards.Pending is { } pending && (frame.FourTurn is not { Phase: SourceFourActionPhase.RewardsPending } cursor ||
            cursor.Defeats.FirstOrDefault(d => d.RequiresExperience) is not { } defeat ||
            defeat.Occupant != pending.Defeated || defeat.FaintEventSequence != pending.FaintEventSequence ||
            pending.Recipient.Side != BattleSide.Player || pending.Recipient.Index >= playerCount ||
            pending.NextPartyIndex != pending.Recipient.Index + 1 || pending.Moves.IsDefaultOrEmpty || pending.Moves.Length > 64 ||
            pending.Moves.Any(m => m is null || m.Level is < 2 or > 100 || m.Level > frame.Party(BattleSide.Player).Members[pending.Recipient.Index].Pokemon.Level) ||
            pending.Moves.Zip(pending.Moves.Skip(1)).Any(pair => pair.First.Level > pair.Second.Level)))
            return "source_four_reward_recipient_cursor";
        return null;
    }

    public static SourceFourActionResult Settle(PokemonCatalog catalog, SourceBattleFrame frame, SourceTurnContinuation continuation)
    {
        if (ValidateRequest(catalog, frame, continuation) is { } error) return Fail(frame, error);
        if (frame.Rewards!.Pending is not null) return Fail(frame, "source_four_reward_learning_required");
        return Execute(catalog, frame, null);
    }

    public static SourceFourActionResult Learn(PokemonCatalog catalog, SourceBattleFrame frame,
        SourceTurnContinuation continuation, SourceRewardLearningChoice choice)
    {
        if (ValidateRequest(catalog, frame, continuation) is { } error) return Fail(frame, error);
        if (frame.Rewards!.Pending is not { } pending || choice is null || choice.Recipient != pending.Recipient ||
            choice.Level != pending.Moves[0].Level || choice.Move != pending.Moves[0].Move || choice.ReplaceSlot is < 0 or > 3)
            return Fail(frame, "source_four_reward_learning_choice");
        return Execute(catalog, frame, choice);
    }

    // Reward application may project a Transform/copy view, even though the bounded action owner
    // still refuses to execute those move recipes. Its learning pause must not lose either view.
    private static string? ValidateRequest(PokemonCatalog catalog, SourceBattleFrame frame, SourceTurnContinuation continuation)
    {
        if (catalog is null || frame is null) return "source_four_reward_input";
        if (SourceBattleIdentityRules.Validate(frame) is { } error) return error;
        if (frame.FourTurn is not { Phase: SourceFourActionPhase.RewardsPending } cursor ||
            continuation != new SourceTurnContinuation(frame.Revision, frame.EventSequence, frame.NextAction, cursor.Phase))
            return "source_four_reward_stale_continuation";
        if (SourceMechanicsCatalog.For(catalog) is null || frame.Rewards?.Context is null)
            return "source_four_reward_context_required";
        if (frame.Rewards.Pending is { } pending)
        {
            var pokemon = frame.Party(BattleSide.Player).Members[pending.Recipient.Index].Pokemon;
            if (!catalog.Species.TryGetValue(pokemon.Species, out var species) || pending.Moves.Any(m =>
                !species.LevelUpMoves.Any(entry => entry.Level == m.Level && entry.Move == m.Move)))
                return "source_four_reward_learning_provenance";
        }
        if (frame.BattleKind != WildBattleCalledBattleKind.Trainer || frame.Rewards.Context.BattleKind != SourceRewardBattleKind.OrdinaryTrainer)
            return "source_four_reward_context_battle_kind";
        return null;
    }

    private static SourceFourActionResult Execute(PokemonCatalog catalog, SourceBattleFrame frame, SourceRewardLearningChoice? choice)
    {
        try
        {
            var next = frame;
            var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            int fromIndex = 0;
            if (choice is not null)
            {
                var pending = next.Rewards!.Pending!;
                var member = next.Party(BattleSide.Player).Members[pending.Recipient.Index];
                var moves = member.Pokemon.Moves;
                if (choice.ReplaceSlot is { } slot)
                {
                    moves = PokemonMoveRules.Replace(catalog, moves, slot, choice.Move);
                    Project(pending.Recipient, member.Pokemon with { Moves = moves });
                }
                Publish((n, t) => new SourceRewardLearningEvent(n, t, pending.Recipient, pending.Moves[0],
                    choice.ReplaceSlot is null ? SourceRewardLearningOutcome.Declined : SourceRewardLearningOutcome.Replaced, choice.ReplaceSlot));
                var remaining = pending.Moves.RemoveAt(0);
                while (!remaining.IsEmpty)
                {
                    var candidate = remaining[0];
                    var learning = PokemonMoveRules.TryLearn(catalog, moves, candidate.Move);
                    if (learning.Outcome == MoveLearningOutcome.NeedsReplacement) break;
                    moves = learning.Moves;
                    member = next.Party(BattleSide.Player).Members[pending.Recipient.Index];
                    Project(pending.Recipient, member.Pokemon with { Moves = moves });
                    Publish((n, t) => new SourceRewardLearningEvent(n, t, pending.Recipient, candidate,
                        learning.Outcome == MoveLearningOutcome.Added ? SourceRewardLearningOutcome.Added : SourceRewardLearningOutcome.AlreadyKnown,
                        learning.Outcome == MoveLearningOutcome.Added ? moves.Length - 1 : null));
                    remaining = remaining.RemoveAt(0);
                }
                next = next with { Rewards = next.Rewards! with { Pending = remaining.IsEmpty ? null : pending with { Moves = remaining } } };
                if (!remaining.IsEmpty) return Commit();
                fromIndex = pending.NextPartyIndex;
            }

            var defeat = next.FourTurn!.Defeats.First(d => d.RequiresExperience);
            var rewards = next.Rewards!;
            var participants = Enumerable.Range(0, next.Party(BattleSide.Player).Members.Length)
                .Where(i => (rewards.Participation[defeat.Occupant.Member.Index] & (1 << i)) != 0)
                .Select(i => new PartyMemberKey(BattleSide.Player, i)).ToImmutableArray();
            // Recalculation after a learning pause reads current permanent state. Earlier recipients
            // are skipped, and remain in the source living denominators even after reaching level100.
            var calculation = SourceVictoryRewards.Calculate(SourceMechanicsCatalog.For(catalog)!, next.Party(BattleSide.Player),
                rewards.Metadata, new(defeat.Occupant, next.Get(defeat.Occupant.Slot).Body!, participants), rewards.Context!);
            foreach (var recipient in calculation.Members.Where(m => m.Key.Index >= fromIndex))
            {
                Project(recipient.Key, recipient.Reward.Pokemon);
                Publish((n, t) => new SourceRewardAppliedEvent(n, t, defeat.Occupant, recipient.Key, recipient.Reward));
                if (!recipient.Reward.PendingMoves.IsEmpty)
                {
                    next = next with { Rewards = next.Rewards! with { Pending = new(defeat.Occupant, defeat.FaintEventSequence,
                        recipient.Key.Index + 1, recipient.Key, recipient.Reward.PendingMoves) } };
                    return Commit();
                }
            }
            next = next with { Rewards = next.Rewards! with { CompletedOpponentMask = (byte)(next.Rewards.CompletedOpponentMask | (1 << defeat.Occupant.Member.Index)) },
                FourTurn = next.FourTurn! with { Defeats = next.FourTurn.Defeats.Remove(defeat) } };
            WildBattleOutcome? completedOutcome = null;
            if (!SourceFourActionTurns.HasPendingRewards(next))
            {
                if (next.FourTurn!.OutcomeAfterRewards is { } outcome)
                {
                    next = next with { Outcome = outcome, FourTurn = next.FourTurn with { OutcomeAfterRewards = null, Phase = SourceFourActionPhase.BattleEnded } };
                    completedOutcome = outcome;
                }
                else next = next with { FourTurn = next.FourTurn! with {
                    Phase = next.FourTurn.EndTurn is not null ? SourceFourActionPhase.EndTurnResume :
                        next.NextAction == next.Queue.Length ? SourceFourActionPhase.EndTurn : SourceFourActionPhase.Actions } };
            }
            Publish((n, t) => new SourceOpponentRewardCompletedEvent(n, t, defeat.Occupant));
            if (completedOutcome is { } finalOutcome) Publish((n, t) => new SourceEndedEvent(n, t, finalOutcome));
            return Commit();

            void Publish(Func<long, int, SourceBattleEvent> create)
            {
                var result = SourceBattleEventHooks.Publish(next, create); next = result.State; events.Add(result.Event);
            }
            void Project(PartyMemberKey key, PokemonInstance pokemon)
            {
                var party = next.Party(key.Side); var member = party.Members[key.Index];
                next = next with { Parties = next.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member with { Pokemon = pokemon }) }),
                    Slots = next.Slots.Select(c => c.Member == key ? c with { Body = WildBattleTransformationRules.ApplyPersistentPokemon(c.Body!, pokemon) } : c).ToImmutableArray() };
            }
            SourceFourActionResult Commit()
            {
                next = next with { Revision = checked(frame.Revision + 1) };
                if (SourceBattleIdentityRules.Validate(next) is { } invalid) return Fail(frame, invalid);
                return new(next, events.ToImmutable(), [], null);
            }
        }
        catch (OverflowException) { return Fail(frame, "source_four_reward_counter_exhausted"); }
        catch (ArgumentException ex) { return Fail(frame, ex.Message); }
        catch (NotSupportedException ex) { return Fail(frame, ex.Message); }
        catch (InvalidOperationException ex) { return Fail(frame, ex.Message); }
    }

    private static SourceFourActionResult Fail(SourceBattleFrame frame, string error) => new(frame, [], [], error);
}
