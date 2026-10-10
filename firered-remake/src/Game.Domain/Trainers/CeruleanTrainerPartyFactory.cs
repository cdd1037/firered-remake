using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Seven pinned Cerulean parties: three ordinary trainers, Misty, and three starter branches.</summary>
public static class CeruleanTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-cerulean-trainers-v1";
    public static TrainerId Misty { get; } = new("pret:TRAINER_LEADER_MISTY");
    public static TrainerId Diana { get; } = new("pret:TRAINER_PICNICKER_DIANA");
    public static TrainerId Luis { get; } = new("pret:TRAINER_SWIMMER_MALE_LUIS");
    public static TrainerId Rocket { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_5");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] { "LEADER_MISTY", "PICNICKER_DIANA",
        "SWIMMER_MALE_LUIS", "RIVAL_CERULEAN_SQUIRTLE", "RIVAL_CERULEAN_BULBASAUR", "RIVAL_CERULEAN_CHARMANDER",
        "TEAM_ROCKET_GRUNT_5" }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch {
        "pret:SPECIES_BULBASAUR" => SupportedTrainers[3], "pret:SPECIES_CHARMANDER" => SupportedTrainers[4],
        "pret:SPECIES_SQUIRTLE" => SupportedTrainers[5], _ => throw new NotSupportedException("Unknown original starter.") };

    public static TrainerPartyCreationResult Create(CeruleanTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (rockTunnelRules ? 93 : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : 65) || !trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Cerulean requires its complete sixty-five-species catalog and a source trainer.");
        foreach (var member in trainer.Party)
        {
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !CeruleanTrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Cerulean trainer species differs from source.");
            var natural = PokemonMoveRules.InitialMoves(pokemon, species, member.Level).Select(m => m.Move.Value);
            if (trainer.PartyFlags == 0 && !natural.SequenceEqual(NaturalMoves(member.SourceSpeciesId).Select(m => "pret:MOVE_" + m)))
                throw new NotSupportedException("Cerulean natural learnset differs from the source party.");
            foreach (var id in trainer.PartyFlags == 0 ? NaturalMoves(member.SourceSpeciesId).Select(m => new MoveId("pret:MOVE_" + m)) : member.CustomMoves.Where(m => m.Value != "pret:MOVE_NONE"))
                if (!pokemon.Moves.TryGetValue(id, out var move) || !Game.Domain.WildBattle.CeruleanBattleScope.ValidDefinition(move))
                    throw new NotSupportedException("Cerulean custom move differs from source.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member, i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(CeruleanTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown Cerulean source/profile or full-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 7 || catalog.Battles.Count != 7)
            throw new ArgumentException("All seven bounded Cerulean trainers are required.");
        foreach (var id in SupportedTrainers)
        {
            var e = Expected(id); var identity = Identity(e.Id);
            if (!catalog.Trainers.TryGetValue(id, out var t) || t is null || t.Id != id || t.SourceTrainerId != e.Id ||
                t.SourceTrainerName != e.Name || !Encoded(e.Name, t.EncodedTrainerName) ||
                t.TrainerClass != "TRAINER_CLASS_" + identity.Class || t.SourceTrainerClass != identity.ClassId ||
                t.TrainerPic != "TRAINER_PIC_" + identity.Pic || t.SourceTrainerPic != identity.PicId ||
                t.EncounterMusicGender != identity.Gender || t.DoubleBattle || t.PartyFlags != e.Flags ||
                t.AiFlags != (e.Flags == 1 ? 7u : 1u) || t.BaseMoney != identity.Money || t.Items.IsDefault ||
                !t.Items.SequenceEqual(new[] { id == Misty ? "ITEM_SUPER_POTION" : "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                t.AiFlagSymbols.IsDefault || !t.AiFlagSymbols.SequenceEqual(e.Flags == 1 ?
                    new[] { "AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_TRY_TO_FAINT", "AI_SCRIPT_CHECK_VIABILITY" } : ["AI_SCRIPT_CHECK_BAD_MOVE"]))
                throw new NotSupportedException("Cerulean trainer identity/parameters differ from the pinned source.");
            if (t.Party.IsDefault || t.Party.Length != e.Party.Length) throw new ArgumentException("Cerulean party length differs from source.");
            for (int i = 0; i < e.Party.Length; i++)
            {
                var m = t.Party[i]; var p = e.Party[i]; string symbol = SpeciesSymbol(p.Species);
                if (m is null || m.Species.Value != "pret:SPECIES_" + symbol || m.SourceSpeciesId != p.Species || m.SpeciesName != symbol ||
                    !Encoded(symbol, m.EncodedSpeciesName) || m.Level != p.Level || m.SourceIv != p.Iv || m.HeldItem != "ITEM_NONE" ||
                    m.CustomMoves.IsDefault || !m.CustomMoves.Select(x => x.Value).SequenceEqual(p.Moves.Select(x => "pret:MOVE_" + x)))
                    throw new NotSupportedException("Cerulean party differs from source custom/default moves, levels or IVs.");
            }
            if (!catalog.Battles.TryGetValue(id, out var b) || b is null || b.Trainer != id)
                throw new ArgumentException("Missing Cerulean battle declaration.");
            ValidateBattle(b);
        }
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        ArgumentNullException.ThrowIfNull(b); var e = Expected(b.Trainer);
        bool rival = e.Id is >= 332 and <= 334, grunt = b.Trainer == Rocket, misty = b.Trainer == Misty;
        string map = rival || grunt ? "CeruleanCity" : "CeruleanCity_Gym";
        string name = rival ? "Rival" : grunt ? "Grunt" : e.Name[..1] + e.Name[1..].ToLowerInvariant();
        string suffix = rival ? e.Id switch { 332 => "Squirtle", 333 => "Bulbasaur", _ => "Charmander" } : "";
        if (b.Mode != (rival || grunt ? 3 : misty ? 1 : 0) || b.RivalFlags != 0 || b.BattleTypeFlags != 8 ||
            b.ScriptEntry != map + "_EventScript_" + name + suffix || b.IntroText != map + "_Text_" + name + "Intro" ||
            b.DefeatText != map + "_Text_" + name + "Defeat" || b.VictoryText != "" ||
            b.PostBattleText != (misty ? "CeruleanCity_Gym_Text_ExplainCascadeBadge" : grunt ? "CeruleanCity_Text_RecoveredTM28FromGrunt" : map + "_Text_" + name + "PostBattle") ||
            b.EntryScript != (rival || grunt ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback != "CB2_EndTrainerBattle" || b.PostBattleScript != (misty ? "CeruleanCity_Gym_EventScript_MistyDefeated" : null) ||
            b.MusicOption != (misty ? "NO_MUSIC" : null)) throw new NotSupportedException("Cerulean battle declaration differs from source: " + b);
    }

    private static bool Encoded(string text, ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(text.Select(c => 0xbb + c - 'A'));
    private static (string Class,int ClassId,string Pic,int PicId,int Gender,int Money) Identity(int id) => id switch {
        415 => ("LEADER",84,"LEADER_MISTY",117,129,25), 150 => ("PICNICKER",62,"PICNICKER",87,130,5),
        234 => ("SWIMMER_M",70,"SWIMMER_M",95,8,1), 332 or 333 or 334 => ("RIVAL_EARLY",81,"RIVAL_EARLY",106,0,4),
        355 => ("TEAM_ROCKET",85,"ROCKET_GRUNT_M",109,6,8), _ => throw new NotSupportedException("Unknown Cerulean trainer.") };
    internal static string SpeciesSymbol(int id) => id switch { 1=>"BULBASAUR",4=>"CHARMANDER",7=>"SQUIRTLE",17=>"PIDGEOTTO",
        19=>"RATTATA",63=>"ABRA",66=>"MACHOP",90=>"SHELLDER",96=>"DROWZEE",116=>"HORSEA",118=>"GOLDEEN",120=>"STARYU",121=>"STARMIE",
        _=>throw new NotSupportedException("Unknown Cerulean party species.") };
    internal static (int Id,string Name,int Flags,(int Species,int Level,int Iv,string[] Moves)[] Party) Expected(TrainerId id) => id.Value switch {
        "pret:TRAINER_LEADER_MISTY" => (415,"MISTY",1,[(120,18,0,["TACKLE","HARDEN","RECOVER","WATER_PULSE"]),(121,21,0,["SWIFT","RECOVER","RAPID_SPIN","WATER_PULSE"])]),
        "pret:TRAINER_PICNICKER_DIANA" => (150,"DIANA",0,[(118,19,0,[])]),
        "pret:TRAINER_SWIMMER_MALE_LUIS" => (234,"LUIS",0,[(116,16,0,[]),(90,16,0,[])]),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_5" => (355,"GRUNT",0,[(66,17,0,[]),(96,17,0,[])]),
        "pret:TRAINER_RIVAL_CERULEAN_SQUIRTLE" => (332,"TERRY",1,Rival(7,["TACKLE","TAIL_WHIP","WITHDRAW","WATER_GUN"])),
        "pret:TRAINER_RIVAL_CERULEAN_BULBASAUR" => (333,"TERRY",1,Rival(1,["SLEEP_POWDER","POISON_POWDER","VINE_WHIP","LEECH_SEED"])),
        "pret:TRAINER_RIVAL_CERULEAN_CHARMANDER" => (334,"TERRY",1,Rival(4,["METAL_CLAW","EMBER","GROWL","SCRATCH"])),
        _ => throw new NotSupportedException("Unknown Cerulean trainer.") };
    internal static string[] NaturalMoves(int species) => species switch {
        118 => ["TAIL_WHIP","WATER_SPORT","SUPERSONIC","HORN_ATTACK"],
        116 => ["BUBBLE","SMOKESCREEN","LEER"], 90 => ["TACKLE","WITHDRAW","ICICLE_SPEAR","SUPERSONIC"],
        66 => ["LOW_KICK","LEER","FOCUS_ENERGY","KARATE_CHOP"],
        96 => ["HYPNOSIS","DISABLE","CONFUSION","HEADBUTT"],
        _ => throw new NotSupportedException("Not a source Cerulean default-move member.") };
    private static (int Species,int Level,int Iv,string[] Moves)[] Rival(int starter,string[] moves) => [
        (17,17,50,["TACKLE","SAND_ATTACK","GUST","QUICK_ATTACK"]),(63,16,50,["TELEPORT","NONE","NONE","NONE"]),
        (19,15,50,["TACKLE","TAIL_WHIP","QUICK_ATTACK","NONE"]),(starter,18,100,moves)];
}
