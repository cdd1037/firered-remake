using Game.Domain.Trainers;
using Godot;

public partial class CampaignView
{
    private static string SeafoamTrainerPortraitId(TrainerDefinition trainer) => trainer.TrainerPic switch
    {
        "TRAINER_PIC_SWIMMER_M" => "swimmer-m-front",
        "TRAINER_PIC_SWIMMER_F" => "swimmer-f-front",
        "TRAINER_PIC_PICNICKER" => "picnicker-front",
        "TRAINER_PIC_BIRD_KEEPER" => "bird-keeper-front",
        "TRAINER_PIC_SIS_AND_BRO" => "sis-and-bro-front",
        _ => throw new InvalidDataException("seafoam_trainer_portrait")
    };

    private string? VisibleSeafoamTrainerPortraitId => _session.State.ForestTrainerBattle is { } battle &&
        _catalog.SeafoamTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
            ? SeafoamTrainerPortraitId(trainer) : null;
    private Texture2D? SeafoamDoubleTrainerPortrait => _session.State.DoubleBattle is { } battle &&
        _catalog.SeafoamTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
            ? _forestTrainerPortraits[SeafoamTrainerPortraitId(trainer)] : null;
}
