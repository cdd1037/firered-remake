using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Generated scripts and controlled valid ownership fixtures, exercised through public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class DaisyDesignerServicesTests : WildBattleFixture
{
    private const string Counter = "VAR_MASSAGE_COOLDOWN_STEP_COUNTER";
    private const string DaisyText = "PalletTown_RivalsHouse_Text_";
    private const string DesignerText = "CeladonCity_Condominiums_3F_Text_";

    private CampaignState Place(CampaignState state, string name, int x, int y)
    {
        var map = content.Maps[new(name)];
        Assert.IsFalse(map.GetTile(x, y)!.Blocked);
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, Direction.North) },
            Actors = content.MapDefinition(map.Id).Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence },
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null,
            MapEdits = null, EntryPrograms = [], Dialogue = null, Motions = [],
            IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    private CampaignState Daisy(int cooldown = 500, int friendship = 100, bool fainted = false)
    {
        var state = FieldFixture();
        var lead = state.Party[0];
        state = state with {
            Party = state.Party.SetItem(0, lead with { Pokemon = lead.Pokemon with {
                Friendship = friendship, CurrentHp = fainted ? 0 : lead.Pokemon.CurrentHp } }),
            Script = state.Script with { Variables = state.Script.Variables.SetItem(Counter, cooldown),
                Flags = state.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR", true) }
        };
        return Place(state, "map.rivals-house", 10, 7);
    }

    private CampaignSession Start(CampaignState state)
    {
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled initial save");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private HashSet<string> Finish(CampaignSession session, bool stopAtParty = false, int choice = 1, bool stopAtDiploma = false)
    {
        var texts = new HashSet<string>(StringComparer.Ordinal);
        for (int n = 0; n < 250; n++)
        {
            if (session.State.Dialogue is { } dialogue) texts.Add(dialogue.TextSymbol);
            if (session.State.DaisyGrooming?.ChoosingParty == true) {
                Assert.IsTrue(stopAtParty, "party selection must be explicit");
                return texts;
            }
            if (stopAtDiploma && session.State.Diploma is not null) return texts;
            if (session.State.Dialogue is null && session.State.Script.Completed && session.State.Script.Wait is null && session.State.EntryPrograms.IsEmpty) return texts;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? choice : null));
        }
        Assert.Fail("source presentation did not finish");
        return texts;
    }

    private void Choose(CampaignSession session)
    {
        Send(session, new("interact"));
        Finish(session, stopAtParty: true);
        Assert.IsTrue(session.State.DaisyGrooming?.ChoosingParty);
        Assert.AreEqual(StoryWaitKind.PartySelection, session.State.Script.Wait?.Kind);
    }

    [TestMethod]
    [DataRow(0, "WhyWouldMonHateYouSoMuch")]
    [DataRow(1, "DontLikeWayItGlaresAtYou")][DataRow(49, "DontLikeWayItGlaresAtYou")]
    [DataRow(50, "NotFamiliarWithYouYet")][DataRow(99, "NotFamiliarWithYouYet")]
    [DataRow(100, "ItsWarmingUpToYou")][DataRow(149, "ItsWarmingUpToYou")]
    [DataRow(150, "ItsQuiteFriendly")][DataRow(199, "ItsQuiteFriendly")]
    [DataRow(200, "ItLooksVeryHappy")][DataRow(254, "ItLooksVeryHappy")]
    [DataRow(255, "CouldntLoveYouMore")]
    public void FriendshipRatingUsesSourceThresholdsIncludingFaintedLead(int friendship, string text)
    {
        var session = Start(Daisy(499, friendship, fainted: true));
        string party = CanonicalJson.Hash(session.State.Party);
        Send(session, new("interact"));
        var texts = Finish(session);
        Assert.IsTrue(texts.Contains(DaisyText + text));
        Assert.IsFalse(texts.Contains(DaisyText + "LikeMeToGroomMon"));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        Assert.AreEqual(499, session.State.Script.Variables[Counter]);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public void GroomingFaintedOrHealthySelectionChangesOnlyFriendshipAndResetsCooldown(bool fainted)
    {
        var session = Start(Daisy(fainted: fainted));
        var before = session.State.Party;
        var rng = session.State.World.Rng;
        Choose(session);
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
        Send(session, new("daisy_choose", PartyIndex: 0) { TargetInstanceId = before[0].InstanceId });
        Assert.IsTrue(Finish(session).Contains(DaisyText + "ThereYouGoAllDone"));
        Assert.AreEqual(0, session.State.Script.Variables[Counter]);
        Assert.IsTrue(session.State.Party[0].Pokemon.Friendship > before[0].Pokemon.Friendship);
        Assert.AreEqual(CanonicalJson.Hash(before), CanonicalJson.Hash(session.State.Party.SetItem(0,
            session.State.Party[0] with { Pokemon = session.State.Party[0].Pokemon with { Friendship = before[0].Pokemon.Friendship } })));
        Assert.AreEqual(rng, session.State.World.Rng);
        Assert.IsNull(session.State.DaisyGrooming);
        StrictRoundtrip(session);
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Contains(DaisyText + "MayISeeFirstMon"));
        Assert.AreEqual(0, session.State.Script.Variables[Counter]);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public void DecliningAndCancellingDoNotConsumeGrooming(bool chooseThenCancel)
    {
        var session = Start(Daisy());
        string party = CanonicalJson.Hash(session.State.Party);
        if (chooseThenCancel) { Choose(session); Send(session, new("daisy_cancel")); }
        else Send(session, new("interact"));
        Assert.IsTrue(Finish(session, choice: 0).Contains(DaisyText + "DontNeedAnyGrooming"));
        Assert.AreEqual(500, session.State.Script.Variables[Counter]);
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        Assert.IsNull(session.State.DaisyGrooming);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void EggSelectionLeavesHatchCyclesAndCooldownUnchanged()
    {
        var state = Daisy();
        while (state.Party.Length > 1) {
            var stored = PokemonStorageRules.Deposit(content.PokemonContent, state.Party, state.Storage!, 1, 13);
            Assert.IsTrue(stored.Succeeded);
            state = state with { Party = stored.Party, Storage = stored.Storage };
        }
        PartyPokemon Parent(long id, uint personality) {
            var pokemon = SourcePokemonFactory.Create(mechanics,
                new(new("pret:SPECIES_BULBASAUR"), 5, state.TrainerId,
                    IvPolicy.Fixed(3), PersonalityPolicy.Fixed(personality)), new(12)).Pokemon;
            return new(id, pokemon, "BULBASAUR", state.PlayerName, state.PlayerGender,
                2, "MAPSEC_FOUR_ISLAND", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        }
        static CampaignEggParent Receipt(PartyPokemon mon) => new(mon.InstanceId, mon.Pokemon.SourceSpeciesId,
            mon.Pokemon.Personality, mon.Pokemon.OriginalTrainerId, mon.Pokemon.Ivs,
            mon.Pokemon.Moves.Select(m => m.Move).ToImmutableArray(), mon.HeldItem);
        long id = state.NextPokemonId;
        var female = Parent(id, 0); var male = Parent(id + 1, 255);
        var trainer = new SourceEggTrainer(state.TrainerId, state.PlayerName, state.PlayerGender);
        var made = SourceEggRules.Receive(content.PokemonContent!, content.PokemonContent!.Catalog.BreedingData!,
            new(new(female, 0), new(male, 0), 123, 0), id + 2, trainer, "MAPSEC_FOUR_ISLAND", 4, state.World.Rng);
        var receipt = new CampaignDayCareEggReceipt(id + 2, Receipt(female), Receipt(male), 123,
            made.Egg.Pokemon.SourceSpeciesId, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs,
            "MAPSEC_FOUR_ISLAND", state.World.Rng, made.Rng);
        state = state with { Party = state.Party.Add(made.Egg), NextPokemonId = id + 3,
            DayCareEggs = (state.DayCareEggs ?? []).Add(receipt), World = state.World with { Rng = made.Rng } };
        var session = Start(state);
        string party = CanonicalJson.Hash(session.State.Party);
        Choose(session);
        Send(session, new("daisy_choose", PartyIndex: 1) { TargetInstanceId = made.Egg.InstanceId });
        Assert.IsTrue(Finish(session).Contains(DaisyText + "CantGroomAnEgg"));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        Assert.AreEqual(500, session.State.Script.Variables[Counter]);
        StrictRoundtrip(session);

        // Ratings skip a leading egg and still use the first non-egg when it is fainted.
        var lead = state.Party[0];
        var rating = state with {
            Party = [made.Egg, lead with { Pokemon = lead.Pokemon with { Friendship = 200, CurrentHp = 0 } }],
            Script = state.Script with { Variables = state.Script.Variables.SetItem(Counter, 499) }
        };
        session = Start(rating);
        party = CanonicalJson.Hash(session.State.Party);
        Send(session, new("interact"));
        Assert.IsTrue(Finish(session).Contains(DaisyText + "ItLooksVeryHappy"));
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        StrictRoundtrip(session);

        // An all-egg party cannot be reached by PC deposit (last usable guard). Exercise
        // the source helper's defensive slot-zero fallback without inventing a player save.
        Assert.AreEqual(20, made.Egg.Pokemon.Friendship, "Bulbasaur egg cycle fixture");
        Assert.AreEqual(1, SourceGiftEggRules.GetLeadMonFriendship(content.PokemonContent!, [made.Egg]));
        Assert.AreEqual(0, SourceGiftEggRules.GetLeadMonFriendship(content.PokemonContent!, []));
    }

    [TestMethod]
    public void FiveHundredActualWalkingStepsEnableAnotherGroomingAfterContinue()
    {
        var session = Start(Daisy());
        Choose(session);
        Send(session, new("daisy_choose", PartyIndex: 0) { TargetInstanceId = session.State.Party[0].InstanceId });
        Finish(session);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        session = CampaignSession.RestoreCurrent(content, saved.State, saved.Generation);
        int steps = 0;
        for (int n = 0; n < 1100 && session.State.Script.Variables[Counter] < 500; n++) {
            int x = session.State.World.Player.X;
            Send(session, new("move", Direction: x == 10 ? Direction.West : Direction.East));
            Finish(session);
            if (session.State.World.Player.X != x) steps++;
        }
        Assert.AreEqual(500, steps);
        Assert.AreEqual(500, session.State.Script.Variables[Counter]);
        Assert.AreEqual(10, session.State.World.Player.X);
        Send(session, new("turn", Direction: Direction.North)); Finish(session);
        Choose(session);
        Send(session, new("daisy_choose", PartyIndex: 0) { TargetInstanceId = session.State.Party[0].InstanceId });
        Assert.IsTrue(Finish(session).Contains(DaisyText + "ThereYouGoAllDone"));
        Assert.AreEqual(0, session.State.Script.Variables[Counter]);
        StrictRoundtrip(session);
    }

    private void RejectUnchanged(CampaignSession session, CampaignCommand command)
    {
        string before = CanonicalJson.Hash(session.State);
        long revision = session.Revision;
        var result = session.Execute("daisy-reject:" + commands++, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
    }

    [TestMethod]
    public void StaleOrMissingSelectionIdentityAndUnrelatedCommandsAreAtomic()
    {
        var session = Start(Daisy());
        RejectUnchanged(session, new("daisy_cancel"));
        RejectUnchanged(session, new("daisy_choose", PartyIndex: 0) { TargetInstanceId = session.State.Party[0].InstanceId });
        Choose(session);
        RejectUnchanged(session, new("daisy_choose", PartyIndex: 0));
        RejectUnchanged(session, new("daisy_choose", PartyIndex: 0) { TargetInstanceId = long.MaxValue });
        RejectUnchanged(session, new("daisy_choose", PartyIndex: 6) { TargetInstanceId = session.State.Party[0].InstanceId });
        RejectUnchanged(session, new("move", Direction: Direction.West));
        Send(session, new("daisy_cancel")); Finish(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void DirectMassageEffectsRejectForgedActorInstructionSelectionAndCooldownContexts()
    {
        var session = Start(Daisy());
        Choose(session);
        var program = content.FindProgram("PalletTown_RivalsHouse_EventScript_GroomMon")!;
        int index = Enumerable.Range(0, program.Instructions.Length).Single(i =>
            program.Instructions[i].Opcode == "special" && program.Instructions[i].Args.SequenceEqual(new[] { "DaisyMassageServices" }));
        var instruction = program.Instructions[index];
        var effect = new StoryEffect(0, instruction.Opcode, instruction.Args, instruction.Source);
        var valid = session.State with {
            DaisyGrooming = new(false, 0, session.State.Party[0].InstanceId),
            Script = session.State.Script with { Stack = [new(program.Id, index + 1)],
                Wait = new(StoryWaitKind.NativeEffect, "test-massage"),
                Variables = session.State.Script.Variables.SetItem("VAR_0x8004", 0) }
        };
        var method = typeof(CampaignSession).GetMethod("TryApplyDaisyAndDesignerEffect", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] accepted = [valid, effect, null];
        Assert.IsTrue((bool)method.Invoke(session, accepted)!);
        Assert.AreEqual(0, ((CampaignState)accepted[2]!).Script.Variables[Counter], "positive control authenticates the constructed native callback");
        CampaignState[] forged = [
            valid with { LastTalked = null },
            valid with { Script = valid.Script with { Locked = false } },
            valid with { Script = valid.Script with { QuestLogState = 1 } },
            valid with { Script = valid.Script with { Flags = valid.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR", false) } },
            valid with { Script = valid.Script with { Stack = [new(program.Id, index)] } },
            valid with { Script = valid.Script with { Variables = valid.Script.Variables.SetItem("VAR_LAST_TALKED", 99) } },
            valid with { Script = valid.Script with { Variables = valid.Script.Variables.SetItem(Counter, 499) } },
            valid with { Script = valid.Script with { Variables = valid.Script.Variables.SetItem("VAR_0x8004", 1) } },
            valid with { DaisyGrooming = null },
            valid with { DaisyGrooming = valid.DaisyGrooming! with { Groomed = true } },
            valid with { DaisyGrooming = valid.DaisyGrooming! with { InstanceId = long.MaxValue } }
        ];
        foreach (var state in forged) {
            string hash = CanonicalJson.Hash(state);
            var exception = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(session, [state, effect, null]));
            Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
            Assert.AreEqual(hash, CanonicalJson.Hash(state));
        }
        Send(session, new("daisy_cancel")); Finish(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public void DesignerChecksCaughtKantoSpeciesAndOpensNativeDiploma(bool complete)
    {
        var state = FieldFixture();
        state = state with { SeenSpecies = state.SeenSpecies.Union(Enumerable.Range(1, 151)),
            CaughtSpecies = complete ? state.CaughtSpecies.Union(Enumerable.Range(1, 150)).Remove(151) : state.CaughtSpecies.Add(151) };
        var session = Start(Place(state, "map.celadon-city-condominiums-3f", 3, 9));
        string party = CanonicalJson.Hash(session.State.Party);
        string caught = CanonicalJson.Hash(session.State.CaughtSpecies);
        Send(session, new("interact"));
        var texts = Finish(session, stopAtDiploma: true);
        if (complete) {
            Assert.IsTrue(texts.Contains(DesignerText + "CompletedPokedexCongratulations"));
            Assert.AreEqual(new CampaignDiploma(false), session.State.Diploma);
            Assert.IsNull(session.State.Dialogue);
            Assert.AreEqual(StoryWaitKind.Presentation, session.State.Script.Wait?.Kind);
            RejectUnchanged(session, new("ack"));
            RejectUnchanged(session, new("move", Direction: Direction.West));
            Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
            Send(session, new("diploma_complete"));
            Finish(session);
        } else {
            Assert.IsTrue(texts.Contains(DesignerText + "ImGameDesignerShowMeFinishedPokedex"));
            Assert.IsNull(session.State.Diploma);
        }
        Assert.IsNull(session.State.Diploma);
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party));
        Assert.AreEqual(caught, CanonicalJson.Hash(session.State.CaughtSpecies));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public void NationalDiplomaUsesInternalSpeciesMappingAndHasNoSeparateUnlockGate(bool omitRequiredHoennSpecies)
    {
        var state = FieldFixture();
        var required = content.PokedexMenu!.Entries.Values
            .Where(e => e.NationalNumber is not (151 or 249 or 250 or 251 or 385 or 386))
            .Select(e => e.SourceSpeciesId).ToImmutableSortedSet();
        Assert.AreEqual(380, required.Count);
        Assert.IsTrue(required.Contains(content.PokedexMenu.NationalEntries[280].SourceSpeciesId));
        if (omitRequiredHoennSpecies) required = required.Remove(content.PokedexMenu.NationalEntries[280].SourceSpeciesId);
        state = state with { SeenSpecies = state.SeenSpecies.Union(required), CaughtSpecies = state.CaughtSpecies.Union(required),
            UnownPokedexPersonality = state.UnownPokedexPersonality ?? 0,
            // Explicit synthetic unlock receipt for this collection fixture, not recovered player history.
            TanobyCompletion = state.TanobyCompletion ?? new(
                content.SevenIslandField!.TanobyKey.Boulders.Select(b => b.LocalId).ToImmutableArray()),
            Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_SYS_UNLOCKED_TANOBY_RUINS", true) } };
        var session = Start(Place(state, "map.celadon-city-condominiums-3f", 3, 9));
        var inventory = session.State.Inventory;
        var party = session.State.Party;
        var rng = session.State.World.Rng;
        var flags = session.State.Script.Flags;
        for (int visit = 0; visit < 2; visit++) {
            Send(session, new("interact")); Finish(session, stopAtDiploma: true);
            Assert.AreEqual(!omitRequiredHoennSpecies, session.State.Diploma!.National);
            // The callback itself has no National-unlock check. Probe that contract without
            // pretending a postgame save with its National gate removed would be restorable.
            var program = content.FindProgram("CeladonCity_Condominiums_3F_EventScript_ShowDiploma")!;
            int index = Enumerable.Range(0, program.Instructions.Length).Single(i =>
                program.Instructions[i].Opcode == "special" && program.Instructions[i].Args.SequenceEqual(new[] { "ShowDiploma" }));
            var instruction = program.Instructions[index];
            var withoutGate = session.State with { Diploma = null, NationalDexMagic = 0,
                Script = session.State.Script with { Stack = [new(program.Id, index + 1)],
                    Wait = new(StoryWaitKind.NativeEffect, "test-diploma-gate"),
                    Flags = session.State.Script.Flags.SetItem("FLAG_SYS_NATIONAL_DEX", false),
                    Variables = session.State.Script.Variables.SetItem("VAR_NATIONAL_DEX", 0) } };
            var callback = typeof(CampaignSession).GetMethod("TryApplyDaisyAndDesignerEffect", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object?[] arguments = [withoutGate, new StoryEffect(0, instruction.Opcode, instruction.Args, instruction.Source), null];
            Assert.IsTrue((bool)callback.Invoke(session, arguments)!);
            Assert.AreEqual(!omitRequiredHoennSpecies, ((CampaignState)arguments[2]!).Diploma!.National);
            Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.State));
            RejectUnchanged(session, new("ack"));
            Send(session, new("diploma_complete")); Finish(session);
            Assert.IsNull(session.State.Diploma);
            Assert.IsTrue(session.State.Script.Completed);
            Assert.IsFalse(session.State.Script.Locked);
            Assert.IsNull(session.State.Script.Wait);
            Assert.AreEqual(inventory, session.State.Inventory);
            Assert.AreEqual(party, session.State.Party);
            Assert.AreEqual(rng, session.State.World.Rng);
            Assert.AreEqual(CanonicalJson.Hash(flags), CanonicalJson.Hash(session.State.Script.Flags));
            StrictRoundtrip(session);
        }
        RejectUnchanged(session, new("diploma_complete"));
    }

    [TestMethod]
    public void DiplomaReturnRejectsForgedWaitActorAndInstructionOwnership()
    {
        var state = FieldFixture();
        state = state with { SeenSpecies = state.SeenSpecies.Union(Enumerable.Range(1, 150)),
            CaughtSpecies = state.CaughtSpecies.Union(Enumerable.Range(1, 150)) };
        var session = Start(Place(state, "map.celadon-city-condominiums-3f", 3, 9));
        Send(session, new("interact")); Finish(session, stopAtDiploma: true);
        var method = typeof(CampaignSession).GetMethod("TryDecideDiploma", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var active = session.State;
        CampaignState[] forged = [
            active with { LastTalked = null },
            active with { Script = active.Script with { Locked = false } },
            active with { Script = active.Script with { QuestLogState = 1 } },
            active with { Script = active.Script with { Wait = active.Script.Wait! with { Kind = StoryWaitKind.Dialog } } },
            active with { Script = active.Script with { Stack = [] } },
            active with { Script = active.Script with { Variables = active.Script.Variables.SetItem("VAR_LAST_TALKED", 99) } }
        ];
        foreach (var wrong in forged) {
            var hash = CanonicalJson.Hash(wrong);
            object?[] arguments = [wrong, new CampaignCommand("diploma_complete"), null];
            Assert.IsTrue((bool)method.Invoke(session, arguments)!);
            var decision = (Decision<CampaignState>)arguments[2]!;
            Assert.AreEqual(CommandStatus.Rejected, decision.Status);
            Assert.AreEqual(hash, CanonicalJson.Hash(decision.State));
        }
        Send(session, new("diploma_complete")); Finish(session); StrictRoundtrip(session);
    }

}
