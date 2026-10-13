using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Exact original singles and preset doubles through the existing trainer construction kernel.</summary>
public static class SevenIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-seven-island-trainers-v1";
    public static readonly ImmutableDictionary<TrainerId, string> TrainerHashes = new Dictionary<TrainerId, string> {
        [new("pret:TRAINER_CRUSH_GIRL_CYNDY")] = "e3f7e4a084213828fd70c7a81e7f85a9ff21d335cf2c5cf8bd2f0c4a5d5630c9",
        [new("pret:TRAINER_TAMER_EVAN")] = "f5e1bcb9efdf2de890580b3406eaf777c557f25fbe4aaf206abbd9af2aa7e49b",
        [new("pret:TRAINER_PKMN_RANGER_JACKSON")] = "2039c62de09ba81e0aea0dbdc5fa8a2565086f2e92c28795bc2500e0c6d38b20",
        [new("pret:TRAINER_PKMN_RANGER_KATELYN")] = "904af99eb9930cd147af0316769916a3e1d225607710c91bb481be06ab0db333",
        [new("pret:TRAINER_COOLTRAINER_LEROY")] = "0fa10100203fe6df26fb524604c6cf902ec5c2e6fee5e70949554541eb69b4e3",
        [new("pret:TRAINER_COOLTRAINER_MICHELLE")] = "0645c2868d565dfa5d29ec5ff2217efbb4f3ef04e12ec86b4ea78e6acda29a5d",
        [new("pret:TRAINER_COOL_COUPLE_LEX_NYA")] = "d58e6de6ff64a845e885ce5899fc95a4d000e53c6c6ed4ca44f061e0fb48feca",
        [new("pret:TRAINER_AROMA_LADY_MIAH")] = "5ba3741786d454387d5fbde8634d9dcf349b4b78aeb879c2c20830a58db37a13",
        [new("pret:TRAINER_YOUNG_COUPLE_EVE_JON")] = "1883b495b6d1f21dd1b89fc26a95a40475fd2aa865e77b56367f4dda2fade74b",
        [new("pret:TRAINER_PKMN_RANGER_NICOLAS")] = "ca4dc10798e5ffb24668252d71af1b04fe2c5de4a1bc2182e5964659f2159ed9",
        [new("pret:TRAINER_PKMN_RANGER_MADELINE")] = "d37c9693057fbf4628f2d74c316effc50323c1b3ad1c75f44fe7ccd2d1827392",
        [new("pret:TRAINER_JUGGLER_MASON")] = "64daa3d8880bc5bbc072cca6a216c16ca8bc6f39a25c2b85143db4724e22ef4b",
        [new("pret:TRAINER_RUIN_MANIAC_BRANDON")] = "91792d076a3d80499f4d05805610c80403da742962c389cb6dc9c8e55e26dc52",
        [new("pret:TRAINER_RUIN_MANIAC_BENJAMIN")] = "3388f6b37df8fac1e5e72589a6f93e62478d65f60820ea8f192d42696758af48",
        [new("pret:TRAINER_PAINTER_EDNA")] = "46104a8e77b3d2ca86bfa5a7454f451fbc0f8dab62ee52d5fddca480fdf0b4bf",
        [new("pret:TRAINER_GENTLEMAN_CLIFFORD")] = "14aa45c71439588125834cb16eb685e3cddcc815c5d5f5173d9a5fb47293095c",
        [new("pret:TRAINER_PSYCHIC_DARIO")] = "fe117012d5779c3469d93e07502e1d0918135b8ecc8bdfe1fc161fc72ab0e846",
        [new("pret:TRAINER_PSYCHIC_RODETTE")] = "31ab0646cfb2f0314ba2e411a5cc2a17f7449de232a8556c62d8f1dd3b0f15de",
    }.ToImmutableDictionary();
    public static readonly ImmutableDictionary<string, string> BattleHashes = new Dictionary<string, string> {
        ["SevenIsland_SevaultCanyon_EventScript_Cyndy"] = "d23e45fc820e155c1c6cc951086d4c5b21f32c027972c93a5c2b48730245e8d1",
        ["SevenIsland_SevaultCanyon_EventScript_Evan"] = "4e2717166c2fd8f22b353137dc381274c55b9879dea6691e0087505a6e07f676",
        ["SevenIsland_SevaultCanyon_EventScript_Jackson"] = "7cb087d71b7f79cd497f988cade2336fab94b67d8f4687701e4f4f21dbcb1239",
        ["SevenIsland_SevaultCanyon_EventScript_Katelyn"] = "e86c570e3ac344b491a9a58ada03a80ef7a17766938bed7866674cedceed9620",
        ["SevenIsland_SevaultCanyon_EventScript_Leroy"] = "9317f3a5151a78f3bead6480bbb2d733fb2660a24caa23cc768b437a9951ad0c",
        ["SevenIsland_SevaultCanyon_EventScript_Michelle"] = "8be6ce0ffb60b608e109b151de527b554b883b718c8e1d5db5afa065ad0891eb",
        ["SevenIsland_SevaultCanyon_EventScript_Lex"] = "23c5d91efc9dffbe62df9a1c0a4eaaf45ed89a4a47f0b4621d20b9016577438d",
        ["SevenIsland_SevaultCanyon_EventScript_Nya"] = "bdfb0fbc66b5419335f94d2ca5cd2ea096a457e248df9d7a2a0cf5ab98c38105",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Miah"] = "d5e2ae9a52c6384d32407a5dc7cce7f5c9ffb84bb1c0f0a8c4fd67d0f36dcd27",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Eve"] = "d512315470903ad34784253d8b00ca4c3f5ba2feb579c33b96053432761b27cf",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Jon"] = "4b4a784881af781ca8d9dc94e04c1a091a179025424e0e164ce3514aed94767f",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Nicolas"] = "aa13b999d4723c6a78b286d418807d109a1a26c3f25fce95187173e70d1997a3",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Madeline"] = "11ee8da85457c31c1e1a4fca17b2da88c457ba56fe1085066896e245f99bcbaf",
        ["SevenIsland_SevaultCanyon_Entrance_EventScript_Mason"] = "3b2ed682d4decd12a16d58cfe15ec7141b4eb686b584a8e5a8d8018669292ac1",
        ["SevenIsland_TanobyRuins_EventScript_Brandon"] = "8c6bc22b4204ac313bce60260f1e9c191e9238f27d0a2ec3b4535822e5e38905",
        ["SevenIsland_TanobyRuins_EventScript_Benjamin"] = "e52b77d28c3a6410e79affd96d53642a455bad9933df387c730db90d4e6f1294",
        ["SevenIsland_TanobyRuins_EventScript_Edna"] = "26ccf1f5e0056d848cd0f4a4cf04b61b7cf3d2a46cd2bcdd94419e7b389ba875",
        ["SevenIsland_TanobyRuins_EventScript_Clifford"] = "6be96bf88ba5b59546702861959fea7694afd4fc38069cfbe35d4d85bccfe068",
        ["SevenIsland_TrainerTower_EventScript_Dario"] = "7bc6e3002f8d2a4eb663396f54d9c6b2a1340eceb926fab840c4837ed5c2711c",
        ["SevenIsland_TrainerTower_EventScript_Rodette"] = "8d73d02500711bb964fbdd6fa7344f836509b97ffec6762f05d28639f05d449e",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_CRUSH_GIRL_CYNDY"), new("pret:TRAINER_TAMER_EVAN"), new("pret:TRAINER_PKMN_RANGER_JACKSON"), new("pret:TRAINER_PKMN_RANGER_KATELYN"), new("pret:TRAINER_COOLTRAINER_LEROY"), new("pret:TRAINER_COOLTRAINER_MICHELLE"), new("pret:TRAINER_COOL_COUPLE_LEX_NYA"), new("pret:TRAINER_AROMA_LADY_MIAH"), new("pret:TRAINER_YOUNG_COUPLE_EVE_JON"), new("pret:TRAINER_PKMN_RANGER_NICOLAS"), new("pret:TRAINER_PKMN_RANGER_MADELINE"), new("pret:TRAINER_JUGGLER_MASON"), new("pret:TRAINER_RUIN_MANIAC_BRANDON"), new("pret:TRAINER_RUIN_MANIAC_BENJAMIN"), new("pret:TRAINER_PAINTER_EDNA"), new("pret:TRAINER_GENTLEMAN_CLIFFORD"), new("pret:TRAINER_PSYCHIC_DARIO"), new("pret:TRAINER_PSYCHIC_RODETTE")];
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    public static int BasePrize(SevenIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seven_island_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(SevenIslandTrainerCatalog trainers,
        SevenIslandOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SevenIslandOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seven_island_trainer_catalog");
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("seven_island_trainer_species");
        var source = opponents.Mechanics;
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // Custom move replacement is applied after CreateMon, without changing its PID or OT draws.
        return made with { Party = made.Party.Select((p, i) => trainer.Party[i].CustomMoves.IsEmpty ? p : p with {
            Pokemon = p.Pokemon with { Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SevenIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 18 || catalog.BattlesByScript.Count != 20)
            throw new NotSupportedException("seven_island_trainer_scope");
        foreach (var (id, hash) in TrainerHashes)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != hash) throw new NotSupportedException("seven_island_trainer_source");
        foreach (var (script, hash) in BattleHashes)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script ||
                CampaignSourceFingerprint.Compute(battle) != hash) throw new NotSupportedException("seven_island_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleHashes.TryGetValue(battle.ScriptEntry, out var hash) || CampaignSourceFingerprint.Compute(battle) != hash)
            throw new NotSupportedException("seven_island_trainer_battle_source");
    }
}
