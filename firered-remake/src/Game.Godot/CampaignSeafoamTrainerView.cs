using Game.Domain.Trainers;
using Godot;

public partial class CampaignView
{
    private static string SeafoamTrainerText(TrainerDefinition trainer, string key) => key switch
    {
        "name" => trainer.SourceTrainerName,
        "className" => trainer.TrainerClass switch
        {
            "TRAINER_CLASS_SWIMMER_M" or "TRAINER_CLASS_SWIMMER_F" => "SWIMMER",
            "TRAINER_CLASS_PICNICKER" => "PICNICKER",
            "TRAINER_CLASS_BIRD_KEEPER" => "BIRD KEEPER",
            "TRAINER_CLASS_SIS_AND_BRO" => "SIS AND BRO",
            _ => throw new InvalidDataException("seafoam_trainer_class")
        },
        "portrait" => trainer.TrainerPic switch
        {
            "TRAINER_PIC_SWIMMER_M" => "swimmer-m-front",
            "TRAINER_PIC_SWIMMER_F" => "swimmer-f-front",
            "TRAINER_PIC_PICNICKER" => "picnicker-front",
            "TRAINER_PIC_BIRD_KEEPER" => "bird-keeper-front",
            "TRAINER_PIC_SIS_AND_BRO" => "sis-and-bro-front",
            _ => throw new InvalidDataException("seafoam_trainer_portrait")
        },
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private string? VisibleSeafoamTrainerText(string key) => _session.State.ForestTrainerBattle is { } battle &&
        _catalog.SeafoamTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
            ? SeafoamTrainerText(trainer, key) : null;
    private string? SeafoamDoubleTrainerText(string key) => _session.State.DoubleBattle is { } battle &&
        _catalog.SeafoamTrainers?.Trainers.TryGetValue(battle.Trainer, out var trainer) == true
            ? SeafoamTrainerText(trainer, key) : null;
    private Texture2D? SeafoamDoubleTrainerPortrait => SeafoamDoubleTrainerText("portrait") is { } id
        ? _forestTrainerPortraits[id] : null;
}
