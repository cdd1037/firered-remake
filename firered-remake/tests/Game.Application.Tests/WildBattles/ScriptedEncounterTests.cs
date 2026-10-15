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
public sealed class ScriptedEncounterTests : WildBattleFixture
{

    [TestMethod]
    public void public_Celio_entry_and_source_flee_settlement_survive_Continue()
    {
        var fixture = FieldFixture();
        var rng = fixture.World.Rng;
        while (Gen3Rng.Next(rng, "fixture.roamer-gate", 4).Value % 4 != 0)
            rng = Gen3Rng.Next(rng, "fixture.advance").State;
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
        Assert.IsTrue(validator.ValidatePlayerSave(fixture)is null, "current Celio field fixture");
        var session = CampaignSession.RestoreCurrent(content, fixture, checkpoint.Generation);
        Send(session, new("turn", Direction: Direction.North));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle?.Frame?.WildEncounter?.CelioRoamer?.Individual == fixture.Roamer!.Individual, "public encounter retains actual roamer identity");
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null && session.State.Exploration!.Battles[^1].CelioRoamer?.Battle.Outcome == RoamerBattleOutcome.MonFled, "typed enemy flee outcome retained");
        Assert.IsTrue(session.State.Roamer!.Individual!.CurrentHp < fixture.Roamer.Individual!.CurrentHp, "roamer battle damage persisted");
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void Lostelle_exact_source_Hypno_escape_resumes_rescue_caller()
    {
        var session = Scene("map.three-island-berry-forest", FirstSeviiHypnoBattleContract.ScriptEntry);
        var battle = session.State.WildBattle!;
        Assert.IsTrue(battle.LostelleHypno is not null && battle.Frame!.WildEncounter is { LostelleHypno: true, FirstSeviiWild: true }, "Hypno typed source provenance");
        for (int i = 0; i < 10 && session.State.WildBattle is not null; i++)
        {
            Send(session, new("battle_menu", Text: "run"));
            Drain(session);
        }

        Assert.IsTrue(session.State.Exploration!.Battles.Single()is { LostelleHypno: not null, Outcome: WildBattleOutcome.Escaped }, "actual Hypno escape history");
        for (int i = 0; i < 50 && session.State.Dialogue?.TextSymbol != "ThreeIsland_BerryForest_Text_ThankYouHaveThis"; i++)
            Send(session, new("ack"));
        Assert.IsTrue(session.State.Dialogue?.TextSymbol == "ThreeIsland_BerryForest_Text_ThankYouHaveThis", "source rescue continuation survives canonical terminal");
    }

    [TestMethod]
    public void revealed_Marowak_dodges_ball_and_real_victory_resumes_Tower_source()
    {
        var session = Scene("map.pokemon-tower-6f", "PokemonTower_6F_EventScript_MarowakGhost");
        Assert.IsTrue(session.State.WildBattle!.Frame!.WildEncounter!.TowerBattleKind == PokemonTowerBattleKind.RevealedMarowak, "revealed mother stays distinct");
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL"));
        Assert.IsTrue(session.State.WildBattle!.Events.Any(e => e is WildBattleTowerBallDodgedEvent) && session.State.WildBattle.Capture is null, "source ghost ball dodge survives canonical event projection");
        Drain(session);
        for (int i = 0; i < 10 && session.State.WildBattle is not null; i++)
        {
            Send(session, new("battle_move", MoveSlot: 0));
            Drain(session);
        }

        Assert.IsTrue(session.State.Exploration!.Battles.Single()is { ScriptedMarowak: true, Outcome: WildBattleOutcome.Won }, "actual source Marowak victory");
        Assert.IsTrue(session.State.Script.Variables.GetValueOrDefault("VAR_RESULT") == 0 && session.State.WildBattle is null, "Tower source gets correct winning callback result");
    }
}
