using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class GiftEggOwnershipTests
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

    private string? Validate(string name, CampaignState state) =>
        (string?)typeof(CampaignSession).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session, [state]);

    private CampaignState RoundTrip(CampaignState state, string validator)
    {
        Assert.IsNull(Validate(validator, state));
        return SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, 1, session.RulesVersion,
            CurrentCampaignFixture.Catalog.ContentVersion), session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion,
            validate: s => Validate(validator, s)).State;
    }

    [TestMethod]
    [DataRow("magikarp")]
    [DataRow("eevee")]
    [DataRow("lapras")]
    [DataRow("dojo")]
    [DataRow("fossil")]
    public void Finite_gift_completion_survives_recipient_departure(string kind)
    {
        var ivs = new SixStats(3, 3, 3, 3, 3, 3);
        var state = initial with { NextPokemonId = 3, Party = [], SeenSpecies = initial.SeenSpecies.Union([129, 133, 131, 106, 138]),
            CaughtSpecies = initial.CaughtSpecies.Union([129, 133, 131, 106, 138]) };
        string flag, validator;
        switch (kind)
        {
            case "magikarp": state = state with { MagikarpGift = new(2, 7, ivs) }; flag = "FLAG_BOUGHT_MAGIKARP"; validator = "ValidateMagikarpGift"; break;
            case "eevee": state = state with { EeveeGift = new(2, 7, ivs), Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_HIDE_EEVEE_BALL", true) } }; flag = "FLAG_GOT_EEVEE"; validator = "ValidateEeveeGift"; break;
            case "lapras": state = state with { LaprasGift = new(2, 7, ivs) }; flag = "FLAG_GOT_LAPRAS_FROM_SILPH"; validator = "ValidateSilphGifts"; break;
            case "dojo": state = state with { DojoGift = new(106, 2, 7, ivs), Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_HIDE_DOJO_HITMONLEE_BALL", true) } }; flag = "FLAG_GOT_HITMON_FROM_DOJO"; validator = "ValidateFightingDojoGift"; break;
            default: state = state with { FossilRevivals = [new(1, 357, 138, 2, 7, ivs)], Script = state.Script with {
                Flags = state.Script.Flags.SetItem("FLAG_GOT_HELIX_FOSSIL", true),
                Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_WHICH_FOSSIL", 1) } };
                flag = "FLAG_REVIVED_HELIX"; validator = "ValidateFossilLabProgress"; break;
        }
        state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(flag, true) } };
        var restored = RoundTrip(state, validator);
        Assert.IsTrue(restored.Script.Flags[flag]);
        var bad = restored with { Script = restored.Script with { Flags = restored.Script.Flags.SetItem(flag, false) } };
        string before = CanonicalJson.Hash(bad);
        Assert.IsNotNull(Validate(validator, bad), "Missing completion flag must still reject.");
        Assert.AreEqual(before, CanonicalJson.Hash(bad), "Rejected validation must not mutate the state.");
        Assert.IsNotNull(Validate(validator, restored with { NextPokemonId = 2 }), "Receipt IDs must remain below the allocator.");
    }

    [TestMethod]
    public void Present_gift_identity_and_remaining_Master_Ball_inventory_still_validate()
    {
        var made = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_EEVEE"), 25, initial.TrainerId,
            IvPolicy.Fixed(3), PersonalityPolicy.Fixed(7)), initial.World.Rng).Pokemon;
        var mon = new PartyPokemon(2, made, "EEVEE", initial.PlayerName, initial.PlayerGender,
            2, "MAPSEC_CELADON_CITY", 25, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var state = initial with { Party = [mon], NextPokemonId = 3, EeveeGift = new(2, 7, made.Ivs),
            SeenSpecies = initial.SeenSpecies.Add(133), CaughtSpecies = initial.CaughtSpecies.Add(133),
            Script = initial.Script with { Flags = initial.Script.Flags.SetItem("FLAG_GOT_EEVEE", true).SetItem("FLAG_HIDE_EEVEE_BALL", true) } };
        Assert.IsNull(Validate("ValidateEeveeGift", state));
        Assert.IsNotNull(Validate("ValidateEeveeGift", state with { EeveeGift = state.EeveeGift! with { Personality = 8 } }));
        var claimed = initial with { Script = initial.Script with { Flags = initial.Script.Flags.SetItem("FLAG_GOT_MASTER_BALL_FROM_SILPH", true) } };
        Assert.IsNull(Validate("ValidateSilphGifts", claimed), "A consumed or departed held Master Ball does not require a retired counter.");
        var ball = new Game.Domain.Ids.ItemId("pret:ITEM_MASTER_BALL");
        var doubled = claimed with { Inventory = claimed.Inventory! with { Balls = [new(ball, 2)] } };
        Assert.IsNotNull(Validate("ValidateSilphGifts", doubled));
        var unclaimed = initial with { Inventory = initial.Inventory! with { Balls = [new(ball, 1)] } };
        Assert.IsNotNull(Validate("ValidateSilphGifts", unclaimed));
    }

    private PartyPokemon Parent(long id, uint pid)
    {
        var made = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_BULBASAUR"), 5, initial.TrainerId,
            IvPolicy.Fixed(3), PersonalityPolicy.Fixed(pid)), new RngState(0)).Pokemon;
        return new(id, made, null, initial.PlayerName, initial.PlayerGender,
            2, "MAPSEC_FOUR_ISLAND", 0, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }
    private static CampaignEggParent Receipt(PartyPokemon mon) => new(mon.InstanceId, mon.Pokemon.SourceSpeciesId,
        mon.Pokemon.Personality, mon.Pokemon.OriginalTrainerId, mon.Pokemon.Ivs,
        mon.Pokemon.Moves.Select(m => m.Move).ToImmutableArray(), mon.HeldItem);

    [TestMethod]
    public void Bred_egg_replays_and_hatches_after_both_parents_depart()
    {
        var first = Parent(1, 0); var second = Parent(2, 255);
        var trainer = new SourceEggTrainer(initial.TrainerId, initial.PlayerName, initial.PlayerGender);
        var rng = initial.World.Rng;
        var made = SourceEggRules.Receive(content, content.Catalog.BreedingData!, new(new(first, 0), new(second, 0), 123, 0),
            3, trainer, "MAPSEC_FOUR_ISLAND", 4, rng);
        var receipt = new CampaignDayCareEggReceipt(3, Receipt(first), Receipt(second), 123,
            made.Egg.Pokemon.SourceSpeciesId, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs, "MAPSEC_FOUR_ISLAND", rng, made.Rng);
        var state = initial with { Party = [made.Egg], NextPokemonId = 4, DayCareEggs = [receipt], World = initial.World with { Rng = made.Rng } };
        state = RoundTrip(state, "ValidateDayCareEggReceipts");
        var hatched = SourceEggRules.Hatch(content, made.Egg, trainer, "MAPSEC_FOUR_ISLAND", made.Rng);
        var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
        var completed = state with { Party = [hatched.Pokemon], World = state.World with { Rng = shards.Rng },
            DayCareEggs = [receipt with { Hatch = new("MAPSEC_FOUR_ISLAND", made.Rng, shards.Rng) }],
            SeenSpecies = state.SeenSpecies.Add(receipt.SourceSpeciesId), CaughtSpecies = state.CaughtSpecies.Add(receipt.SourceSpeciesId) };
        RoundTrip(completed, "ValidateDayCareEggReceipts");
        var bad = state with { DayCareEggs = [receipt with { First = receipt.First with { InstanceId = 0 } }] };
        string before = CanonicalJson.Hash(bad);
        Assert.IsNotNull(Validate("ValidateDayCareEggReceipts", bad));
        Assert.AreEqual(before, CanonicalJson.Hash(bad));
    }

    [TestMethod]
    public void Hatched_Togepi_completion_survives_departure()
    {
        var source = CurrentCampaignFixture.Catalog.FindProgram("FiveIsland_WaterLabyrinth_EventScript_TryGiveEgg")!.Instructions.Single(i => i.Opcode == "giveegg").Source;
        var trainer = new SourceEggTrainer(initial.TrainerId, initial.PlayerName, initial.PlayerGender);
        var made = SourceGiftEggRules.GiveEgg(content, content.Catalog.BreedingData!, SourceGiftEggRules.Togepi, 2, trainer, 4, initial.World.Rng);
        var hatched = SourceEggRules.Hatch(content, made.Egg, trainer, "MAPSEC_FOUR_ISLAND", made.Rng);
        var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
        var receipt = new CampaignGiftEggReceipt(2, source, 175, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs, initial.World.Rng, made.Rng)
            { Hatch = new("MAPSEC_FOUR_ISLAND", made.Rng, shards.Rng) };
        var state = initial with { Party = [], NextPokemonId = 3, GiftEggs = [receipt], World = initial.World with { Rng = shards.Rng },
            SeenSpecies = initial.SeenSpecies.Add(175), CaughtSpecies = initial.CaughtSpecies.Add(175),
            Script = initial.Script with { Flags = initial.Script.Flags.SetItem("FLAG_GOT_TOGEPI_EGG", true) } };
        RoundTrip(state, "ValidateGiftEggReceipts");
        Assert.IsNotNull(Validate("ValidateGiftEggReceipts", state with { GiftEggs = [receipt with { Personality = receipt.Personality ^ 1 }] }));
    }
}
