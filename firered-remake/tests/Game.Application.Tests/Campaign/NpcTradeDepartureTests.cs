using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class NpcTradeDepartureTests
{
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

    private MethodInfo Method(string name) => typeof(CampaignSession).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!;
    private string? Validate(string name, CampaignState state) => (string?)Method(name).Invoke(session, [state]);
    private PartyPokemon Pokemon(long id, SpeciesId species)
    {
        var made = PokemonFactory.Create(content.Catalog, new(species, 30, initial.TrainerId,
            IvPolicy.Fixed(12), PersonalityPolicy.Fixed(7)), initial.World.Rng).Pokemon;
        return new(id, made, content.SpeciesNames[made.SourceSpeciesId], initial.PlayerName, initial.PlayerGender,
            2, "MAPSEC_ROUTE_24", 30, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

    // Deliberately enters the authoritative script-effect boundary, not the scene's UI.
    // This isolates ownership lifetime from walking, dialogue and animation timing.
    private CampaignState Context(CampaignState state, int tradeId)
    {
        var definition = CurrentCampaignFixture.Catalog.NpcTrades!.Definitions[tradeId];
        const string actorId = "trade-test-actor";
        var actor = new CampaignActor(actorId, definition.LocalId, "", 1, 1, 0, Direction.South,
            "", definition.ScriptLabel, "", true, false, 1, 1);
        return state with
        {
            World = state.World with { Player = state.World.Player with { MapId = CurrentCampaignFixture.Catalog.NpcTradeMap(tradeId) } },
            Actors = state.Actors.SetItem(actorId, actor), LastTalked = actorId,
            Script = state.Script with { Variables = state.Script.Variables
                .SetItem("VAR_LAST_TALKED", definition.LocalId).SetItem("VAR_0x8008", tradeId)
                .SetItem("VAR_0x8004", tradeId).SetItem("VAR_0x8005", 1) }
        };
    }

    private CampaignState Effect(CampaignState state, int tradeId, string function)
    {
        var definition = CurrentCampaignFixture.Catalog.NpcTrades!.Definitions[tradeId];
        object?[] arguments = [state, new StoryEffect(0, "special", [function], new("test", definition.ScriptLabel, 1)), null];
        Assert.IsTrue((bool)Method("TryApplyNpcTradeEffect").Invoke(session, arguments)!);
        return (CampaignState)arguments[2]!;
    }

    private CampaignState Prepare(int tradeId)
    {
        var definition = CurrentCampaignFixture.Catalog.NpcTrades!.Definitions[tradeId];
        var keeper = Pokemon(1, new("pret:SPECIES_BULBASAUR"));
        var outgoing = Pokemon(2, definition.RequestedSpecies);
        var state = Context(initial with { Party = [keeper, outgoing], NextPokemonId = 3,
            Storage = PokemonStorageState.Empty,
            Exploration = new(null, 1, []) { Captured = [] }, NpcTrades = [],
            SeenSpecies = initial.SeenSpecies.Union([1, outgoing.Pokemon.SourceSpeciesId]),
            CaughtSpecies = initial.CaughtSpecies.Union([1, outgoing.Pokemon.SourceSpeciesId]),
            NpcTrade = new(tradeId, CampaignNpcTradePhase.Selected, 1, outgoing.InstanceId) }, tradeId);
        return Effect(state, tradeId, "CreateInGameTradePokemon");
    }

    private CampaignState Finish(CampaignState swapped, int tradeId)
    {
        var flag = CurrentCampaignFixture.Catalog.NpcTrades!.Definitions[tradeId].CompletionFlag;
        return swapped with { NpcTrade = null, PresentationTicks = 0,
            Script = swapped.Script with { Flags = swapped.Script.Flags.SetItem(flag, true) } };
    }

    private CampaignState ReleaseRecipient(CampaignState state)
    {
        var location = PokemonStorageMoveLocation.Party(1);
        state = state with { Pc = new(CampaignPcView.Deposit) };
        Decision<CampaignState> Release(CampaignState input, CampaignCommand command) =>
            (Decision<CampaignState>)Method("DecideStorageRelease").Invoke(session, [input, command])!;
        var recipient = state.Party[1];
        var prompt = Release(state, new("storage_release") { StorageLocation = location, TargetInstanceId = recipient.InstanceId });
        Assert.AreEqual(CommandStatus.Accepted, prompt.Status, prompt.Error);
        Assert.AreEqual(2, prompt.State.Party.Length, "The prompt must not remove the recipient.");
        var released = Release(prompt.State, new("storage_release_prompt", Choice: 0)
            { StorageLocation = location, TargetInstanceId = recipient.InstanceId });
        Assert.AreEqual(CommandStatus.Accepted, released.Status, released.Error);
        Assert.AreEqual(1, released.State.Party.Length);
        Assert.AreEqual(state.NextPokemonId, released.State.NextPokemonId);
        return released.State with { Pc = null };
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(6, true)]
    public void Authoritative_swap_cleans_outgoing_capture_and_evolution_then_recipient_can_be_released(int tradeId, bool evolved)
    {
        var prepared = Prepare(tradeId);
        var outgoing = prepared.Party[1];
        int originalSpecies = evolved ? 25 : outgoing.Pokemon.SourceSpeciesId;
        // Source rows exercise retention at the swap boundary; encounter-generation
        // validity is tested by the capture suites, rather than manufactured here.
        prepared = prepared with
        {
            Exploration = new(null, 3, [new("wild-battle:1", originalSpecies, 30, WildBattleOutcome.Caught, 0),
                new("wild-battle:2", 19, 3, WildBattleOutcome.Won, 10)])
            { Captured = [new(outgoing.InstanceId, "wild-battle:1", originalSpecies,
                outgoing.Pokemon.Personality, outgoing.Pokemon.Ivs, 30, outgoing.MetLocation)] },
            EvolutionHistory = evolved ? [new(outgoing.InstanceId, null, 25, 26, 30, 30) { Item = "ITEM_THUNDER_STONE" }] : []
        };
        var stale = prepared with { Party = prepared.Party.SetItem(1, outgoing with { Nickname = "CHANGED" }) };
        string staleHash = CanonicalJson.Hash(stale);
        var rejected = Assert.ThrowsExactly<TargetInvocationException>(() => Effect(stale, tradeId, "DoInGameTradeScene"));
        Assert.AreEqual("npc_trade_swap_context", rejected.InnerException!.Message);
        Assert.AreEqual(staleHash, CanonicalJson.Hash(stale), "Rejected swaps must retain all current-source rows.");
        Assert.AreEqual(1, stale.Exploration!.Captured!.Value.Length);
        string before = CanonicalJson.Hash(prepared);
        var swapped = Effect(prepared, tradeId, "DoInGameTradeScene");
        Assert.AreEqual(before, CanonicalJson.Hash(prepared), "The source transaction input is immutable.");
        Assert.AreEqual(0, swapped.Exploration!.Captured!.Value.Length);
        Assert.AreEqual(0, swapped.EvolutionHistory!.Value.Length);
        CollectionAssert.AreEqual(new[] { "wild-battle:2" }, swapped.Exploration.Battles.Select(b => b.Id).ToArray());
        Assert.AreEqual(outgoing.InstanceId, swapped.NpcTrades!.Value.Single().Outgoing.InstanceId);
        var finished = Finish(swapped, tradeId);
        Assert.IsNull(Validate("ValidateNpcTradeHistory", finished));
        var released = ReleaseRecipient(finished);
        Assert.IsNull(Validate("ValidateNpcTradeHistory", released));
        var restored = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(released, 1, session.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion), session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion,
            validate: s => Validate("ValidateNpcTradeHistory", s)).State;
        Assert.AreEqual(1, restored.NpcTrades!.Value.Length);
        Assert.IsTrue(restored.CaughtSpecies.Contains(swapped.Party[1].Pokemon.SourceSpeciesId));
        var duplicate = restored with { NpcTrades = restored.NpcTrades.Value.Add(restored.NpcTrades.Value[0]) };
        Assert.IsNotNull(Validate("ValidateNpcTradeHistory", duplicate), "A missing recipient cannot make a completed trade repeatable.");
        var retry = Assert.ThrowsExactly<TargetInvocationException>(() => Effect(restored, tradeId, "ChoosePartyMon"));
        Assert.IsInstanceOfType<InvalidDataException>(retry.InnerException);
        Assert.AreEqual("npc_trade_chooser_context", retry.InnerException!.Message);
    }

    [TestMethod]
    public void Dontae_letter_survives_recipient_release_without_becoming_duplicable()
    {
        var finished = Finish(Effect(Prepare(1), 1, "DoInGameTradeScene"), 1);
        Assert.IsNull(Validate("ValidateNpcTradeHistory", finished));
        Assert.IsNull(Validate("ValidateCampaignMail", finished));
        var stored = SourceMailRules.SendToMailbox(finished.Mail!, finished.Party, 1);
        Assert.IsTrue(stored.Succeeded, stored.Error);
        var released = ReleaseRecipient(finished with { Mail = stored.State, Party = stored.Party });
        Assert.IsNull(Validate("ValidateNpcTradeHistory", released));
        Assert.IsNull(Validate("ValidateCampaignMail", released));
        Assert.AreEqual(1, released.Mail!.Mailbox.Count(m => m is not null));
        var copied = released with { Mail = released.Mail with { Mailbox = released.Mail.Mailbox.SetItem(1, released.Mail.Mailbox[0]) } };
        Assert.AreEqual("save_mail_acquisition_missing", Validate("ValidateCampaignMail", copied));
        var unissued = released with { NpcTrades = [] };
        Assert.AreEqual("save_mail_acquisition_missing", Validate("ValidateCampaignMail", unissued));
    }

    [TestMethod]
    public void Departed_original_letter_cannot_be_restored_from_trade_snapshots()
    {
        var first = Finish(Effect(Prepare(1), 1, "DoInGameTradeScene"), 1);
        var stored = SourceMailRules.SendToMailbox(first.Mail!, first.Party, 1);
        Assert.IsTrue(stored.Succeeded, stored.Error);
        var outgoing = Pokemon(first.NextPokemonId, new("pret:SPECIES_ABRA"));
        var state = first with { Party = [first.Party[0], outgoing], Mail = stored.State,
            NextPokemonId = first.NextPokemonId + 1 };
        var attached = SourceMailRules.GiveFromMailbox(state.Mail, state.Party, 0, 1);
        Assert.IsTrue(attached.Succeeded, attached.Error);
        state = Context(state with { Party = attached.Party, Mail = attached.State,
            NpcTrade = new(0, CampaignNpcTradePhase.Selected, 1, outgoing.InstanceId) }, 0);
        state = Effect(state, 0, "CreateInGameTradePokemon");
        var completed = Finish(Effect(state, 0, "DoInGameTradeScene"), 0);
        Assert.IsNull(Validate("ValidateNpcTradeHistory", completed));
        Assert.IsNull(Validate("ValidateCampaignMail", completed));
        Assert.IsNotNull(completed.NpcTrades!.Value[1].OutgoingMail);
        var replay = completed with { Mail = completed.Mail! with { Mailbox = completed.Mail.Mailbox.SetItem(0,
            completed.NpcTrades.Value[0].IncomingMail) } };
        Assert.AreEqual("save_mail_acquisition_missing", Validate("ValidateCampaignMail", replay));
        var reversed = completed with { NpcTrades = completed.NpcTrades.Value.Reverse().ToImmutableArray() };
        Assert.AreEqual("save_mail_departure_before_acquisition", Validate("ValidateCampaignMail", reversed));
    }
}
