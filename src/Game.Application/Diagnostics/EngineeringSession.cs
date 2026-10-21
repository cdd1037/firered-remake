using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.World;
namespace Game.Application;
// Synthetic integration harness, not FireRed story content. It proves module and save boundaries.
public sealed record EngineeringState(WorldSnapshot World, BattleSnapshot? Battle, ImmutableArray<string> Rewards, int Money, string Message);
public sealed record EngineeringCommand(string Kind, Direction Direction = Direction.South);
public sealed class EngineeringSession
{
    public const string Rules = "engineering-harness-v1";
    public const string Content = "original-fixture-v1";
    private readonly IWorldMap _map;
    private readonly IBattleContent _battleContent;
    private readonly BattleSnapshot _battleFixture;
    private readonly CommandSession<EngineeringState, EngineeringCommand> _commands;
    public EngineeringState State => _commands.State;
    public long Revision => _commands.Revision;
    public EngineeringSession(IWorldMap map, IBattleContent battleContent, BattleSnapshot battleFixture, EngineeringState? restored = null)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(battleContent); ArgumentNullException.ThrowIfNull(battleFixture);
        _map = map; _battleContent = battleContent; _battleFixture = battleFixture;
        var initial = restored ?? new EngineeringState(new(RuleVersions.GridWorldV0, 0, 0, new(map.Id, 2, 2, 0, Direction.South), new RngState(1)), null, ImmutableArray<string>.Empty, 0, "原创工程夹具，非火红游戏内容");
        var error = ValidateState(initial, map, battleContent);
        if (error is not null) throw new InvalidDataException(error);
        _commands = new(initial, Rules, Content);
    }
    public static string? ValidatePlayerSave(EngineeringState state, IWorldMap map, IBattleContent battleContent) =>
        state.Battle is not null ? "save_battle_active" : ValidateState(state, map, battleContent);
    // Internal replay/checkpoint validation can accept a stable battle snapshot; player save policy cannot.
    public static string? ValidateState(EngineeringState state, IWorldMap map, IBattleContent battleContent)
    {
        if (state.World is null) return "harness_world_missing";
        var worldError = WorldRules.ValidateState(state.World, map);
        if (worldError is not null) return worldError.Code;
        if (state.Message is null || state.Money < 0 || state.Rewards.IsDefault || state.Rewards.Any(x => x != "fixture:one-time") || !state.Rewards.SequenceEqual(state.Rewards.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))) return "harness_inventory_invalid";
        if (state.Battle is {} b)
        {
            if (b.ContentVersion != Content) return "harness_content_mismatch";
            var battleError = BattleRules.ValidateState(b, battleContent);
            if (battleError is not null) return battleError.Code;
            if (b.Phase != BattlePhase.AwaitCommands || !b.PendingMoves.IsEmpty) return "harness_save_not_stable";
        }
        return null;
    }
    public CommandOutcome<EngineeringState> Execute(string commandId, long expectedRevision, EngineeringCommand command) =>
        _commands.Execute(new(commandId, expectedRevision, Rules, Content, command), Decide);
    private Decision<EngineeringState> Decide(EngineeringState state, EngineeringCommand command)
    {
        switch (command.Kind)
        {
            case "move":
                if (state.Battle is not null) return new(CommandStatus.Rejected, state, "battle_active");
                var moved = WorldRules.Move(state.World, new(new("grid:" + state.World.Revision), state.World.Revision, command.Direction), _map);
                return new(Convert(moved.Status), moved.Status == TransitionStatus.Accepted ? state with { World = moved.State, Message = moved.Events[0].Kind.ToString() } : state, moved.Diagnostic?.Code);
            case "reward":
                if (state.Battle is not null) return new(CommandStatus.Rejected, state, "battle_active");
                if (state.Rewards.Contains("fixture:one-time")) return new(CommandStatus.Accepted, state with { Message = "奖励已领取，不会重复发放" });
                return new(CommandStatus.Accepted, state with { Rewards = state.Rewards.Add("fixture:one-time").Order(StringComparer.Ordinal).ToImmutableArray(), Money = checked(state.Money + 100), Message = "获得100测试币" });
            case "battle":
                if (state.Battle is not null) return new(CommandStatus.Rejected, state, "battle_active");
                return new(CommandStatus.Accepted, state with { Battle = _battleFixture, Message = "普通伤害单打规则夹具" });
            case "attack":
                if (state.Battle is null) return new(CommandStatus.Rejected, state, "no_battle");
                var current = state.Battle;
                var player = current.Battlers.Single(x => x.Side == BattleSide.Player);
                var opponent = current.Battlers.Single(x => x.Side == BattleSide.Opponent);
                var chosen = BattleRules.Apply(current, new(new("player:" + current.Revision), current.Revision, player.Id, 0, opponent.Id), _battleContent);
                if (chosen.Status != TransitionStatus.Accepted) return new(Convert(chosen.Status), state, chosen.Diagnostic?.Code);
                var resolved = BattleRules.Apply(chosen.State, new(new("opponent:" + chosen.State.Revision), chosen.State.Revision, opponent.Id, 0, player.Id), _battleContent);
                if (resolved.Status != TransitionStatus.Accepted) return new(Convert(resolved.Status), state, resolved.Diagnostic?.Code);
                var ended = resolved.State.Phase == BattlePhase.Ended;
                var message = ended ? resolved.State.Outcome.ToString() : "回合" + (resolved.State.Turn - 1) + "已结算";
                return new(CommandStatus.Accepted, state with { Battle = ended ? null : resolved.State, Message = message });
            default: return new(CommandStatus.Rejected, state, "unknown_command");
        }
    }
    private static CommandStatus Convert(TransitionStatus status) => status switch { TransitionStatus.Accepted => CommandStatus.Accepted, TransitionStatus.Rejected => CommandStatus.Rejected, _ => CommandStatus.Fault };
}
