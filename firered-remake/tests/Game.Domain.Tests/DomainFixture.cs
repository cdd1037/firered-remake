using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Battle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Domain;
[TestClass]
public abstract class DomainFixture
{
    private protected global::Game.Domain.Battle.MoveDefinition fixtureMove = default !;
    private protected Catalog catalog = default !;
    [TestInitialize]
    public void SetUp()
    {
        fixtureMove = new MoveDefinition(new("fixture/tackle"), ElementType.Normal, 40, 100, 35);
        catalog = new Catalog(fixtureMove);
    }

    private protected string Json<T>(T x) => JsonSerializer.Serialize(x);
    private protected CombatantSnapshot Combatant(string id, BattleSide side, int speed = 20, int hp = 100) => new(new(id), new("fixture/species"), side, 5, new(100, 50, 50, 30, 30, speed), ElementType.Normal, null, hp, [new(fixtureMove.Id, 35, 35)], new("fixture/trainer"));
    private protected BattleSnapshot Battle(uint seed = 0) => BattleSnapshot.Create(new("fixture/battle"), "fixture-content-v1", Combatant("player", BattleSide.Player), Combatant("opponent", BattleSide.Opponent, 10), seed);
    private protected UseMove Player(BattleSnapshot s, string id = "cmd/player") => new(new(id), s.Revision, new("player"), 0, new("opponent"));
    private protected UseMove Opponent(BattleSnapshot s, string id = "cmd/opponent") => new(new(id), s.Revision, new("opponent"), 0, new("player"));
    private protected BattleTransition Turn(BattleSnapshot s, IBattleContent? content = null, string prefix = "cmd")
    {
        var a = BattleRules.Apply(s, Player(s, prefix + "/player"), content ?? catalog);
        Assert.AreEqual(TransitionStatus.Accepted, a.Status);
        return BattleRules.Apply(a.State, Opponent(a.State, prefix + "/opponent"), content ?? catalog);
    }

    private protected void Unchanged(BattleSnapshot before, BattleTransition after, TransitionStatus status, string code)
    {
        Assert.AreEqual(status, after.Status);
        Assert.IsTrue(ReferenceEquals(before, after.State), "Failure must return the original snapshot reference.");
        Assert.AreEqual(code, after.Diagnostic!.Code);
        Assert.AreEqual(0, after.Events.Length);
        Assert.AreEqual(0, after.RandomTrace.Length);
    }

    private protected DamageInput Damage(ElementType type = ElementType.Normal) => new(50, 40, type, 100, 100, type, null, ElementType.Normal, null, false, 100);
    private protected GridMap Map() => new(new("fixture/map"), 3, 3, Enumerable.Repeat(new TileCell(false), 9).ToImmutableArray().SetItem(5, new(true)));
    private protected WorldSnapshot World() => new(RuleVersions.GridWorldV0, 0, 0, new(new("fixture/map"), 1, 1, 0, Direction.South), new(99));
    private protected MoveIntent Walk(WorldSnapshot s, Direction d) => new(new("cmd/walk"), s.Revision, d);
}
