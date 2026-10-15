using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record TrainerSourceRef(string Path, string Symbol, int StartLine, int EndLine);

/// <summary>Source iv is the trainer-table 0..255 difficulty field, not a per-stat IV.</summary>
public sealed record TrainerPartyMemberDefinition(SpeciesId Species, int SourceSpeciesId, string SpeciesName,
    ImmutableArray<int> EncodedSpeciesName, int Level, int SourceIv, string HeldItem,
    ImmutableArray<MoveId> CustomMoves, TrainerSourceRef Source, TrainerSourceRef SpeciesNameSource);

/// <summary>Extensible source metadata. This profile executes only no-item/default-move lab parties.</summary>
public sealed record TrainerDefinition(TrainerId Id, int SourceTrainerId, string TrainerClass, int SourceTrainerClass,
    string TrainerPic, int SourceTrainerPic, string SourceTrainerName, ImmutableArray<int> EncodedTrainerName,
    int EncounterMusicGender, ImmutableArray<string> Items, bool DoubleBattle, int PartyFlags,
    uint AiFlags, ImmutableArray<string> AiFlagSymbols, int BaseMoney,
    ImmutableArray<TrainerPartyMemberDefinition> Party, TrainerSourceRef Source, TrainerSourceRef MoneySource);

/// <summary>Speech labels are from the trainer's perspective: defeat text when the player wins.</summary>
public sealed record EarlyRivalBattleDefinition(TrainerId Trainer, int Mode, int RivalFlags, uint BattleTypeFlags,
    string DefeatText, string VictoryText, string EntryScript, string Callback, string PostBattleScript,
    TrainerSourceRef Source);

public sealed record TrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, EarlyRivalBattleDefinition> Battles);

public sealed record TrainerPartyPokemon(PokemonInstance Pokemon, string HeldItem);
public sealed record TrainerPartyCreationResult(TrainerDefinition Trainer, ImmutableArray<TrainerPartyPokemon> Party,
    RngState Rng, ImmutableArray<RandomTrace> Trace)
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal SourceTrainerPartyReceipt? Receipt { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal Game.Domain.Battle.SourceTrainerTowerPartyReceipt? TowerReceipt { get; init; }
}

// Only terminal outcomes reachable in this bounded trainer battle are accepted.
public enum EarlyRivalOutcome { PlayerWon = 1, PlayerLost = 2, Draw = 3 }
public sealed record EarlyRivalReturn(int SpecialVarResult, bool HealBeforeResume, bool SetBattledTrainerFlag,
    bool ContinuePostBattleScript, bool WhiteOut, string PostBattleScript);

public static class EarlyRivalCallback
{
    /// <summary>Pure callback decisions only. The later lab script also heals after every outcome.</summary>
    public static EarlyRivalReturn Resolve(EarlyRivalBattleDefinition battle, EarlyRivalOutcome outcome)
    {
        TrainerPartyFactory.ValidateBattle(battle);
        if (!Enum.IsDefined(outcome)) throw new NotSupportedException("Only won/lost/draw terminal outcomes are supported.");
        bool defeated = outcome is EarlyRivalOutcome.PlayerLost or EarlyRivalOutcome.Draw;
        return new(defeated ? 1 : 0, defeated, true, true, false, battle.PostBattleScript);
    }
}
