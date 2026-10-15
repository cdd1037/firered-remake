using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CurrentPokemonLifecycleTests : WildBattleFixture
{
    private void Inject(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private static IEnumerable<(PartyPokemon Mon, PokemonStorageMoveLocation Location)> Located(CampaignState state) =>
        state.Party.Select((p, i) => (p, PokemonStorageMoveLocation.Party(i))).Concat(
            state.Storage!.Boxes.SelectMany((b, bi) => b.Slots.Select((p, si) => (p, bi, si)))
                .Where(x => x.p is not null).Select(x => (x.p!, PokemonStorageMoveLocation.Box(x.bi, x.si))));

    // The PC callback context is controlled; release confirmation and mutation use public commands.
    private void Release(CampaignSession session, long id)
    {
        var stable = session.State;
        var target = Located(stable).Single(p => p.Mon.InstanceId == id);
        Inject(session, stable with { Pc = new(CampaignPcView.Move), PcPoweredOn = true,
            Script = stable.Script with { Wait = new(StoryWaitKind.Presentation, "lifecycle-pc") } });
        if (!target.Location.IsParty) Send(session, new("storage_box", Choice: target.Location.BoxIndex));
        Send(session, new("storage_release") { StorageLocation = target.Location, TargetInstanceId = id });
        var pending = session.State;
        var rejected = session.Execute("wrong-release:" + commands++, session.Revision,
            new("storage_release_prompt", Choice: 0) { StorageLocation = target.Location, TargetInstanceId = id + 1 });
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreSame(pending, session.State);
        Send(session, new("storage_release_prompt", Choice: 0) { StorageLocation = target.Location, TargetInstanceId = id });
        Inject(session, session.State with { Pc = null, PcPoweredOn = false, Script = stable.Script });
        Assert.IsFalse(Located(session.State).Any(p => p.Mon.InstanceId == id));
        Assert.IsFalse((session.State.Exploration!.Captured ?? []).Any(p => p.InstanceId == id));
        Assert.IsFalse((session.State.EvolutionHistory ?? []).Any(p => p.InstanceId == id));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void PublicCaptureReleaseCyclesKeepAllocatorAndDexWithoutRetiredEntities()
    {
        var session = StartField();
        int sources = session.State.Exploration!.Captured!.Value.Length;
        int histories = session.State.Exploration.Battles.Length;
        long next = session.State.NextPokemonId;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            for (int attempt = 0; attempt < 20 && session.State.WildBattle is not null; attempt++)
            {
                Send(session, new("battle_ball", Text: "pret:ITEM_ULTRA_BALL"));
                Drain(session);
            }
            Assert.IsNull(session.State.WildBattle);
            long acquired = next + cycle;
            Assert.AreEqual(acquired + 1, session.State.NextPokemonId);
            var mon = Located(session.State).Single(p => p.Mon.InstanceId == acquired).Mon;
            var dex = session.State.CaughtSpecies;
            var inventory = CanonicalJson.Hash(session.State.Inventory);
            Release(session, acquired);
            Assert.AreEqual(acquired + 1, session.State.NextPokemonId);
            Assert.AreSame(dex, session.State.CaughtSpecies);
            Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
            Assert.IsTrue(session.State.CaughtSpecies.Contains(mon.Pokemon.SourceSpeciesId));
            Assert.AreEqual(sources, session.State.Exploration!.Captured!.Value.Length);
            Assert.IsTrue(session.State.Exploration.Battles.Length <= histories + 1);
            if (cycle == 2) break;
            var state = session.State;
            var field = FieldFixture();
            var prepared = state with { World = field.World with { Rng = state.World.Rng },
                Exploration = state.Exploration with { EarlyEncounter = field.Exploration!.EarlyEncounter } };
            session = CampaignSession.RestoreCurrent(content, prepared, session.Revision);
            Send(session, new("turn", Direction: Direction.North));
            Drain(session);
            Assert.IsNotNull(session.State.WildBattle);
        }
        string json = JsonSerializer.Serialize(session.State);
        Assert.IsFalse(json.Contains("ReleasedPokemon", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("RibbonRecipients", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PublicCaptureEvolutionReleasePreservesActualEvolutionStatistic()
    {
        var session = StartField();
        long id = session.State.NextPokemonId;
        for (int attempt = 0; attempt < 20 && session.State.WildBattle is not null; attempt++)
        {
            Send(session, new("battle_ball", Text: "pret:ITEM_ULTRA_BALL"));
            Drain(session);
        }
        var caught = Located(session.State).Single(p => p.Mon.InstanceId == id);
        int species = caught.Mon.Pokemon.SourceSpeciesId;
        Assert.IsTrue(species is 16 or 19);
        var state = session.State;
        if (!caught.Location.IsParty)
        {
            var deposited = PokemonStorageRules.Deposit(content.PokemonContent, state.Party, state.Storage!, 0, 13);
            Assert.IsTrue(deposited.Succeeded);
            var withdrawn = PokemonStorageRules.Withdraw(content.PokemonContent, deposited.Party, deposited.Storage,
                caught.Location.BoxIndex, caught.Location.SlotIndex);
            Assert.IsTrue(withdrawn.Succeeded);
            state = state with { Party = withdrawn.Party, Storage = withdrawn.Storage };
        }
        var candy = new Game.Domain.Ids.ItemId("pret:ITEM_RARE_CANDY");
        state = state with { Inventory = state.Inventory! with
            { BagItems = state.Inventory.BagItems.RemoveAll(s => s.Item == candy).Add(new(candy, 20)) } };
        session = CampaignSession.RestoreCurrent(content, state, session.Revision);
        int slot = Array.FindIndex(state.Party.ToArray(), p => p.InstanceId == id);
        int previous = state.GameStats!.GetValueOrDefault(CampaignSession.EvolutionCountStat);
        Send(session, new("bag_open"));
        for (int level = 0; level < 20 && session.State.Party[slot].Pokemon.SourceSpeciesId == species; level++)
        {
            Send(session, new("field_rare_candy", Text: candy.Value, PartyIndex: slot));
            for (int i = 0; i < 40 && (session.State.RareCandy is not null || session.State.Evolution is not null); i++)
                Send(session, session.State.Evolution is { Stage: CampaignEvolutionStage.Proposal } ? new("evolution_choice", Choice: 1)
                    : session.State.Evolution is { Stage: CampaignEvolutionStage.LearnMove } ? new("evolution_learn", MoveSlot: -1)
                    : session.State.RareCandy is { Stage: CampaignRareCandyStage.LearnMove } ? new("rare_candy_learn", MoveSlot: -1) : new("ack"));
        }
        Send(session, new("menu_back"));
        Assert.AreNotEqual(species, session.State.Party[slot].Pokemon.SourceSpeciesId);
        Assert.AreEqual(previous + 1, session.State.GameStats![CampaignSession.EvolutionCountStat]);
        Assert.IsTrue(session.State.EvolutionHistory!.Value.Any(e => e.InstanceId == id));
        StrictRoundtrip(session);
        Release(session, id);
        Assert.AreEqual(previous + 1, session.State.GameStats![CampaignSession.EvolutionCountStat]);
    }

    [TestMethod]
    public void StarterAndEvolvedIndividualsCanLeaveWithoutRewindingFacts()
    {
        var session = CampaignSession.RestoreCurrent(content, checkpoint.State, checkpoint.Generation);
        long evolved = session.State.EvolutionHistory!.Value.Select(e => e.InstanceId).First(id => id != 1 &&
            Located(session.State).Any(p => p.Mon.InstanceId == id && p.Mon.Mail == SourceMailState.NoMail &&
                !p.Mon.Pokemon.Moves.Any(m => m.Move.Value is "pret:MOVE_SURF" or "pret:MOVE_DIVE")));
        string stats = CanonicalJson.Hash(session.State.GameStats), hall = CanonicalJson.Hash(session.State.HallOfFame);
        Release(session, evolved);
        Release(session, 1);
        Assert.AreEqual(stats, CanonicalJson.Hash(session.State.GameStats));
        Assert.AreEqual(hall, CanonicalJson.Hash(session.State.HallOfFame));
        Assert.IsTrue(session.State.Script.Flags["FLAG_SYS_POKEMON_GET"]);
    }

    [TestMethod]
    [DataRow(9L)] // Eevee, now evolved
    [DataRow(60L)] // Fighting Dojo
    [DataRow(61L)] // fossil
    [DataRow(68L)] // hatched Togepi
    [DataRow(57L)] // caught Mewtwo
    [DataRow(59L)] // caught Zapdos
    public void FiniteRewardsAndSpecialCapturesRemainCompletedAfterPublicRelease(long id)
    {
        var session = CampaignSession.RestoreCurrent(content, checkpoint.State, checkpoint.Generation);
        if (id == 68)
        {
            // Seed the existing gift egg at its final walking cycle, then use the
            // production walking transaction and all public hatch callbacks.
            int slot = Array.FindIndex(session.State.Party.ToArray(), p => p.InstanceId == id);
            var egg = session.State.Party[slot];
            var ready = session.State with { Party = session.State.Party.SetItem(slot,
                egg with { Pokemon = egg.Pokemon with { Friendship = 0 } }),
                FourIslandDayCare = (session.State.FourIslandDayCare ?? FourIslandDayCareState.Empty) with { HatchStepCounter = 254 } };
            var started = (CampaignState)typeof(CampaignSession).GetMethod("ApplyDayCareWalkingStep", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(validator, [ready])!;
            Inject(session, started);
            Assert.IsNotNull(session.State.Dialogue);
            for (int n = 0; n < 20 && (!session.State.Script.Completed || session.State.EggHatch is not null); n++)
                Send(session, new("ack", Choice: session.State.EggHatch?.Stage == CampaignEggHatchStage.NicknameChoice ? 0 : null));
            Assert.IsNull(session.State.EggHatch);
            Assert.IsFalse(session.State.Party[slot].IsEgg);
            StrictRoundtrip(session);
        }
        var flags = session.State.Script.Flags;
        var dex = session.State.CaughtSpecies;
        Release(session, id);
        Assert.AreSame(flags, session.State.Script.Flags);
        Assert.AreSame(dex, session.State.CaughtSpecies);
        Assert.AreEqual(checkpoint.State.NextPokemonId, session.State.NextPokemonId);
        Assert.IsTrue(session.State.Exploration!.Battles.Where(b => b.SourceSpeciesId is 150 or 145)
            .Any(b => b.Outcome == WildBattleOutcome.Caught));
    }

    [TestMethod]
    public void ThousandsOfDepartedSourcesCollapseWithoutNewLedgerOrAllocatorReconstruction()
    {
        var original = checkpoint.State;
        var template = original.Exploration!.Captured!.Value[0];
        var captures = Enumerable.Range(0, 5000).Select(i => template with
            { InstanceId = 10000 + i, BattleId = "wild-battle:" + (10000 + i) }).ToImmutableArray();
        var histories = captures.Select(c => new CampaignWildHistory(c.BattleId, c.SourceSpeciesId,
            c.MetLevel, WildBattleOutcome.Caught, 0)).ToImmutableArray();
        var input = original with { NextPokemonId = 20000, Exploration = original.Exploration with
            { Captured = captures, Battles = histories, NextWildBattleId = 15000 },
            EvolutionHistory = captures.Select(c => new CampaignEvolutionHistory(c.InstanceId, null, 1, 2, 16, 16)).ToImmutableArray() };
        var compact = (CampaignState)typeof(CampaignSession).GetMethod("CompactCurrentPokemonSources", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(validator, [input])!;
        Assert.AreEqual(0, compact.Exploration!.Captured!.Value.Length);
        Assert.AreEqual(0, compact.EvolutionHistory!.Value.Length);
        Assert.AreEqual(1, compact.Exploration.Battles.Length);
        Assert.AreEqual(20000L, compact.NextPokemonId);
        Assert.AreSame(input.CaughtSpecies, compact.CaughtSpecies);
        Assert.AreSame(input.GameStats, compact.GameStats);
        Assert.AreEqual(5000, input.Exploration.Captured!.Value.Length);
    }

    [TestMethod]
    public void CurrentIdentityBoundsAndStrictVersionBoundaryRemainEnforced()
    {
        var state = checkpoint.State;
        foreach (long id in new[] { 0L, -1L, state.NextPokemonId, state.Party[1].InstanceId })
            Assert.IsNotNull(validator.ValidatePlayerSave(state with { Party = state.Party.SetItem(0, state.Party[0] with { InstanceId = id }) }));
        var old = SaveCodec.Encode(state, checkpoint.Generation, "firered-current-campaign-v2", content.ContentVersion);
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(old, validator.RulesVersion, content.ContentVersion));
        var payload = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(state))!.AsObject();
        payload["ReleasedPokemon"] = new System.Text.Json.Nodes.JsonArray();
        var forbidden = SaveCodec.Encode(payload, checkpoint.Generation, validator.RulesVersion, content.ContentVersion);
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(forbidden, validator.RulesVersion, content.ContentVersion));
        var fresh = CampaignSession.CreateCurrent(content);
        var bytes = SaveCodec.Encode(fresh.State, 0, fresh.RulesVersion, content.ContentVersion);
        var restored = SaveCodec.Decode<CampaignState>(bytes, fresh.RulesVersion, content.ContentVersion, validate: fresh.ValidatePlayerSave);
        Assert.AreEqual(1L, restored.State.NextPokemonId);
    }
}
