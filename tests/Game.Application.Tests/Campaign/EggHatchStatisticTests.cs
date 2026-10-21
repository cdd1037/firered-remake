using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class EggHatchStatisticTests
{
    private const string Stat = "GAME_STAT_HATCHED_EGGS";
    private CampaignSession session = default!;
    private CampaignState initial = default!;
    private CampaignPokemonContent content = default!;

    [TestInitialize]
    public void Initialize()
    {
        session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        initial = session.State;
        content = CurrentCampaignFixture.Catalog.PokemonContent!;
    }

    private object? Invoke(string name, params object?[] args) => typeof(CampaignSession)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(session, args);

    private CampaignState Egg(bool gift)
    {
        var trainer = new SourceEggTrainer(initial.TrainerId, initial.PlayerName, initial.PlayerGender);
        PartyPokemon egg;
        CampaignState state;
        if (gift)
        {
            var source = CurrentCampaignFixture.Catalog.FindProgram("FiveIsland_WaterLabyrinth_EventScript_TryGiveEgg")!
                .Instructions.Single(i => i.Opcode == "giveegg").Source;
            var made = SourceGiftEggRules.GiveEgg(content, content.Catalog.BreedingData!, SourceGiftEggRules.Togepi, 3, trainer, 4, initial.World.Rng);
            egg = made.Egg;
            state = initial with { GiftEggs = [new(3, source, 175, egg.Pokemon.Personality, egg.Pokemon.Ivs, initial.World.Rng, made.Rng)],
                World = initial.World with { Rng = made.Rng },
                Script = StoryState.Empty with { Flags = initial.Script.Flags.SetItem("FLAG_GOT_TOGEPI_EGG", true) } };
        }
        else
        {
            PartyPokemon Parent(long id, uint pid)
            {
                var made = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_BULBASAUR"), 5, initial.TrainerId,
                    IvPolicy.Fixed(3), PersonalityPolicy.Fixed(pid)), new RngState(0)).Pokemon;
                return new(id, made, "BULBASAUR", initial.PlayerName, initial.PlayerGender, 2, "MAPSEC_FOUR_ISLAND", 0, 4, "ITEM_POKE_BALL", "ITEM_NONE");
            }
            CampaignEggParent Receipt(PartyPokemon mon) => new(mon.InstanceId, mon.Pokemon.SourceSpeciesId, mon.Pokemon.Personality,
                mon.Pokemon.OriginalTrainerId, mon.Pokemon.Ivs, mon.Pokemon.Moves.Select(m => m.Move).ToImmutableArray(), mon.HeldItem);
            var first = Parent(1, 0); var second = Parent(2, 255);
            var made = SourceEggRules.Receive(content, content.Catalog.BreedingData!, new(new(first, 0), new(second, 0), 123, 0),
                3, trainer, "MAPSEC_FOUR_ISLAND", 4, initial.World.Rng);
            egg = made.Egg;
            state = initial with { DayCareEggs = [new(3, Receipt(first), Receipt(second), 123, egg.Pokemon.SourceSpeciesId,
                egg.Pokemon.Personality, egg.Pokemon.Ivs, "MAPSEC_FOUR_ISLAND", initial.World.Rng, made.Rng)],
                World = initial.World with { Rng = made.Rng }, Script = StoryState.Empty };
        }
        return state with { Party = [egg with { Pokemon = egg.Pokemon with { Friendship = 0 } }], NextPokemonId = 4,
            FourIslandDayCare = FourIslandDayCareState.Empty with { HatchStepCounter = 254 }, Dialogue = null,
            EntryPrograms = [], Motions = [], PresentationEffects = [], ScreenIsBlack = false };
    }

    private CampaignState Ack(CampaignState state, int? choice = null)
    {
        object?[] args = [state, new CampaignCommand("ack", Choice: choice), null];
        Assert.IsTrue((bool)Invoke("TryDecideEggHatch", args)!);
        var decision = (Decision<CampaignState>)args[2]!;
        Assert.AreEqual(CommandStatus.Accepted, decision.Status, decision.Error);
        return decision.State;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Shared_hatch_transaction_counts_once_through_animation_nickname_and_save(bool gift)
    {
        var state = Egg(gift);
        Assert.AreEqual(0, state.GameStats![Stat]);
        // Exercise the real eligible-step, source script and EggHatch native transaction.
        state = (CampaignState)Invoke("ApplyDayCareWalkingStep", state)!;
        for (int i = 0; state.EggHatch is null && i < 10; i++)
        {
            var decision = (Decision<CampaignState>)Invoke("Decide", state, new CampaignCommand("ack"))!;
            Assert.AreEqual(CommandStatus.Accepted, decision.Status, decision.Error);
            state = decision.State;
        }
        Assert.AreEqual(CampaignEggHatchStage.Animation, state.EggHatch?.Stage);
        Assert.IsFalse(state.Party[0].IsEgg);
        Assert.AreEqual(1, state.GameStats![Stat]);
        state = Ack(state);
        Assert.AreEqual(CampaignEggHatchStage.Hatched, state.EggHatch?.Stage);
        state = Ack(state);
        Assert.AreEqual(CampaignEggHatchStage.NicknameChoice, state.EggHatch?.Stage);
        state = Ack(state, 0);
        Assert.IsNull(state.EggHatch);
        Assert.AreEqual(1, state.GameStats![Stat]);
        string receiptValidator = gift ? "ValidateGiftEggReceipts" : "ValidateDayCareEggReceipts";
        string? Validate(CampaignState s) => (string?)Invoke(receiptValidator, s) ?? (string?)Invoke("ValidateHatchedEggCount", s);
        Assert.IsNull(Validate(state));
        var saved = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, 9, session.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion), session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, validate: Validate);
        var restored = saved.State;
        Assert.AreEqual(CanonicalJson.Hash(state), CanonicalJson.Hash(restored));
        restored = (CampaignState)Invoke("ApplyDayCareWalkingStep", restored)!;
        Assert.IsNull(restored.EggHatch);
        Assert.AreEqual(1, restored.GameStats![Stat]);
        Assert.AreEqual("save_hatched_egg_count", Invoke("ValidateHatchedEggCount",
            restored with { GameStats = restored.GameStats.SetItem(Stat, 0) }));
        // Pruning a departed bred receipt cannot decrement the lifetime statistic.
        var pruned = restored with { DayCareEggs = null, GiftEggs = null };
        Assert.IsNull(Invoke("ValidateHatchedEggCount", pruned));
        Assert.AreEqual(1, pruned.GameStats![Stat]);
    }

    [TestMethod]
    public void Shared_hatch_counter_saturates_at_source_24_bit_limit()
    {
        var state = Egg(true);
        state = state with { GameStats = state.GameStats!.SetItem(Stat, 0xffffff) };
        state = (CampaignState)Invoke("ApplyDayCareWalkingStep", state)!;
        for (int i = 0; state.EggHatch is null && i < 10; i++)
        {
            var decision = (Decision<CampaignState>)Invoke("Decide", state, new CampaignCommand("ack"))!;
            Assert.AreEqual(CommandStatus.Accepted, decision.Status, decision.Error);
            state = decision.State;
        }
        Assert.AreEqual(CampaignEggHatchStage.Animation, state.EggHatch?.Stage);
        Assert.IsFalse(state.Party[0].IsEgg);
        Assert.AreEqual(0xffffff, state.GameStats![Stat]);
        state = Ack(Ack(Ack(state)), 0);
        Assert.IsNull(state.EggHatch);
        Assert.AreEqual(0xffffff, state.GameStats![Stat]);
        Assert.IsNull(Invoke("ValidateGiftEggReceipts", state));
        Assert.IsNull(Invoke("ValidateHatchedEggCount", state));
    }

    [TestMethod]
    public void Missing_lifetime_total_is_rejected_without_guessing_from_retained_receipts()
    {
        var state = Egg(true);
        state = state with { GameStats = state.GameStats!.Remove(Stat) };
        Assert.AreEqual("save_hatched_egg_count_missing", Invoke("ValidateHatchedEggCount", state));
        // Explicitly remove the real new-game statistic from a controlled current
        // state. No historical player checkpoint or inferred lifetime total is used.
        var missing = session.State with { GameStats = session.State.GameStats!.Remove(Stat) };
        Assert.AreEqual("save_hatched_egg_count_missing", session.ValidatePlayerSave(missing));
        Assert.ThrowsExactly<InvalidDataException>(() => CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, missing, session.Revision));
        var currentBytes = SaveCodec.Encode(missing, session.Revision, session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion);
        var originalBytes = currentBytes.ToArray();
        var result = PlayerSaveOperations.Load(() => SaveCodec.Decode<CampaignState>(currentBytes,
            session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, validate: session.ValidatePlayerSave));
        Assert.AreEqual(SaveOperationStatus.InvalidData, result.Result.Status);
        Assert.AreEqual(SaveFileOutcome.Preserved, result.Result.Files);
        CollectionAssert.AreEqual(originalBytes, currentBytes);
    }
}
