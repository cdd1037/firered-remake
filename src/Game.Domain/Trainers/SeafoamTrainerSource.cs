using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;

// Generated from the 21 pinned Route19/20 first-battle teams and 22 source actor entries.
public static partial class SeafoamTrainerPartyFactory
{
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_SWIMMER_MALE_REECE"),
        new("pret:TRAINER_SWIMMER_MALE_RICHARD"),
        new("pret:TRAINER_SWIMMER_MALE_TONY"),
        new("pret:TRAINER_SWIMMER_MALE_MATTHEW"),
        new("pret:TRAINER_SWIMMER_MALE_DOUGLAS"),
        new("pret:TRAINER_SWIMMER_MALE_DAVID"),
        new("pret:TRAINER_SWIMMER_MALE_AXLE"),
        new("pret:TRAINER_SWIMMER_FEMALE_ALICE"),
        new("pret:TRAINER_SWIMMER_FEMALE_CONNIE"),
        new("pret:TRAINER_SWIMMER_FEMALE_ANYA"),
        new("pret:TRAINER_SIS_AND_BRO_LIA_LUC"),
        new("pret:TRAINER_SWIMMER_FEMALE_MELISSA"),
        new("pret:TRAINER_PICNICKER_MISSY"),
        new("pret:TRAINER_SWIMMER_FEMALE_NORA"),
        new("pret:TRAINER_BIRD_KEEPER_ROGER"),
        new("pret:TRAINER_SWIMMER_MALE_DEAN"),
        new("pret:TRAINER_PICNICKER_IRENE"),
        new("pret:TRAINER_SWIMMER_MALE_BARRY"),
        new("pret:TRAINER_SWIMMER_MALE_DARRIN"),
        new("pret:TRAINER_SWIMMER_FEMALE_SHIRLEY"),
        new("pret:TRAINER_SWIMMER_FEMALE_TIFFANY")]);
    public static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "Route19_EventScript_Reece",
        "Route19_EventScript_Richard",
        "Route19_EventScript_Tony",
        "Route19_EventScript_Matthew",
        "Route19_EventScript_Douglas",
        "Route19_EventScript_David",
        "Route19_EventScript_Axle",
        "Route19_EventScript_Alice",
        "Route19_EventScript_Connie",
        "Route19_EventScript_Anya",
        "Route19_EventScript_Lia",
        "Route19_EventScript_Luc",
        "Route20_EventScript_Melissa",
        "Route20_EventScript_Missy",
        "Route20_EventScript_Nora",
        "Route20_EventScript_Roger",
        "Route20_EventScript_Dean",
        "Route20_EventScript_Irene",
        "Route20_EventScript_Barry",
        "Route20_EventScript_Darrin",
        "Route20_EventScript_Shirley",
        "Route20_EventScript_Tiffany");
}
