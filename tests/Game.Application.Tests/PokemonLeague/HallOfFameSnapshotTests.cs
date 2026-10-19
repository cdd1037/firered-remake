using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.PokemonLeague;

[TestClass, TestCategory("Game")]
public sealed class HallOfFameSnapshotTests
{
    private CampaignSession inspector = default!;
    private CampaignState original = default!;

    [TestInitialize]
    public void SetUp()
    {
        inspector = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        original = CurrentCampaignFixture.Checkpoint.State;
        Assert.IsNotNull(original.HallOfFame);
        Assert.IsNull(Validate(original));
    }

    // These tests isolate the Hall contract from unrelated story-route admission.
    // Championship preparation uses real checkpoint receipts and the production
    // entry/save-retry transitions; it is not a natural replay of all five battles.
    private T Invoke<T>(string method, params object?[] arguments) =>
        (T)typeof(CampaignSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(inspector, arguments)!;
    private string? Validate(CampaignState state) => Invoke<string?>("ValidatePokemonLeagueHallOfFame", state);

    [TestMethod]
    public void ReleasedChampionKeepsIndependentArchive()
    {
        int slot = Enumerable.Range(0, original.Party.Length).First(i => original.Party[i].ChampionRibbon &&
            !original.Party[i].Pokemon.Moves.Any(m => m.Move.Value is "pret:MOVE_SURF" or "pret:MOVE_DIVE"));
        long id = original.Party[slot].InstanceId;
        string archive = CanonicalJson.Hash(original.HallOfFame);
        var release = PokemonStorageReleaseRules.ReleaseSelected(CurrentCampaignFixture.Catalog.PokemonContent,
            original.Party, original.Storage!, null, PokemonStorageMoveLocation.Party(slot), id);
        Assert.AreEqual(PokemonStorageReleaseOutcome.Released, release.Outcome, release.Diagnostic?.Code);
        var state = original with { Party = release.Party, Storage = release.Storage };
        Assert.IsFalse(state.Party.Any(p => p.InstanceId == id));
        Assert.IsTrue(state.HallOfFame!.Teams.Any(t => t.Members.Any(m => m.InstanceId == id)));
        Assert.IsNull(Validate(state));
        Assert.AreEqual(archive, CanonicalJson.Hash(state.HallOfFame));
    }

    [TestMethod]
    public void ArchiveAndCumulativeRibbonFactsSurviveEveryRecipientLeavingOwnership()
    {
        string archive = CanonicalJson.Hash(original.HallOfFame);
        var state = original with
        {
            Party = original.Party.Where(p => !p.ChampionRibbon).ToImmutableArray(),
            Storage = original.Storage! with
            {
                Boxes = original.Storage!.Boxes.Select(b => b with
                {
                    Slots = b.Slots.Select(p => p is { ChampionRibbon: true } ? null : p).ToImmutableArray()
                }).ToImmutableArray()
            },
            Route5DayCare = null, FourIslandDayCare = null, Pc = null
        };
        Assert.IsNull(Validate(state));
        Assert.IsTrue(state.Script.Flags["FLAG_SYS_RIBBON_GET"]);
        Assert.AreEqual(original.GameStats!["GAME_STAT_RECEIVED_RIBBONS"], state.GameStats!["GAME_STAT_RECEIVED_RIBBONS"]);
        Assert.AreEqual(archive, CanonicalJson.Hash(state.HallOfFame));
    }

    [TestMethod]
    public void RenamingCurrentChampionDoesNotRewriteSnapshot()
    {
        int slot = Enumerable.Range(0, original.Party.Length).First(i => original.Party[i].ChampionRibbon);
        string archive = CanonicalJson.Hash(original.HallOfFame);
        var state = original with { Party = original.Party.SetItem(slot, original.Party[slot] with { CustomNickname = "NEW NAME" }) };
        Assert.IsNull(Validate(state));
        Assert.AreEqual(archive, CanonicalJson.Hash(state.HallOfFame));
    }

    [TestMethod]
    [DataRow("id-zero")]
    [DataRow("id-unallocated")]
    [DataRow("species")]
    [DataRow("level")]
    [DataRow("slot")]
    [DataRow("invalid-custom-name")]
    [DataRow("duplicate-id")]
    [DataRow("championship")]
    public void SnapshotStillRejectsInvalidLocalContentAndChampionship(string corruption)
    {
        var team = original.HallOfFame!.Teams[0];
        var member = team.Members[0];
        member = corruption switch
        {
            "id-zero" => member with { InstanceId = 0 },
            "id-unallocated" => member with { InstanceId = original.NextPokemonId },
            "species" => member with { SpeciesOrEgg = 999 },
            "level" => member with { Level = 101 },
            "slot" => member with { PartySlot = 5 },
            "invalid-custom-name" => member with { CustomNickname = "" },
            "duplicate-id" => member with { InstanceId = team.Members[1].InstanceId },
            _ => member
        };
        team = team with { Members = team.Members.SetItem(0, member) };
        if (corruption == "championship") team = team with { TrainerBattleId = "missing-championship" };
        Assert.IsNotNull(Validate(original with { HallOfFame = original.HallOfFame with
        { Teams = original.HallOfFame.Teams.SetItem(0, team) } }));
    }

    private CampaignState PrepareEntry(CampaignState state)
    {
        int challenge = state.PokemonLeague!.ChallengeNumber;
        var champion = state.ForestTrainerHistory!.Value.Last(h => h.PokemonLeagueChallenge.HasValue &&
            h.TrainerId.Contains("TRAINER_CHAMPION_", StringComparison.Ordinal));
        var history = state.ForestTrainerHistory.Value.Add(champion with
        { Id = "hall-snapshot-test:" + challenge, PokemonLeagueChallenge = challenge });
        return state with
        {
            ForestTrainerHistory = history,
            PokemonLeague = state.PokemonLeague with
            {
                ChallengeNumber = challenge + 1,
                Resets = state.PokemonLeague.Resets.Add(new(challenge, history.Length, "hall-of-fame"))
            },
            PokemonLeagueCompletion = new(CampaignPokemonLeagueCompletionStage.RecordingFieldEffect,
                "hall-snapshot-test:" + challenge, challenge, state.Party.Length, true, false),
            LastHealingLocation = "HEAL_LOCATION_PALLET_TOWN", ScreenIsBlack = true,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_TEMP_1", 1) }
        };
    }

    private CampaignState Enter(CampaignState state) => Invoke<CampaignState>("EnterPokemonLeagueHallOfFame", PrepareEntry(state));

    [TestMethod]
    public void RepeatEntryAwardsOnlyNewRecipientsAndKeepsLatestFiftyTeams()
    {
        int previousRibbons = original.GameStats!["GAME_STAT_RECEIVED_RIBBONS"];
        int expectedAward = original.Party.Any(p => !p.IsEgg && !p.ChampionRibbon) ? 1 : 0;
        var state = Enter(original);
        Assert.AreEqual(previousRibbons + expectedAward, state.GameStats!["GAME_STAT_RECEIVED_RIBBONS"]);
        Assert.IsTrue(state.Party.Where(p => !p.IsEgg).All(p => p.ChampionRibbon));
        string firstArchive = CanonicalJson.Hash(original.HallOfFame);
        for (int i = 0; i < 52; i++) state = Enter(state);
        Assert.AreEqual(50, state.HallOfFame!.Teams.Length);
        Assert.AreEqual(previousRibbons + expectedAward, state.GameStats!["GAME_STAT_RECEIVED_RIBBONS"]);
        CollectionAssert.AreEqual(state.PokemonLeague!.Resets.Where(r => r.Reason == "hall-of-fame").TakeLast(50)
            .Select(r => r.ChallengeNumber).ToArray(), state.HallOfFame.Teams.Select(t => t.ChallengeNumber).ToArray());
        Assert.AreEqual(firstArchive, CanonicalJson.Hash(original.HallOfFame));
        Assert.IsNull(Validate(state with { PokemonLeagueCompletion = null }));
    }

    [TestMethod]
    public void FailedSaveRetryDoesNotRepeatTeamOrRibbonAward()
    {
        var state = Enter(original);
        string archive = CanonicalJson.Hash(state.HallOfFame), stats = CanonicalJson.Hash(state.GameStats);
        CampaignState Decide(CampaignState input, CampaignCommand command)
        {
            object?[] arguments = [input, command, null];
            Assert.IsTrue(Invoke<bool>("TryDecidePokemonLeague", arguments));
            var decision = (Decision<CampaignState>)arguments[2]!;
            Assert.AreEqual(CommandStatus.Accepted, decision.Status);
            return decision.State;
        }
        string request = state.PokemonLeagueCompletion!.RequestId;
        state = Decide(state, new("league_save_result", Text: request, Choice: 0));
        state = Decide(state, new("league_save_retry", Text: request));
        state = Decide(state, new("league_save_result", Text: request, Choice: 1));
        Assert.AreEqual(archive, CanonicalJson.Hash(state.HallOfFame));
        Assert.AreEqual(stats, CanonicalJson.Hash(state.GameStats));
    }

    [TestMethod]
    public void ArchiveSerializesNoRecipientLedgerAndRejectsOldField()
    {
        string json = JsonSerializer.Serialize(original.HallOfFame);
        Assert.IsFalse(json.Contains("RibbonRecipients", StringComparison.Ordinal));
        string old = json.Insert(1, "\"RibbonRecipients\":[],");
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<CampaignHallOfFameArchive>(old));
    }
}
