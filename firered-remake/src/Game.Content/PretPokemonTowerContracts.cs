using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

// Source contracts only. These records do not register script handlers or activate a campaign.
public sealed record PretTowerCoordinateTrigger(MapId MapId, int EventIndex, int X, int Y, int Elevation,
    string VariableSymbol, int VariableId, int RequiredValue, string Script, SourceRef Source)
{
    public bool ElevationIsWildcard => Elevation == 0;
    public bool Matches(int x, int y, int elevation, int variableValue) =>
        X == x && Y == y && (ElevationIsWildcard || Elevation == elevation) && RequiredValue == variableValue;
}
public sealed record PretTowerPurifiedGround(MapId MapId, int TemporaryVariableId,
    ImmutableArray<(int X, int Y)> NoEncounterCells,
    ImmutableArray<PretTowerCoordinateTrigger> EntryTriggers, ImmutableArray<PretTowerCoordinateTrigger> ResetTriggers,
    bool RestoresPartyHpPpStatus, bool AppliesPpBonuses, bool UpdatesLastHealLocation, SourceRef HealSource);
public sealed record PretTowerFog(ImmutableArray<MapId> MapIds, int WeatherId, int FramesPerPixel,
    int InitialBlendA, int InitialBlendB, int TargetBlendA, int TargetBlendB, int BlendDelay,
    int SpriteGridColumns, int SpriteGridSpacing, int ScrollMask, bool DependsOnSilphScope,
    bool AppliesBattleAccuracyPenalty, string TexturePath, SourceRef Source);
public sealed record PretTowerMarowakGate(int SceneVariableId, int ActiveValue, int ClearedValue,
    int RequiredBattleOutcome, bool ClearsOnlyAfterVictoryDialogue, Direction SurvivingFailurePushDirection,
    bool LossWhitesOut, ImmutableArray<PretTowerCoordinateTrigger> Triggers, SourceRef OutcomeSource);
public sealed record PretTowerCoordinateWarp(MapId DestinationMapId, int WarpId, int X, int Y,
    bool ReplacesEscapeAnchor, SourceRef Source);
public sealed record PretTowerFujiRescue(ImmutableArray<int> SetFlags, ImmutableArray<int> ClearFlags,
    int InitialHiddenFlag, PretTowerCoordinateWarp Warp, MapId ExteriorMapId, int ExteriorWarpId,
    bool GrantsFluteOnTowerFloor, string Script, SourceRef Source);
public sealed record PretTowerFluteReward(MapId MapId, string Script, string ItemSymbol, int ItemConstant,
    int Quantity, int RewardFlag, bool ChecksCapacityBeforeFlag, bool Consumed,
    bool FieldWakesWholePlayerParty, bool BattleCuresSleepAndNightmare, bool BattleAffectsBothSides,
    bool BattleExcludesSoundproof, bool SpendsBattleAction, SourceRef Source);
public enum PretTowerGhostModeKind { DisguisedWild, OrdinaryWild, DisguisedMarowak, RevealedMarowak }
public sealed record PretTowerGhostMode(PretTowerGhostModeKind Kind, int BattleTypeFlags,
    bool DisguisesSpecies, bool CanAttackNormally, bool PokedexSeen, bool BallAlwaysDodges,
    bool PermittedBallThrowConsumesBall, bool PlayerRunAlwaysSucceeds, bool UnderlyingSpeciesFromWildTable);
public sealed record PretTowerScriptedMarowak(string SpeciesSymbol, int Level, string Gender, int FixedIv,
    string NatureSymbol, string HeldItemSymbol, ImmutableArray<string> MoveSymbols,
    bool PersonalityUsesFemaleSeriousRejectionSampling, bool AbilityUsesPersonalitySlot);
public sealed record PretTowerPickup(MapId MapId, int? LocalId, int? BackgroundEventIndex, int X, int Y,
    int Elevation, string ItemSymbol, int Quantity, string FlagSymbol, int FlagId,
    bool Hidden, bool Underfoot, bool RequiresItemfinder, string? Script, SourceRef Source)
{
    // Underfoot items cannot be admitted through ordinary adjacent A interaction.
    public bool AllowsOrdinaryInteraction => !Underfoot;
}
public sealed record PretTowerRivalBranch(int StarterVariableValue, string TrainerSymbol, int TrainerConstant);
