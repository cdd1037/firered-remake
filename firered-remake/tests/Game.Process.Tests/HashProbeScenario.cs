using System.Collections.Immutable;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Rules;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Processes;
internal static class HashProbeScenario
{

    public static HashProbeResult HashProbe()
    {
        var map = new GridMap(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
        CombatantSnapshot Actor(string id, BattleSide side) => new(new(id), new("species"), side, 5, new(20, 10, 10, 10, 10, 10), ElementType.Normal, null, 20, [new(new("strike"), 35, 35)]);
        var battle = BattleSnapshot.Create(new("battle"), EngineeringSession.Content, Actor("player", BattleSide.Player), Actor("opponent", BattleSide.Opponent), 1);
        var session = new EngineeringSession(map, new ProbeBattleContent(), battle);
        var reward = session.Execute("audit:reward", 0, new("reward"));
        if (reward.Status != CommandStatus.Accepted)
            throw new InvalidOperationException("Hash probe reward failed.");
        var state = session.State;
        var bytes = SaveCodec.Encode(state, 1, EngineeringSession.Rules, EngineeringSession.Content);
        var loaded = SaveCodec.Decode<EngineeringState>(bytes, EngineeringSession.Rules, EngineeringSession.Content);
        // Set order is normalized explicitly before canonical JSON; array order is otherwise semantic.
        var ids = ImmutableHashSet.Create("reward:one", "reward:two", "reward:three", "reward:four").Order(StringComparer.Ordinal).ToImmutableArray();
        return new(Environment.ProcessId, Environment.Version.Major, CanonicalJson.Hash(state), CanonicalJson.Hash(loaded.State), CanonicalJson.Hash(ids));
    }

}
sealed record HashProbeResult(int ProcessId, int RuntimeMajor, string StateHash, string RoundTripHash, string NormalizedSetHash);
sealed class ProbeBattleContent : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => id.Value == "strike" ? new(id, ElementType.Normal, 40, 100, 35) : null;
}
