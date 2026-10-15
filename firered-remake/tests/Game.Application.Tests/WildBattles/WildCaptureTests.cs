using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.WildBattles;
[TestClass, TestCategory("Game")]
public sealed class WildCaptureTests : WildBattleFixture
{

    [TestMethod]
    public void public_capture_exact_instance_nickname_and_PC_delivery()
    {
        var session = StartField();
        var b = session.State.WildBattle!;
        var enemy = b.Frame!.Party(BattleSide.Opponent).Members[0];
        long id = session.State.NextPokemonId;
        int count = session.State.Party.Length;
        for (int attempt = 0; attempt < 20 && session.State.WildBattle is { } live; attempt++)
        {
            enemy = live.Frame!.Party(BattleSide.Opponent).Members[0];
            Send(session, new("battle_ball", Text: "pret:ITEM_ULTRA_BALL"));
            Drain(session, stopCapture: true);
            if (session.State.WildBattle?.Capture is { } pending)
            {
                Assert.IsTrue(CanonicalJson.Hash(pending.Pokemon) == CanonicalJson.Hash(enemy.Pokemon), "capture pending retains exact persistent enemy before storage");
                Drain(session, nickname: true);
            }
        }

        Assert.IsTrue(session.State.WildBattle is null && session.State.NextPokemonId == id + 1, "capture commits one identity");
        var all = session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(box => box.Slots).OfType<PartyPokemon>());
        var captured = all.Single(p => p.InstanceId == id);
        Assert.IsTrue(captured.Nickname == "CANON" && captured.Pokemon.Personality == enemy.Pokemon.Personality && captured.Pokemon.Ivs == enemy.Pokemon.Ivs && captured.Pokemon.Species == enemy.Pokemon.Species && captured.Pokemon.Experience == enemy.Pokemon.Experience, "capture preserves identity and chosen nickname");
        Assert.IsTrue(captured.HeldItem == WildBattleSourcePartyEffects.PersistentHeldItem(enemy) && captured.Pokemon.CurrentHp == captured.Pokemon.Stats.MaxHp && captured.Status == 0 && captured.Pokemon.Moves.All(m => m.CurrentPp == m.MaximumPp), "existing PC receive heals HP PP status and retains held item");
        Assert.IsTrue(session.State.Party.Length == (count == 6 ? 6 : count + 1), "capture destination capacity");
        Assert.IsTrue(session.State.Exploration!.Battles[^1].ExperienceGained == 0 && session.State.Exploration.Battles[^1].Outcome == WildBattleOutcome.Caught, "capture earns zero EXP");
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void public_Celio_capture_and_PC_delivery_retain_typed_provenance()
    {
        var fixture = CelioHistoryRetentionTests.WithPriorSettlements(FieldFixture(), 2);
        var rng = fixture.World.Rng;
        var party = (ImmutableArray<WildBattleCombatant>)typeof(CampaignSession).GetMethod("SourceTrainerEntryParty", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(validator, [fixture])!;
        int active = Enumerable.Range(0, party.Length).First(i => party[i].Pokemon.CurrentHp > 0);
        bool found = false;
        var caughtSpecies = catalog.Species.Values.Where(p => fixture.CaughtSpecies.Contains(p.SourceSpeciesId)).Select(p => p.Id).ToImmutableHashSet();
        for (int n = 0; n < 20000; n++, rng = Gen3Rng.Next(rng, "fixture.capture-search").State)
        {
            var gate = Gen3Rng.Next(rng, "fixture.roamer-gate", 4);
            if (gate.Value % 4 != 0)
                continue;
            var entry = SourceWildBattlePolicy.StartCelioRoamer(ownership, fixture.Roamer!.Individual!, fixture.TrainerId, party, active, gate.State, WildBattleTerrain.Grass);
            if (!entry.Succeeded)
                throw new InvalidDataException(entry.Fault);
            var frame = entry.State!;
            var choice = new SourceActionSelection(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Capture)
            {
                Capture = new(CaptureBall.Ultra, new(false, caughtSpecies, CaptureEncounterKind.Roamer))
            };
            var action = SourceWildBattlePolicy.BeginAction(catalog, frame, frame.Revision, choice);
            if (!action.Succeeded)
                throw new InvalidDataException(action.Fault);
            for (int step = 0; step < 5 && action.State.Outcome == WildBattleOutcome.Ongoing; step++)
            {
                action = SourceFourActionTurns.Advance(catalog, action.State, action.Continuation!.Value);
                if (!action.Succeeded)
                    throw new InvalidDataException(action.Fault);
            }

            if (action.State.Outcome == WildBattleOutcome.Caught)
            {
                found = true;
                break;
            }
        }

        Assert.IsTrue(found, "bounded canonical capture seed search");
        fixture = fixture with
        {
            World = fixture.World with
            {
                Rng = rng
            },
            Roamer = fixture.Roamer!with
            {
                Runtime = fixture.Roamer.Runtime with
                {
                    Location = content.CelioRoamerMaps![fixture.World.Player.MapId]
                }
            }
        };
        var session = CampaignSession.RestoreCurrent(content, fixture, checkpoint.Generation);
        Send(session, new("turn", Direction: Direction.North));
        Drain(session);
        Send(session, new("battle_ball", Text: "pret:ITEM_ULTRA_BALL"));
        Drain(session, nickname: true);
        Assert.IsTrue(session.State.WildBattle is null && !session.State.Roamer!.Individual!.Active, "actual public capture deactivates roamer");
        var capture = session.State.Exploration!.Captured!.Value[^1];
        Assert.IsTrue(capture.CelioRoamer?.Individual.Personality == fixture.Roamer!.Individual!.Personality && session.State.Exploration.Battles[^1].CelioRoamer?.Battle.Outcome == RoamerBattleOutcome.Caught, "capture and terminal ledger preserve typed Celio receipt");
        Assert.AreEqual(1, session.State.Exploration!.Battles.Count(h => h.CelioRoamer is not null), "prior roaming settlements were reduced after the public battle");
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void Snorlax_source_capture_clears_actor_and_resumes_source_outcome()
    {
        var session = Scene("map.route-12", "Route12_EventScript_Snorlax");
        var battle = session.State.WildBattle!;
        Assert.IsTrue(battle.ScriptedSnorlaxRoute == 12 && battle.Frame!.WildEncounter!.ScriptedSnorlax, "Snorlax source policy retained");
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL"));
        Drain(session);
        Assert.IsTrue(session.State.Exploration!.Battles.Single()is { ScriptedSnorlaxRoute: 12, Outcome: WildBattleOutcome.Caught }, "Snorlax actual capture history");
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_ROUTE_12_SNORLAX") && !session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_SPECIAL_WILD_BATTLE"), "source OnResume clears special battle flag and hides Snorlax");
        Assert.IsTrue(session.State.Actors.Values.Where(a => a.ScriptId == "Route12_EventScript_Snorlax").All(a => !a.Active), "source Snorlax actor removed after capture");
    }
}
