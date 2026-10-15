using Game.Domain.Trainers;
using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Options;
using Game.Domain.World;
using System.Text.Json.Serialization;
namespace Game.Application;
public sealed record CampaignActor(string LocalId,int SourceLocalId,string GraphicsId,int X,int Y,int Elevation,Direction Facing,string MovementType,string ScriptId,string HideFlag,bool Active,bool Invisible,int TemplateX,int TemplateY)
{ public bool Visible=>Active&&!Invisible; }
public sealed record CampaignDialogue(string TextSymbol,ImmutableArray<string> Pages,int Page,bool IsChoice,bool CloseOnCompletion);
public sealed record CampaignMotion(string ActorId,ImmutableArray<TilePosition> Path,ImmutableArray<string> Actions);
public sealed record CampaignPresentation(long Sequence,string Opcode,MapId MapId,ImmutableArray<string> Args);
public sealed record CampaignMonPicture(int SourceSpeciesId,int X,int Y);
public sealed record CampaignNickname(int PartyIndex,string OriginalName,bool Submitted)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PokemonStorageAddress? BoxAddress{get;init;}
}
public sealed record CampaignSignMessage(bool Cancelable=false,bool WalkawayDisabled=false,int InhibitTicks=0,bool SignStyle=false);
public sealed record CampaignNewGameOptions(uint TrainerId=0,string PlayerName="RED",string RivalName="BLUE",int PlayerGender=0,uint RngSeed=1);
public sealed record CampaignState(WorldSnapshot World,StoryState Script,ImmutableSortedDictionary<string,CampaignActor> Actors,
    ImmutableArray<string> EntryPrograms,Direction? ArrivalFacing,string? LastTalked,CampaignDialogue? Dialogue,
    ImmutableArray<CampaignMotion> Motions,int PresentationTicks,string LastHealingLocation,string TextColor,string Music,
    string PlayerName,string RivalName,int Money,string SavedMusic,ImmutableArray<CampaignPresentation> PresentationEffects,bool PlayerVisible)
{
    [JsonRequired] public CampaignPlayTime PlayTime{get;init;}=CampaignPlayTime.NewGame;
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? RegisteredItem{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignTeachyTv? TeachyTv{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignFameChecker? FameChecker{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerProgress? VsSeekerProgress{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerEffect? VsSeekerEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignItemfinder? Itemfinder{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPokeFluteEffect? PokeFlute{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSafariVisit? SafariVisit{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSafariBattle? SafariBattle{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool SafariExitPending{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int FlashLevel{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignFlashEffect? FlashEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignStrengthEffect? StrengthEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignDeoxysRockEffect? DeoxysRockEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignTanobyKeyVisit? TanobyKeyVisit{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignTanobyCompletion? TanobyCompletion{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? FieldWeather{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool IsCycling{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool IsSurfing{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSurfEffect? SurfEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignFishing? Fishing{get;init;}
    [JsonRequired] public int Coins { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSlotMachine? SlotMachine { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCoinsBox? CoinsBox { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignPrizePokemon>? PrizePokemon { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPrizeExchange? PrizeExchange { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMoneyBox? MoneyBox{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMagikarpGift? MagikarpGift{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEeveeGift? EeveeGift{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignLaprasGift? LaprasGift{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignDojoGift? DojoGift{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignFossilRevival>? FossilRevivals{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignNpcTradeReceipt>? NpcTrades{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SourceMailState? Mail{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMailInteraction? MailInteraction{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMailComposer? MailComposer{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignFossilMenu? FossilMenu{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSilphElevator? SilphElevator{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCinnabarFall? CinnabarFall{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSeagallopTransition? Seagallop{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSeagallopMenu? SeagallopMenu{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignLostelleHypnoPreparation? LostelleHypnoPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSilphPad? SilphPad{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignNameRater? NameRater{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignNpcTrade? NpcTrade{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMoveTutor? MoveTutor{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignUltimateTutor? UltimateTutor{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMoveReminder? MoveReminder{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMoveDeleter? MoveDeleter{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMagikarpRecord? MagikarpRecord{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignDepartmentElevator? DepartmentElevator{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignGiftDelivery? GiftDelivery{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public EscapeRopeDestination? EscapeDestination{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEscapeRope? EscapeRope{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRareCandy? RareCandy{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPokemonLeagueProgress? PokemonLeague{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignHallOfFameArchive? HallOfFame{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPokemonLeagueCompletion? PokemonLeagueCompletion{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public int NationalDexMagic{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public IcefallIceState? IcefallIce{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRuinValleyCut? RuinValleyCut{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignDottedHoleFall? DottedHoleFall{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSeafoamMovement? SeafoamMovement{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignFourIslandMovement? FourIslandMovement{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public Direction? FourIslandMovementDirection{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignCelioHandoff? CelioHandoff{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.Roaming.RoamerState? Roamer{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public byte SpecialSaveWarpFlags{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public TrainerTowerProgressState? TrainerTower{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignDepartmentElevator? TrainerTowerElevator{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignTrainerTowerHistory>? TrainerTowerHistory{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignTrainerTowerMenu? TrainerTowerMenu{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool TrainerTowerRecordsVisible{get;init;}
    public uint TrainerId{get;init;}
    public int PlayerGender{get;init;}
    public ImmutableArray<PartyPokemon> Party{get;init;}=[];
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public PokemonStorageState? Storage{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public SourceGameOptions? Options{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignPcState? Pc{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignFieldPickup? FieldPickup{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CampaignFieldItemService? FieldItemService { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignRepel? Repel{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignTownMap? TownMap{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignFlyEffect? FlyEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public string? OakRatingHelpContextBackup{get;init;}
    public ImmutableSortedSet<int> SeenSpecies{get;init;}=ImmutableSortedSet<int>.Empty;
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public uint? UnownPokedexPersonality { get; init; }
    public ImmutableSortedSet<int> CaughtSpecies{get;init;}=ImmutableSortedSet<int>.Empty;
    public ImmutableSortedDictionary<string,string> TextBuffers{get;init;}=ImmutableSortedDictionary<string,string>.Empty.WithComparers(StringComparer.Ordinal);
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignRocketElevator? RocketElevator{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignRocketSpin? RocketSpin{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public Route5DayCareState? Route5DayCare{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRoute5DayCare? DayCareInteraction{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public FourIslandDayCareState? FourIslandDayCare{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignDayCareEggReceipt>? DayCareEggs{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<CampaignGiftEggReceipt>? GiftEggs{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEggHatch? EggHatch{get;init;}
    // Final snapshots of released identities; absence preserves existing saves byte-for-byte.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<PartyPokemon>? ReleasedPokemon{get;init;}
    public long NextPokemonId{get;init;}=1;
    public CampaignMonPicture? MonPicture{get;init;}
    public CampaignNickname? Nickname{get;init;}
    public bool ScreenIsBlack{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool ScreenIsWhite{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.Encounters.Route1WildPokemonResult? TowerWildPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSnorlaxPreparation? SnorlaxPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEventIslandPreparation? EventIslandPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignEventIslandReturn? EventIslandReturn{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignArticunoPreparation? ArticunoPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? ArticunoReturn{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPowerPlantPreparation? PowerPlantPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignPowerPlantReturn? PowerPlantReturn{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignMewtwoPreparation? MewtwoPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MewtwoReturn{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSnorlaxReturn? SnorlaxReturn{get;init;}
    public CampaignLabBattle? Battle{get;init;}
    public InventoryState? Inventory{get;init;}
    public CampaignItemMenu? ItemMenu{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignShop? Shop{get;init;}
    public bool PcPoweredOn{get;init;}
    public string HelpContext{get;init;}="map";
    public long NextBattleId{get;init;}=1;
    public ImmutableSortedSet<string> SettledBattles{get;init;}=ImmutableSortedSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    public ImmutableSortedSet<string> BattledTrainers{get;init;}=ImmutableSortedSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    public ImmutableArray<CampaignBattleHistory> BattleHistory{get;init;}=[];
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignExploration? Exploration{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignWildBattle? WildBattle{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignSignMessage? SignMessage{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public string? MessagePointer{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<string,FieldMetatileEdit>? MapEdits{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public ushort GcnLinkFlags{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignHealingEffect? HealingEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignCutEffect? CutEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRockSmashEffect? RockSmashEffect{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignRockSmashPreparation? RockSmashPreparation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<int,int>? FieldEffectArguments{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<string,int>? GameStats{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<int,string>? ObjectSlots{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public int SelectedObjectSlot{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignOldManTutorial? OldManTutorial{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public bool OldManCompleted{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignFieldPoison? FieldPoison{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignEvolution? Evolution{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<CampaignEvolutionHistory>? EvolutionHistory{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignForestTrainerBattle? ForestTrainerBattle{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CampaignDoubleBattle? DoubleBattle{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<CampaignDoubleBattleHistory>? DoubleBattleHistory{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<CampaignForestTrainerHistory>? ForestTrainerHistory{get;init;}
}
public sealed record CampaignCommand(string Kind,Direction Direction=Direction.South,int? Choice=null,string? Text=null,int MoveSlot=-1,int Quantity=1,int PartyIndex=0)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public SourceGameOptions? Options{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignSlotInput? SlotInput { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public long? TargetInstanceId{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PokemonStorageMoveLocation? StorageLocation{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? StorageBoxIndex{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? HeldInstanceId{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? HeldItemId{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? TargetHeldItemId{get;init;}
}
/// <summary>Source-driven event composition; all rule state and effects commit through the existing command session.</summary>
public sealed partial class CampaignSession
{
    public const string Rules="fr-pallet-campaign-items-v3";
    public const string RegionRules="fr-route1-campaign-v1";
    public const string ParcelRules="fr-route1-parcel-campaign-v1";
    public const string CaptureRules="fr-party-capture-campaign-v1";
    public const string CenterRules="fr-viridian-center-campaign-v1";
    public const string TutorialRules="fr-old-man-tutorial-campaign-v1";
    public const string PartyTrainingRules="fr-party-training-campaign-v1";
    public const string ForestRules="fr-forest-campaign-v1";
    public const string ForestTrainerRules="fr-forest-trainers-campaign-v1";
    public const string PewterRules="fr-pewter-campaign-v2";
    public const string EarlyShopRulesVersion="fr-pewter-shops-campaign-v2";
    public const string EarlyStarterRulesVersion="fr-early-starter-campaign-v1";
    public const string EarlyPartyRulesVersion="fr-early-party-campaign-v1";
    public const string PokemonStorageRulesVersion="fr-pokemon-storage-campaign-v1";
    public const string FieldPickupRulesVersion="fr-field-pickups-campaign-v1";
    public const string RepelRulesVersion="fr-repel-campaign-v1";
    public const string TownMapRulesVersion="fr-town-map-campaign-v1";
    public const string OakRatingRulesVersion="fr-oak-rating-campaign-v1";
    public const string PokemonTowerRulesVersion="fr-pokemon-tower-campaign-v1";
    public const string RocketHideoutRulesVersion="fr-rocket-hideout-campaign-v1";
    public const string CeladonGymRulesVersion="fr-celadon-gym-campaign-v1";
    public const string CeladonApproachRulesVersion="fr-celadon-approach-campaign-v1";
    public const string RockTunnelRulesVersion="fr-rocktunnel-campaign-v1";
    public const string Route11RulesVersion="fr-route11-campaign-v1";
    public const string CityServicesRulesVersion="fr-city-services-campaign-v1";
    public const string VermilionGymRulesVersion="fr-vermilion-gym-campaign-v1";
    public const string SsAnneRulesVersion="fr-ssanne-campaign-v1";
    public const string VermilionRulesVersion="fr-vermilion-campaign-v1";
    public const string BillRulesVersion="fr-bill-campaign-v1";
    public const string CeruleanRulesVersion="fr-cerulean-campaign-v1";
    public const string MtMoonRulesVersion="fr-mtmoon-campaign-v1";
    public const string MagikarpRulesVersion="fr-route4-magikarp-campaign-v1";
    public const string Route3RulesVersion="fr-route3-campaign-v1";
    public const string Route22RulesVersion="fr-route22-campaign-v1";
    public const string FuchsiaRouteRulesVersion="fr-fuchsia-route-campaign-v1";
    public const string FuchsiaGymRulesVersion="fr-fuchsia-gym-campaign-v1";
    public const string SnorlaxRulesVersion="fr-snorlax-campaign-v1";
    public const string PresetDoublesRulesVersion="fr-preset-doubles-campaign-v1";
    public const string SafariRulesVersion="fr-safari-campaign-v1";
    public const string VictoryRoadRulesVersion="fr-victory-road-campaign-v1";
    public const string LeagueRivalRulesVersion="fr-league-rival-campaign-v1";
    public const string ViridianGymRulesVersion="fr-viridian-gym-campaign-v1";
    public const string FirstSeviiRulesVersion="fr-first-sevii-campaign-v1";
    public const string CinnabarRulesVersion="fr-cinnabar-campaign-v1";
    public const string SaffronGymRulesVersion="fr-saffron-gym-campaign-v1";
    public const string SilphRulesVersion="fr-silph-campaign-v1";
    public const string SurfRulesVersion="fr-surf-campaign-v1";
    public string RulesVersion=>_binding.RulesVersion;
    public bool IsCurrentGame=>true;
    private readonly CampaignCatalogBinding _binding;
    private readonly ICampaignCatalog _content;
    private readonly CommandSession<CampaignState,CampaignCommand> _commands;
    public CampaignState State=>_commands.State;
    public long Revision=>_commands.Revision;
    public string? VisibleText
    {
        get
        {
            if(State.Dialogue is not {} d)return null;
            // English pret strings.c defines both Kun/Chan expansions as empty;
            // string_util.c chooses one by gender. Keep the raw source page intact.
            var text=d.Pages[d.Page].Replace("{PLAYER}",State.PlayerName,StringComparison.Ordinal).Replace("{RIVAL}",State.RivalName,StringComparison.Ordinal).Replace("{KUN}","",StringComparison.Ordinal);
            foreach(var item in State.TextBuffers)text=text.Replace("{"+item.Key+"}",item.Value,StringComparison.Ordinal);
            return text;
        }
    }
    // Convenience entrypoint with the same current-only contract as CreateCurrent.
    public CampaignSession(ICampaignCatalog content,CampaignNewGameOptions? options=null)
        : this(content,options,BindCurrentCatalog(content)) { }
    private CampaignSession(ICampaignCatalog content,CampaignNewGameOptions? options,CampaignCatalogBinding binding)
    {
        _content=content;_binding=binding;options??=new();
        if(!ValidTrainerName(options.PlayerName)||!ValidTrainerName(options.RivalName)||options.PlayerGender is <0 or >1)throw new ArgumentException("Invalid explicit new-game identity.");
        var id=content.MapFromSourceSymbol("MAP_PALLET_TOWN_PLAYERS_HOUSE_2F");var map=content.Maps[id];var tile=map.GetTile(6,6)??throw new InvalidDataException("Initial player room coordinate missing.");
        var initial=new CampaignState(new(RuleVersions.GridWorldV0,0,0,new(id,6,6,tile.Elevation,Direction.South),new(1)),StoryState.Empty with{Fame=FameCheckerRules.Reset()},ImmutableSortedDictionary<string,CampaignActor>.Empty,[],null,null,null,[],0,"HEAL_LOCATION_PALLET_TOWN","NPC_TEXT_COLOR_NEUTRAL","MUS_PALLET","RED","BLUE",3000,"MUS_DUMMY",[],true);
        initial=initial with{Inventory=content.OpeningItems is {} items?OpeningInventoryRules.NewGame(items):null,TrainerId=options.TrainerId,PlayerName=options.PlayerName,RivalName=options.RivalName,PlayerGender=options.PlayerGender,World=initial.World with{Rng=new(options.RngSeed)}};
        if(content.PokemonStorageEnabled)initial=initial with{Storage=PokemonStorageState.Empty};
        if(content.ConnectedFieldMaps is not null)initial=InitializeExploration(initial);
        if(content.OldManTutorialEnabled)initial=initial with{ObjectSlots=EmptyObjectPool};
        initial=InitializeRenewableHiddenItems(initial);
        initial=InitializeTrainerFanClub(initial);
        // NewGameInitData runs this real source script before normal map entry. No hand-copied hide flag list.
        initial=StartStory(initial,"EventScript_ResetAllMapFlags");
        initial=EnterMap(initial,null);
        initial=Pump(initial);
        _commands=new(initial,RulesVersion,content.ContentVersion);
    }
    private CampaignSession(ICampaignCatalog content,CampaignState state,long revision,CampaignCatalogBinding binding)
    {
        _content=content;_binding=binding;var error=ValidatePlayerSave(state);if(error is not null)throw new InvalidDataException("campaign_restore_invalid: "+error);
        _commands=new(state,RulesVersion,content.ContentVersion,revision);
    }
    // Restoration never chooses a historical composition.
    public static CampaignSession Restore(ICampaignCatalog content,CampaignState state,long revision)=>RestoreCurrent(content,state,revision);
    public static CampaignSession CreateCurrent(ICampaignCatalog content,CampaignNewGameOptions? options=null)=>new(content,options,BindCurrentCatalog(content));
    public static CampaignSession RestoreCurrent(ICampaignCatalog content,CampaignState state,long revision)=>new(content,state,revision,BindCurrentCatalog(content));
    public CommandOutcome<CampaignState> Execute(string id,long revision,CampaignCommand command)=>
        _commands.Execute(new(id,revision,RulesVersion,_content.ContentVersion,command),Decide);
    public string? ValidatePlayerSave(CampaignState s)
    {
        if(s is null||s.World is null||s.World.Player is null||s.Script is null||s.Actors is null||s.PlayerName is null||s.RivalName is null||s.EntryPrograms.IsDefault||s.Motions.IsDefault||s.PresentationEffects.IsDefault||s.Party.IsDefault||s.SeenSpecies is null||s.CaughtSpecies is null||s.TextBuffers is null||s.SettledBattles is null||s.BattledTrainers is null||s.BattleHistory.IsDefault)return "save_structure_invalid";
        if(ValidatePlayTime(s) is {} playTimeError)return playTimeError;
        if(s.Script.QuestLogState!=0)return "save_quest_log_state_not_supported";
        if(s.Script.Variables is null||s.Script.Flags is null||s.Script.Fame is null||s.Script.Stack.IsDefault)return "save_script_invalid";
        if(s.Script.Variables.Any(x=>x.Value is <0 or >65535)||s.Script.Fame.Any(x=>x.Key is <0 or >15||x.Value is null||x.Value.PickState is <0 or >2||x.Value.FlavorFlags is <0 or >63)||s.Script.Revision<0||s.Script.EffectSequence<0)return "save_script_invalid";
        if(ValidateTrainerFanClub(s) is {} fanClubError)return fanClubError;
        var renewableError=ValidateRenewableHiddenItems(s);if(renewableError is not null)return renewableError;
        if(ValidateSelphyRequest(s) is {} selphyError)return selphyError;
        var safariError=ValidateSafariProgress(s);if(safariError is not null)return safariError;
        if(s.TeachyTv is not null||s.VsSeekerEffect is not null||s.FameChecker is not null||s.SlotMachine is not null||s.FlyEffect is not null||s.Seagallop is not null||s.SeagallopMenu is not null||s.LostelleHypnoPreparation is not null||s.NpcTrade is not null||s.NameRater is not null||s.MoveTutor is not null||s.UltimateTutor is not null||s.MoveReminder is not null||s.MoveDeleter is not null||s.MagikarpRecord is not null||s.PokeFlute is not null||s.Fishing is not null||s.RareCandy is not null||s.MoneyBox is not null||s.CoinsBox is not null||s.PrizeExchange is not null||s.GiftDelivery is not null||s.TownMap is not null||s.Pc is not null||s.Shop is not null||s.DoubleBattle is not null||s.ForestTrainerBattle is not null||s.FieldPoison is not null||s.Evolution is not null||s.SignMessage is not null||s.ItemMenu is not null||s.PcPoweredOn||s.Battle is not null||s.WildBattle is not null||s.OldManTutorial is not null||!s.Script.Completed||s.Script.Locked||!s.Script.Stack.IsEmpty||s.Script.Wait is not null||s.Dialogue is not null||s.Nickname is not null||s.MonPicture is not null||s.ScreenIsBlack||!s.EntryPrograms.IsEmpty)return "save_event_active";
        if(s.MailComposer is not null||s.FossilMenu is not null||s.MailInteraction is not null||s.DayCareInteraction is not null||s.EggHatch is not null)return "save_event_active";
        if(s.PokemonLeagueCompletion is not null)return "save_pokemon_league_completion_active";
        var oakError=ValidateOakRatingState(s);if(oakError is not null)return oakError;
        var escapeError=ValidateEscapeRopeState(s);if(escapeError is not null)return escapeError;
        var repelError=ValidateRepelState(s);if(repelError is not null)return repelError;
        var optionsError=ValidateOptionsState(s);if(optionsError is not null)return optionsError;
        var mailError=ValidateCampaignMail(s);if(mailError is not null)return mailError;
        var storageError=ValidateStorageState(s);if(storageError is not null)return storageError;
        var giftEggError=ValidateGiftEggReceipts(s);if(giftEggError is not null)return giftEggError;
        var daycareError=ValidateRoute5DayCare(s)??ValidateFourIslandDayCare(s);if(daycareError is not null)return daycareError;
        var npcTradeError=ValidateNpcTradeHistory(s);if(npcTradeError is not null)return npcTradeError;
        var giftError=ValidateMagikarpGift(s);if(giftError is not null)return giftError;
        var eeveeError=ValidateEeveeGift(s);if(eeveeError is not null)return eeveeError;
        var silphGiftError=ValidateSilphGifts(s);if(silphGiftError is not null)return silphGiftError;
        var dojoGiftError=ValidateFightingDojoGift(s);if(dojoGiftError is not null)return dojoGiftError;
        var oldAmberError=ValidateOldAmberProgress(s);if(oldAmberError is not null)return oldAmberError;
        var fossilGiftError=ValidateFossilLabProgress(s);if(fossilGiftError is not null)return fossilGiftError;
        var giftIdentityError=ValidateGiftIdentityUniqueness(s);if(giftIdentityError is not null)return giftIdentityError;
        var coinError=ValidateGameCornerCoins(s);if(coinError is not null)return coinError;
        var prizeError=ValidateGameCornerPrizePokemon(s);if(prizeError is not null)return prizeError;
        var flyError=ValidateFieldFlyProgress(s);if(flyError is not null)return flyError;
        var teaError=ValidateRegionalTeaProgress(s);if(teaError is not null)return teaError;
        var trainerTowerError=ValidateTrainerTowerProgress(s);if(trainerTowerError is not null)return trainerTowerError;
        var parcelError=ValidateParcelState(s);if(parcelError is not null)return parcelError;
        if(!_content.Maps.TryGetValue(s.World.Player.MapId,out var map))return "save_map_missing";
        var surfError=ValidateFieldSurfState(s);if(surfError is not null)return surfError;
        var victoryRoadFieldError=ValidateVictoryRoadFieldProgress(s);if(victoryRoadFieldError is not null)return victoryRoadFieldError;
        var eventIslandError=ValidateEventIslandField(s);if(eventIslandError is not null)return eventIslandError;
        var seafoamFieldError=ValidateSeafoamField(s);if(seafoamFieldError is not null)return seafoamFieldError;
        var sevenFieldError=ValidateSevenIslandField(s);if(sevenFieldError is not null)return sevenFieldError;
        var strengthError=ValidateFieldStrengthState(s);if(strengthError is not null)return strengthError;
        var flashError=ValidateFieldFlashState(s);if(flashError is not null)return flashError;
        var pickupError=ValidateFieldPickupState(s);if(pickupError is not null)return pickupError;
        if(s.FieldItemService is not null)return "save_field_item_service_active";
        var worldError=ValidateCampaignWorld(s,map);if(worldError is not null)return worldError;
        if(!ValidTrainerName(s.PlayerName)||!ValidTrainerName(s.RivalName)||s.Money is <0 or >999999||s.LastHealingLocation is null||s.TextColor is null||s.Music is null||s.SavedMusic is null)return "save_player_invalid";
        if(s.PlayerGender is <0 or >1||s.PresentationTicks!=0||!s.Motions.IsEmpty||s.ArrivalFacing is not null)return "save_unstable_presentation";
        if(s.Script.Variables.Keys.Any(k=>!k.StartsWith("VAR_",StringComparison.Ordinal))||s.Script.Flags.Keys.Any(k=>!k.StartsWith("FLAG_",StringComparison.Ordinal)))return "save_script_identity_invalid";
        if(s.SeenSpecies.Any(x=>!_content.IsValidPokedexSourceSpecies(x))||!s.CaughtSpecies.IsSubsetOf(s.SeenSpecies))return "save_pokedex_invalid";
        if(s.TextBuffers.Any(k=>k.Key is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3")||k.Value is null||k.Value.Length>(_content.QuestItems is null?10:k.Key=="STR_VAR_1"?31:19)))return "save_text_buffer_invalid";
        var definitions=_content.MapDefinition(map.Id).Actors;
        if(s.Actors.Count!=definitions.Length)return "save_actor_count_invalid";
        foreach(var d in definitions)
        {
            if(!s.Actors.TryGetValue(d.LocalId,out var actor)||actor is null||actor.LocalId!=d.LocalId||actor.SourceLocalId!=d.SourceLocalId||actor.GraphicsId!=d.GraphicsId||actor.ScriptId!=d.ScriptId||actor.HideFlag!=d.HideFlag||string.IsNullOrEmpty(actor.MovementType)||!Enum.IsDefined(actor.Facing))return "save_actor_identity_invalid";
            if(d.IsClone)
            {if(actor.X!=d.X||actor.Y!=d.Y||actor.TemplateX!=d.X||actor.TemplateY!=d.Y||actor.Elevation!=d.Elevation)return "save_clone_position_invalid";}
            else if(actor.X<0||actor.Y<0||actor.X>=map.Width||actor.Y>=map.Height||actor.Elevation is <0 or >15||
                (actor.TemplateX<0||actor.TemplateY<0||actor.TemplateX>=map.Width||actor.TemplateY>=map.Height)&&
                !ValidCinnabarHiddenFerryTemplate(s,actor))return "save_actor_position_invalid";
        }
        if(s.LastTalked is not null&&!s.Actors.ContainsKey(s.LastTalked))return "save_last_actor_invalid";
        var allOwned=OwnedPokemon(s);
        if(allOwned.Length>0)
        {
            if(!_content.ForestEnabled&&s.Party.Any(p=>p.Status!=0))return "party_status_unsupported";
            var partyError=PokemonStateValidator.ValidatePartyCollection(_content.PokemonContent,s.Party,s.NextPokemonId);if(partyError is not null)return partyError;
            if(allOwned.Any(p=>!p.IsEgg&&!s.CaughtSpecies.Contains(p.Pokemon.SourceSpeciesId)))return "save_party_dex_invalid";
            // Held items are mutable ownership; the original trainer/met/capture identity remains fixed.
            if(allOwned.Any(p=>!HasSupportedCampaignOriginalTrainer(s,p)||p.Language!=(p.IsEgg?1:2)||p.MetGame!=4||((!_content.CaptureEnabled||p.InstanceId==1)&&(p.MetLocation!="MAPSEC_PALLET_TOWN"||p.MetLevel!=5))||(p.Ball!="ITEM_POKE_BALL"&&!(p.InstanceId!=1&&(AdditionalCaptureBallSupported(p.Ball)||p.Ball=="ITEM_SAFARI_BALL"&&_content.SafariEnabled)))||!OwnedHeldItemSupported(s,p)))return "save_party_origin_not_supported";
        }
        else if(s.NextPokemonId<1)return "save_party_serial_invalid";
        if(_content.OpeningItems is {} items){var inventoryError=ValidateCampaignInventory(s.Inventory);if(inventoryError is not null)return inventoryError;}
        else if(s.Inventory is not null)return "save_inventory_content_missing";
        if(s.HelpContext!="map")return "save_help_context_invalid";
        if(s.NextBattleId<1)return "save_battle_serial_invalid";
        return ValidateSeafoamEncounterProgress(s)??ValidatePowerPlantProgress(s)??ValidateItemfinderProgress(s)??ValidateVsSeekerProgress(s)??ValidateRegisteredKeyItem(s)??ValidateEventIslandBattleProgress(s)??ValidateSevenIslandProgress(s)??ValidateUnownPokedex(s)??ValidateCeruleanCaveProgress(s)??ValidateCelioProgress(s)??ValidateFiveIslandProgress(s)??ValidateSixIslandProgress(s)??ValidateFourIslandProgress(s)??ValidateSeviiRubyProgress(s)??ValidateNationalDexProgress(s)??ValidatePokemonLeagueProgress(s)??ValidateVictoryRoadProgress(s)??ValidateViridianGymProgress(s)??ValidateFirstSeviiProgress(s)??ValidateFirstSeviiBattleProgress(s)??ValidateCinnabarProgress(s)??ValidateCinnabarFieldProgress(s)??ValidateSaffronGymProgress(s)??ValidateSilphFieldProgress(s)??ValidateSilphProgress(s)??ValidateDoubleBattleHistory(s)??ValidateFuchsiaGymProgress(s)??ValidateDepartmentElevatorProgress(s)??ValidateSnorlaxProgress(s)??ValidateTowerProgress(s)??ValidateRocketHideoutProgress(s)??ValidateCeladonGymProgress(s)??ValidateRockTunnelProgress(s)??ValidateRoute11Progress(s)??ValidateBicycleState(s)??ValidateCityServicesProgress(s)??ValidateFieldRockSmashState(s)??ValidateFieldCutState(s)??ValidateVermilionGymProgress(s)??ValidateSsAnneProgress(s)??ValidateVermilionProgress(s)??ValidateBillRocketProgress(s)??ValidateBillProgress(s)??ValidateCeruleanProgress(s)??ValidateMtMoonProgress(s)??ValidateRoute3Progress(s)??ValidateRoute22Progress(s)??ValidateTownMapProgress(s)??ValidatePewterProgress(s)??ValidateForestTrainerHistory(s)??ValidateFieldPoisonState(s)??ValidateEvolutionHistory(s)??ValidateOldManProgress(s)??ValidateObjectSlots(s)??ValidateCenterState(s)??ValidateCapturedParty(s)??ValidateStarterProgress(s);
    }
    private static bool ValidTrainerName(string? name)=>name is not null&&name.Length is >=1 and <=7&&name.Any(c=>c!=' ')&&name.All(PokemonStateValidator.NicknameAlphabet.Contains);
    private string? ValidateStarterProgress(CampaignState state)
    {
        if(_content.PokemonContent is not {} content)return state.Party.IsEmpty?null:"save_pokemon_content_missing";
        var choices=content.StarterChoices;if(choices.Length!=3)return "starter_source_choices_missing";
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB");
        bool Obtained=state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEMON_GET");
        if(scene is 0 or 2 or 3 && (state.SettledBattles.Count!=0||state.BattledTrainers.Count!=0||state.NextBattleId!=1||state.BattleHistory.Length!=0))return "save_first_battle_not_completed_inconsistent";
        if(scene is 0 or 2)
        {
            if(!OwnedPokemon(state).IsEmpty||state.NextPokemonId!=1||Obtained||!state.SeenSpecies.IsEmpty||!state.CaughtSpecies.IsEmpty||choices.Any(c=>state.Script.Flags.GetValueOrDefault(c.BallHideFlag)))return "save_starter_not_yet_given_inconsistent";
        }
        else if(scene is 3 or 4 or 5 or 6||_content.NationalDexEnabled&&scene is 7 or 8 or 9)
        {
            if(!state.Script.Variables.TryGetValue("VAR_STARTER_MON",out var index)||index is <0 or >2||(_content.CaptureEnabled?state.Party.Length is <1 or >6:state.Party.Length!=1)||!Obtained||!state.Script.Flags.GetValueOrDefault("FLAG_PALLET_LADY_NOT_BLOCKING_SIGN"))return "save_starter_given_inconsistent";
            if(scene>=4&&(_content.BattleContent is null||!state.Script.Flags.GetValueOrDefault("FLAG_BEAT_RIVAL_IN_OAKS_LAB")||state.NextBattleId<2||state.SettledBattles.Count!=1||state.BattledTrainers.Count!=1+(_content.ForestTrainersEnabled?OrdinaryWonFlagCount(state)+TwinsWonFlagCount(state):0)||state.BattleHistory.Length!=1))return "save_first_battle_progress_inconsistent";
            if(scene==3&&(state.SettledBattles.Count!=0||state.BattledTrainers.Count!=0||state.NextBattleId!=1||state.BattleHistory.Length!=0))return "save_first_battle_not_completed_inconsistent";
            if(scene>4&&_content.QuestItems is null)return "save_parcel_content_missing";
            var selected=choices.Single(c=>c.Index==index);
            var requiredSeen=scene>=4?new[]{selected.SourceSpeciesId,selected.RivalSourceSpeciesId}:new[]{selected.SourceSpeciesId};
            bool seenValid=state.Exploration is null?state.SeenSpecies.SetEquals(requiredSeen):requiredSeen.All(state.SeenSpecies.Contains)&&state.SeenSpecies.All(n=>requiredSeen.Contains(n)||WildSpeciesSupported(n)||EvolvedSpecies(state).Contains(n)||SeenByOrdinaryTrainer(state,n)||SeenByTwins(state,n)||SeenByTrainerTower(state,n)||SeenByTowerMarowak(state,n)||SeenBySnorlax(state,n)||SeenByMewtwo(state,n)||SeenByEventIsland(state,n)||SeenByPowerPlant(state,n)||SeenByRockSmash(state,n)||SeenBySeafoam(state,n)||SeenByLostelleHypno(state,n)||SeenByCelioRoamer(state,n)||IsGiftSpecies(state,n)||NpcTradeSourceSpecies(state).Contains(n)||HatchedEggSpecies(state).Contains(n)||(_content.BillEnabled&&state.Script.Flags.GetValueOrDefault("FLAG_GOT_SS_TICKET")&&BillSourcePresentation.PcSpecies.Contains(n)));
            // Field Switch changes slots only in the normal early-starter profile. The gift and
            // its first-battle history remain attached to instance 1, including after evolution.
            var starter=_content.EarlyStarterEnabled?HistoricalPokemon(state).SingleOrDefault(p=>p.InstanceId==1):state.Party[0];
            if(starter is null||!EvolutionSpeciesMatchesOrigin(state,starter,selected.SourceSpeciesId)||starter.InstanceId!=1||state.NextPokemonId!=(_content.CaptureEnabled?2L+(state.Exploration!.Captured?.Length??0)+GiftCount(state)+NpcTradeCount(state)+EggAcquisitionCount(state):2)||!seenValid||!state.CaughtSpecies.SetEquals(new[]{selected.SourceSpeciesId}.Concat(_content.CaptureEnabled?state.Exploration!.Captured!.Value.Select(c=>c.SourceSpeciesId):[]).Concat(GiftSourceSpecies(state)).Concat(NpcTradeSourceSpecies(state)).Concat(HatchedEggSpecies(state)).Concat(EvolvedSpecies(state))))return "save_starter_identity_inconsistent";
            if(scene>=4)
            {
                var trainer=_content.BattleContent!.Trainers.Trainers.Values.Single(t=>t.Party.Single().SourceSpeciesId==selected.RivalSourceSpeciesId).Id.Value;
                var history=state.BattleHistory[0];
                if(history is null||history.BattleId!="lab-battle:1"||history.TrainerId!=trainer||state.NextBattleId!=2||!state.SettledBattles.Contains(history.BattleId)||!state.BattledTrainers.Contains(trainer)||!Enum.IsDefined(history.Outcome)||history.Outcome==Game.Domain.FirstBattle.FirstBattleOutcome.Ongoing||history.QuestLogRequested!=(history.Outcome==Game.Domain.FirstBattle.FirstBattleOutcome.Won))return "save_first_battle_history_invalid";
                var won=history.Outcome==Game.Domain.FirstBattle.FirstBattleOutcome.Won;var pokemon=starter.Pokemon;
                var rival=content.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==selected.RivalSourceSpeciesId);
                int xp=rival.ExperienceYield*5/7*150/100;
                if(history.ExperienceGained!=(won?xp:0)||history.MoneyGained<0||history.MoneyGained>(won?80:0)||(won?history.HpFraction is not (0 or 1 or 2):history.HpFraction is not null))return "save_first_battle_reward_history_invalid";
                if(won&&(pokemon.Level<6||pokemon.Experience<135u+(uint)xp||pokemon.Evs.Hp<rival.EvYield.Hp||pokemon.Evs.Attack<rival.EvYield.Attack||pokemon.Evs.Defense<rival.EvYield.Defense||pokemon.Evs.Speed<rival.EvYield.Speed||pokemon.Evs.SpecialAttack<rival.EvYield.SpecialAttack||pokemon.Evs.SpecialDefense<rival.EvYield.SpecialDefense))return "save_first_battle_growth_missing";
            }
            if(choices.Any(c=>state.Script.Flags.GetValueOrDefault(c.BallHideFlag)!=(c.BallId==selected.BallId||c.BallId==selected.RivalBallId)))return "save_starter_ball_flags_inconsistent";
        }
        else return "save_campaign_scene_not_supported";
        var lab=_content.MapFromSourceSymbol("MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB");
        if(state.World.Player.MapId==lab)
        {
            foreach(var choice in choices)
            {
                var actor=state.Actors[choice.BallId];var definition=_content.MapDefinition(lab).Actors.Single(a=>a.LocalId==choice.BallId);
                var player=state.World.Player;bool inView=actor.X+7>=player.X-2&&actor.X+7<=player.X+17&&actor.Y+7>=player.Y&&actor.Y+7<=player.Y+16;
                if(actor.Active!=(!state.Script.Flags.GetValueOrDefault(choice.BallHideFlag)&&inView)||actor.Invisible||actor.X!=definition.X||actor.Y!=definition.Y||actor.TemplateX!=definition.X||actor.TemplateY!=definition.Y)return "save_starter_ball_actor_inconsistent";
            }
        }
        return null;
    }
    private string? PreflightStarterAcceptance(CampaignState state)
    {
        if(_content.PokemonContent is not {} content)return "pokemon_content_missing";
        if(!ValidTrainerName(state.PlayerName)||!ValidTrainerName(state.RivalName)||state.PlayerGender is <0 or >1)return "starter_identity_invalid";
        if(!state.Party.IsEmpty||state.NextPokemonId!=1||state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEMON_GET"))return "starter_already_given_or_id_invalid";
        if(state.World.Rng.Cursor>ulong.MaxValue-4)return "starter_rng_capacity";
        if(state.World.Rng.AlgorithmId!=Game.Domain.Randomness.Gen3Rng.AlgorithmId)return "starter_rng_algorithm";
        var index=state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");var choice=content.StarterChoices.SingleOrDefault(c=>c.Index==index);
        if(choice is null||state.MonPicture?.SourceSpeciesId!=choice.SourceSpeciesId||state.LastTalked!=choice.BallId||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB")!=2)return "starter_choice_context_invalid";
        if(_content.MapDefinition(state.World.Player.MapId).RegionMapSection!="MAPSEC_PALLET_TOWN")return "starter_met_location_invalid";
        PokemonFactory.ValidateCatalog(content.Catalog);RequireSpecies(choice.SourceSpeciesId);
        return null;
    }
    private Decision<CampaignState> Decide(CampaignState state,CampaignCommand command)
    {
        try
        {
            // The source clock owns no modal object and runs during every active scene.
            // Dispatch this bounded, time-only transaction before ownership guards.
            if(command.Kind=="play_time_tick")return DecidePlayTime(state,command);
            // MOVE owns a removed creature or held item. All gameplay commands
            // remain behind this native modal.
            if(state.Pc is {View:CampaignPcView.Move or CampaignPcView.MoveItems} or {MoveCursor:not null} or {ItemCursor:not null} or {ItemPrompt:not null} or {ItemBagTarget:not null} or {ReleaseTarget:not null} or {Customization:not null} or {Summary:not null})return DecidePc(state,command);
            if(state.ItemMenu?.Toss is not null)return state.MailComposer is not null?DecideMailComposer(state,command):DecideBagToss(state,command);
            if(state.SlotMachine is not null||command.Kind.StartsWith("slot_",StringComparison.Ordinal))return DecideSlotMachine(state,command);
            if(state.FlyEffect is not null)return state.TownMap is not null?DecideTownMap(state,command):DecideFieldFly(state,command);
            if(command.Kind=="field_fly")return StartFieldFly(state,command);
            if(TryDecideFourIslandDayCare(state,command,out var fourDaycareDecision))return fourDaycareDecision;
            if(TryDecideEggHatch(state,command,out var eggHatchDecision))return eggHatchDecision;
            if(TryDecideRoute5DayCare(state,command,out var daycareDecision))return daycareDecision;
            if(TryDecideMagikarpRecord(state,command,out var magikarpRecordDecision))return magikarpRecordDecision;
            if(TryDecideMoveDeleter(state,command,out var moveDeleterDecision))return moveDeleterDecision;
            if(TryDecideMoveReminder(state,command,out var moveReminderDecision))return moveReminderDecision;
            if(command.Kind=="trainer_tower_tick")return DecideTrainerTowerTime(state,command);
            if(command.Kind=="national_dex_resume")return NationalDexScenePendingIn(state)
                ?new(CommandStatus.Accepted,Pump(state)):new(CommandStatus.Rejected,state,"national_dex_scene_not_pending");
            if(NationalDexScenePendingIn(state)&&command.Kind is "move" or "turn" or "interact")
                return new(CommandStatus.Accepted,Pump(state));
            if(state.Seagallop is not null)return DecideSeagallop(state,command);
            if(state.RuinValleyCut is not null)return DecideRuinValleyCutAck(state,command);
            if(state.SeafoamMovement is not null)return DecideSeafoamMovement(state,command);
            if(state.DottedHoleFall is not null)return DecideDottedHoleFallAck(state,command);
            if(state.FourIslandMovement is not null)return DecideFourIslandMovement(state,command);
            if(command.Kind=="field_waterfall")return StartFieldWaterfall(state,command);
            if(TryDecideCinnabarFall(state,command) is {} cinnabarFallDecision)return cinnabarFallDecision;
            if(state.SilphPad is not null)return DecideSilphPad(state,command);
            if(SilphMenuPending(state)||command.Kind=="silph_elevator_choice")return DecideSilphElevatorChoice(state,command);
            if(state.SurfEffect is not null)return CompleteFieldSurf(state,command);
            if(TryDecidePokemonLeague(state,command,out var leagueDecision))return leagueDecision;
            if(command.Kind=="field_surf")return StartFieldSurf(state,command);
            if(state.StrengthEffect is {Phase:FieldStrengthSourcePresentation.Push})return CompleteFieldStrengthPush(state,command);
            if(state.StrengthEffect is not null&&command.Kind!="ack")return new(CommandStatus.Rejected,state,"field_strength_event_active");
            if(command.Kind=="field_strength")return StartFieldStrength(state,command);
            if(state.SafariBattle is not null)return DecideSafariBattle(state,command);
            if(TryDecideUltimateTutor(state,out var ultimateTutorDecision))return ultimateTutorDecision;
            if(TryDecideMoveTutor(state,command,out var moveTutorDecision))return moveTutorDecision;
            if(state.MailComposer is not null||command.Kind.StartsWith("mail_compose_",StringComparison.Ordinal))return DecideMailComposer(state,command);
            if(command.Kind=="mail_preview_open")return OpenMailComposer(state,command,true);
            if(TryDecideMail(state,command,out var mailDecision))return mailDecision;
            if(TryDecideNpcTrade(state,command,out var npcTradeDecision))return npcTradeDecision;
            if(TryDecideNameRater(state,command,out var nameRaterDecision))return nameRaterDecision;
            if(command.Kind=="safari_retire")return DecideSafariRetire(state);
            if(state.Itemfinder is not null)return DecideItemfinder(state,command);
            if(state.PokeFlute is not null)return DecidePokeFlute(state,command);
            if(state.RocketSpin is not null)return DecideRocketSpin(state,command);
            if(state.FlashEffect is not null)return DecideFieldFlash(state,command);
            if(command.Kind=="field_flash")return StartFieldFlash(state,command);
            if(state.Fishing is not null)return DecideFishing(state,command);
            if(command.Kind=="field_rock_smash")return DecideFieldRockSmash(state,command);
            if(command.Kind=="field_cut")return DecideFieldCut(state,command);
            if(state.RareCandy is not null)return DecideRareCandy(state,command);
            if(state.EscapeRope is not null)return DecideEscapeRope(state,command);
            if(state.TeachyTv is not null)return DecideTeachyTv(state,command);
            if(state.VsSeekerEffect is not null)return DecideVsSeeker(state,command);
            if(state.FameChecker is not null)return DecideFameChecker(state,command);
            if(state.TownMap is not null)return DecideTownMap(state,command);
            if(state.Repel is not null)return DecideRepel(state,command);
            if(command.Kind=="options_save")return DecideOptions(state,command);
            if(command.Kind=="party_item_open")return OpenPartyItemMenu(state,command);
            if(command.Kind=="party_swap")return DecideFieldParty(state,command);
            if(command.Kind=="party_summary_swap")return DecideFieldSummarySwap(state,command);
            if(state.FieldPoison is not null)return DecideFieldPoison(state,command);
            if(state.Evolution is not null)return DecideEvolution(state,command);
            if(state.OldManTutorial is not null)return DecideOldManTutorial(state,command);
            if(state.DoubleBattle is not null)return DecideDoubleBattle(state,command);
            if(state.ForestTrainerBattle is not null)return DecideForestTrainerBattle(state,command);
            if(state.WildBattle is not null)return DecideWildBattle(state,command);
            if(state.Battle is not null)return DecideBattle(state,command);
            if(state.Shop is not null)return DecideShop(state,command);
            if(state.ItemMenu is not null)return DecideItemMenu(state,command);
            if(state.Pc is not null)return DecidePc(state,command);
            if(command.Kind=="nickname")
            {
                if(state.Nickname is not {Submitted:false} naming||state.Script.Wait is not {Kind:StoryWaitKind.Nickname} wait)return new(CommandStatus.Rejected,state,"nickname_not_pending");
                if(command.Text is null)return new(CommandStatus.Rejected,state,"nickname_text_required");
                var content=_content.PokemonContent??throw new InvalidDataException("pokemon_content_missing");
                var error=PokemonNicknameRules.Validate(command.Text,content.NicknameCharacters);if(error is not null)return new(CommandStatus.Rejected,state,error);
                var name=PokemonNicknameRules.Apply(naming.OriginalName,command.Text,content.NicknameCharacters);
                if(naming.BoxAddress is not null)return SubmitBoxGiftNickname(state,naming,wait,name);
                var mon=state.Party[naming.PartyIndex];
                // Resume the existing field context. EnterMap would wrongly clear the starter branch's temp variables.
                return new(CommandStatus.Accepted,state with{Party=state.Party.SetItem(naming.PartyIndex,mon with{Nickname=name}),Nickname=naming with{Submitted=true},TextBuffers=state.TextBuffers.SetItem("STR_VAR_2",name),ScreenIsBlack=false,PresentationTicks=16,Script=state.Script with{Wait=wait with{Kind=StoryWaitKind.Presentation}}});
            }
            if(command.Kind is "sign_tick" or "sign_cancel")return DecideRegionalSignWalkaway(state,command);
            if(command.Kind=="button_press")return ArticunoButtonWait(state)?DecideArticunoButton(state,command):PowerPlantButtonWait(state)?DecidePowerPlantButton(state,command):MewtwoButtonWait(state)?DecideMewtwoButton(state,command):SixIslandBrailleWait(state)?DecideSixIslandButton(state,command):FourIslandButtonWait(state)?DecideFourIslandButton(state,command):RubyButtonWait(state)?DecideRubyButton(state,command):IsTowerButton(state)?DecideTowerButton(state,command):IsRocketHideoutButton(state)?DecideRocketHideoutButton(state,command):IsBillRocketButton(state)?DecideBillRocketButton(state,command):IsBillButton(state)?DecideBillButton(state,command):IsMtMoonButton(state)?DecideMtMoonButton(state,command):IsRoute22GuardButton(state)?DecideRoute22GuardButton(state,command):DecideOakRatingButton(state,command);
            if(command.Kind=="ack")
            {
                if(state.Script.Wait is not {} wait)return new(CommandStatus.Rejected,state,"no_pending_event");
                if(wait.Kind==StoryWaitKind.ButtonPress)return new(CommandStatus.Rejected,state,"button_press_required");
                if(wait.Kind==StoryWaitKind.Nickname)return new(CommandStatus.Rejected,state,"nickname_submission_required");
                if(wait.Kind is StoryWaitKind.Dialog or StoryWaitKind.Choice or StoryWaitKind.MenuChoice or StoryWaitKind.MessagePrinting && state.Dialogue is {} d&&d.Page+1<d.Pages.Length)return new(CommandStatus.Accepted,state with{Dialogue=d with{Page=d.Page+1}});
                if(wait.Kind==StoryWaitKind.Choice&&command.Choice==1&&state.MonPicture is{SourceSpeciesId:1 or 4 or 7})
                {var preflight=PreflightStarterAcceptance(state);if(preflight is not null)return new(CommandStatus.Rejected,state,preflight);}
                if(wait.Kind==StoryWaitKind.Choice&&command.Choice==1&&!_content.FirstSeviiEnabled&&CinnabarFerryChoice(state))
                    return new(CommandStatus.Rejected,state,"sevii_first_visit_not_yet_available");
                if(PrizeMenu(state) is {} prizeMenu && command.Choice is not 127 &&
                    (command.Choice is not {} prizeChoice || prizeChoice < 0 || prizeChoice >= prizeMenu.MenuLabels.Length))
                    return new(CommandStatus.Rejected,state,"game_corner_prize_choice_invalid");
                if(GameCornerCoinSource.Pending(state.Script,_content) && command.Choice is not (0 or 1 or 2 or 127))
                    return new(CommandStatus.Rejected,state,"game_corner_coin_choice_invalid");
                if(DepartmentRooftopSource.Pending(state.Script,_content) is {} rooftopMenu && command.Choice is not 127 &&
                    (command.Choice is not {} roofChoice || roofChoice<0 || roofChoice>=rooftopMenu.Rows.Length))
                    return new(CommandStatus.Rejected,state,"department_rooftop_menu_choice_invalid");
                if(state.TrainerTowerMenu is {} towerMenu&&command.Choice is not 127&&
                    (command.Choice is not {} towerChoice||towerChoice<0||towerChoice>=towerMenu.Options.Length))return new(CommandStatus.Rejected,state,"trainer_tower_menu_choice_invalid");
                if(state.SeagallopMenu is {} ferryMenu&&command.Choice is not 127&&
                    (command.Choice is not {} selected||selected<0||selected>=ferryMenu.Options.Length))return new(CommandStatus.Rejected,state,"seagallop_menu_choice_invalid");
                if(state.FossilMenu is {} fossilMenu&&command.Choice is not 127&&
                    (command.Choice is not {} fossilChoice||fossilChoice<0||fossilChoice>=fossilMenu.Options.Length))return new(CommandStatus.Rejected,state,"fossil_lab_menu_choice_invalid");
                if(RainbowFerryMenu(state)&&command.Choice is {} rainbowChoice&&RainbowDestination(state,rainbowChoice) is int rainbowDestination&&rainbowDestination is >=0 and <=7&&!RainbowIslandAvailable(rainbowDestination))
                    return new(CommandStatus.Rejected,state,"seagallop_destination_not_yet_available");
                var next=state with{ScreenIsBlack=state.TrainerTowerRecordsVisible?false:state.ScreenIsBlack,TrainerTowerMenu=null,TrainerTowerRecordsVisible=false,SeagallopMenu=null,FossilMenu=null,HealingEffect=wait.Kind==StoryWaitKind.Presentation&&state.PresentationEffects.Any(e=>e.Opcode=="waitfieldeffect")?null:state.HealingEffect,Nickname=state.Nickname?.Submitted==true?null:state.Nickname,Dialogue=state.Dialogue?.CloseOnCompletion==true&&wait.Kind is StoryWaitKind.Dialog or StoryWaitKind.Choice?null:state.Dialogue,Motions=[],PresentationTicks=0,PresentationEffects=[]};
                next=CompleteNpcTradePresentation(state,next);
                next=CompleteEventIslandPresentation(state,next);
                next=CompleteFieldRockSmashPresentation(state,next);
                next=CompleteFieldCutPresentation(state,next);
                next=CompleteFieldStrengthPresentation(state,next);
                next=ReleaseForestTrainerPostDialogue(state,next);
                next=Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content,command.Choice)));
                return new(CommandStatus.Accepted,CompleteFightingDojoMasterTurn(state,next));
            }
            if(!state.Script.Completed||state.Script.Wait is not null||state.Dialogue is not null)return new(CommandStatus.Rejected,state,"event_active");
            // ProcessPlayerFieldInput resets the selected physical object to player slot 0
            // before choosing a field action. A successful NPC interaction selects its slot below.
            if(state.ObjectSlots is not null&&command.Kind is "move" or "turn" or "interact" or "bag_open")
                state=state with{SelectedObjectSlot=0};
            if(command.Kind=="use_registered")return UseRegisteredKeyItem(state,command);
            if(command.Kind=="return_to_field")return new(CommandStatus.Accepted,ReturnToEventIslandField(state));
            if(command.Kind=="bag_open")
                return _content.OpeningItems is null?new(CommandStatus.Rejected,state,"opening_items_not_enabled"):new(CommandStatus.Accepted,state with{ItemMenu=new("bag","bag")});
            if(command.Kind is "move" or "turn"&&_content.ConnectedFieldMaps is not null)return DecideConnectedField(state,command);
            if(command.Kind=="move")
            {
                if(!Enum.IsDefined(command.Direction))return new(CommandStatus.Rejected,state,"invalid_direction");
                var before=state.World.Player;var map=_content.Maps[before.MapId];
                var (dx,dy)=Delta(command.Direction);int x=before.X+dx,y=before.Y+dy;
                // Current map's on-frame/coordinate event selection is checked before movement/warp.
                var active=VisibleActorAt(state,x,y);
                if(active is not null)
                {
                    var world=state.World with{Revision=state.World.Revision+1,Player=before with{Facing=command.Direction}};
                    return new(CommandStatus.Accepted,state with{World=world});
                }
                // Source auto-sign interaction is vertical and requires matching facing, before the door hook.
                if(command.Direction is Direction.North or Direction.South&&before.Facing==command.Direction&&RegionalWalkIntoSign(state,x,y) is {} regionalSign)
                    return new(CommandStatus.Accepted,regionalSign);
                if(!_content.SupportsRegionalFlavor(before.MapId)&&command.Direction is Direction.North or Direction.South&&before.Facing==command.Direction&&map.SourceBehaviorAt(x,y)==0x84)
                {
                    var sign=_content.MapDefinition(map.Id).BackgroundTriggers.FirstOrDefault(z=>z.X==x&&z.Y==y);
                    if(sign is not null)return new(CommandStatus.Accepted,Pump(StartStory(state,sign.ScriptId)));
                }
                var movement=FieldMovementRules.Move(state.World,new(new("field:"+state.World.Revision),state.World.Revision,command.Direction),_content.Maps);
                if(movement.Status!=TransitionStatus.Accepted)return new(movement.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,movement.Diagnostic?.Code);
                var moved=RefreshActors(state with{World=movement.State});
                if(movement.MapChanged)return new(CommandStatus.Accepted,Pump(EnterMap(moved,movement.State.Player.Facing)));
                if(movement.State.Player.X!=before.X||movement.State.Player.Y!=before.Y)
                {
                    moved=ApplyEventIslandWalkingStep(moved);
                    var p=movement.State.Player;var trigger=_content.MapDefinition(p.MapId).CoordTriggers.FirstOrDefault(t=>t.X==p.X&&t.Y==p.Y&&(t.Elevation==0||t.Elevation==p.Elevation)&&moved.Script.Variables.GetValueOrDefault(t.Variable)==t.Expected);
                    if(trigger is not null&&_content.BattleContent is not null&&p.MapId==_content.MapFromSourceSymbol("MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB")&&moved.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB")==3)PreflightLabBattle(moved);
                    if(trigger is not null)moved=StartStory(moved,trigger.ScriptId);
                }
                return new(CommandStatus.Accepted,Pump(moved));
            }
            if(command.Kind=="interact")
            {
                var p=state.World.Player;var (dx,dy)=Delta(p.Facing);int actorX=p.X+dx,actorY=p.Y+dy;
                if(!_content.SupportsRegionalFlavor(p.MapId)&&_content.PokemonStorageEnabled&&IsSupportedHealingCenter(p.MapId)&&SourceBehavior(state,actorX,actorY)==0x83)
                    return new(CommandStatus.Accepted,Pump(StartStory(state,CenterPcScript)));
                if(_content.QuestItems is not null&&SourceBehavior(state,actorX,actorY)==0x80&&(!_content.SupportsRegionalFlavor(p.MapId)||VisibleActorAt(state,actorX,actorY) is null)){actorX+=dx;actorY+=dy;}
                var actor=VisibleActorAt(state,actorX,actorY);
                if(actor is not null)
                {
                    if(_content.DeferredRegionalServiceReason(p.MapId,actor.ScriptId) is {} unavailable)return new(CommandStatus.Rejected,state,unavailable);
                    if(actor.ScriptId is "0" or "0x0")return new(CommandStatus.Rejected,state,"actor_no_script");
                    if(_content.CeladonApproachEnabled&&!_content.PresetDoublesEnabled&&actor.ScriptId is "Route8_EventScript_Eli" or "Route8_EventScript_Anne")return new(CommandStatus.Rejected,state,"celadon_approach_double_battle_not_implemented");
                    if(_content.RocketHideoutEnabled&&actor.ScriptId is "RocketHideout_B4F_EventScript_LiftKey" or "RocketHideout_B4F_EventScript_SilphScope"&&
                        (state.Inventory?.KeyItems??[]).Length>=ShopCatalog.QuestLimits.KeyItemSlots)
                        return new(CommandStatus.Rejected,state,"inventory_destination_full");
                    PreflightOakInteraction(state,actor);
                    PreflightSourceItemInteraction(state,actor);
                    var context=SelectInteractedObject(state,actor.LocalId) with{LastTalked=actor.LocalId,Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_LAST_TALKED",actor.SourceLocalId).SetItem("VAR_FACING",SourceDirectionValue(p.Facing))}};
                    context=BeginItemBall(context,actor);
                    return new(CommandStatus.Accepted,Pump(StartStory(context,actor.ScriptId)));
                }
                if(TryStartFieldWaterfallInteraction(state) is {} waterfallInteraction)return waterfallInteraction;
                if(TryStartFieldSurfInteraction(state) is {} surfInteraction)return surfInteraction;
                var sign=InteractionBackgroundAt(state,p.X+dx,p.Y+dy,p.Facing);
                if(_content.SupportsRegionalFlavor(p.MapId))return DecideRegionalInteraction(state,sign,p.X+dx,p.Y+dy);
                if(sign?.HiddenItem is not null)return BeginHiddenPickup(state,sign);
                if(sign?.ScriptId==BedroomPcScript&&_content.OpeningItems is not null&&(long.MaxValue-Revision<(1L<<32)||long.MaxValue-state.Script.Revision<(1L<<32)||long.MaxValue-state.Script.EffectSequence<(1L<<32)))return new(CommandStatus.Fault,state,"pc_counter_headroom_required");
                return sign is null?new(CommandStatus.Rejected,state,"nothing_to_interact"):new(CommandStatus.Accepted,Pump(StartBackgroundStory(state,sign)));
            }
            return new(CommandStatus.Rejected,state,"unknown_campaign_command");
        }
        catch(Exception ex) when(ex is InvalidDataException or KeyNotFoundException or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {return new(CommandStatus.Fault,state,ex.Message);}
    }
    private CampaignState EnterMap(CampaignState state,Direction? arrival,Game.Domain.Roaming.RoamerTransition roamerTransition=Game.Domain.Roaming.RoamerTransition.Warp)
    {
        if(state.SurfEffect is not null)throw new InvalidDataException("field_surf_map_entry_while_active");
        if(arrival is not null)state=ApplyCelioRoamerTransition(state,roamerTransition);
        state=ResetVsSeekerForMapEntry(state);
        state=FourIslandMapEntry(state);
        state=ApplyFieldStrengthMapEntry(state);
        state=ApplySevenIslandMapEntry(state);
        state=ApplyEventIslandMapEntry(state);
        state=ApplyFieldFlashMapEntry(state);
        var definition=_content.MapDefinition(state.World.Player.MapId);
        var variables=state.Script.Variables.RemoveRange(state.Script.Variables.Keys.Where(k=>k.StartsWith("VAR_TEMP_",StringComparison.Ordinal)));
        var flags=state.Script.Flags.RemoveRange(state.Script.Flags.Keys.Where(k=>k.StartsWith("FLAG_TEMP_",StringComparison.Ordinal)));
        var actors=definition.Actors.ToImmutableSortedDictionary(a=>a.LocalId,a=>new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,DirectionFromType(a.MovementType),a.MovementType,a.ScriptId,a.HideFlag,false,false,a.X,a.Y),StringComparer.Ordinal);
        var queue=ImmutableArray.CreateBuilder<string>();if(definition.Programs.OnLoad is {} onLoad)queue.Add(onLoad);if(definition.Programs.OnTransition is {} onTransition)queue.Add(onTransition);
        if(_content.FirstSeviiEnabled)queue.Add("@renewable-hidden-items");
        if(_content.ViridianCenterEnabled&&definition.Programs.OnResume is {} onResume)queue.Add(onResume);
        queue.Add("@spawn");
        if(_content.EventIslandsEnabled&&definition.Programs.OnReturnToField is {} onReturn)queue.Add(onReturn);
        // Markers evaluate conditional tables after transition flags/vars have committed.
        if(definition.Programs.OnWarpTable is {} onWarp)queue.Add("@table:"+onWarp);
        return state with{IsSurfing=false,SurfEffect=null,Music=state.IsSurfing?definition.Music:state.Music,IsCycling=state.IsCycling&&_content.AllowsCycling(state.World.Player.MapId),ObjectSlots=state.ObjectSlots is null?null:EmptyObjectPool,Actors=actors,Script=state.Script with{Variables=variables,Flags=flags},EntryPrograms=queue.ToImmutable(),ArrivalFacing=arrival,LastTalked=null,PlayerVisible=true,MapEdits=_content.FiveIslandEnabled&&FiveIslandEditedMap(state.World.Player.MapId)?FiveIslandExpectedEdits(state):_content.CinnabarEnabled&&CinnabarFieldMap(state.World.Player.MapId)?CinnabarExpectedEdits(state):_content.RocketHideoutEnabled&&RocketMap(state.World.Player.MapId)?RocketExpectedEdits(state):null,MessagePointer=null};
    }
    private CampaignState Pump(CampaignState state) => PumpWithCameraStep(state, null);

    private CampaignState PumpWithCameraStep(CampaignState state, Action? onCameraStepPreempted)
    {
        for(int i=0;i<128;i++)
        {
            if(state.Script.Wait is {Kind:StoryWaitKind.NativeEffect} native)
            {state=Advance(state,StoryRules.Continue(state.Script,native.CorrelationId,_content));continue;}
            if(state.Script.Wait is not null||!state.Script.Completed)return state;
            if(state.SafariExitPending){state=CompleteSafariExitWarp(state);continue;}
            state=FinishFieldPickup(state);
            if(state.FieldItemService is not null)state=state with{FieldItemService=null};
            if(state.NpcTrade is not null)state=state with{NpcTrade=null};
            if(state.NameRater is not null)state=state with{NameRater=null};
            if(state.MoveReminder is not null)state=state with{MoveReminder=null};
            if(state.MoveDeleter is not null)state=state with{MoveDeleter=null};
            if(state.DayCareInteraction is not null)state=state with{DayCareInteraction=null};
            if(state.MagikarpRecord is not null)state=state with{MagikarpRecord=null};
            if(_content.SupportsRegionalFlavor(state.World.Player.MapId)&&state.Dialogue is null&&!state.Script.Locked)state=state with{SignMessage=null};
            if(!state.EntryPrograms.IsEmpty)
            {
                var next=state.EntryPrograms[0];state=state with{EntryPrograms=state.EntryPrograms.RemoveAt(0)};
                if(next=="@renewable-hidden-items"){state=ApplyRenewableHiddenItemMapEntry(state);continue;}
                if(next=="@renewable-camera-step")
                {
                    state=CheckForestTrainerSight(state);
                    if(state.ForestTrainerBattle is not null||state.DoubleBattle is not null){onCameraStepPreempted?.Invoke();return state;}
                    var frameTable=_content.MapDefinition(state.World.Player.MapId).Programs.OnFrameTable;
                    var frame=frameTable is null?null:_content.Conditions(frameTable).FirstOrDefault(c=>state.Script.Variables.GetValueOrDefault(c.Variable)==c.Expected);
                    if(frame is not null){onCameraStepPreempted?.Invoke();state=StartStory(state,frame.ScriptId);continue;}
                    state=ApplyRenewableHiddenItemWalkingStep(state);
                    var p=state.World.Player;
                    var coord=_content.MapDefinition(p.MapId).CoordTriggers.FirstOrDefault(t=>t.X==p.X&&t.Y==p.Y&&(t.Elevation==0||t.Elevation==p.Elevation)&&state.Script.Variables.GetValueOrDefault(t.Variable)==t.Expected);
                    if(coord is not null){onCameraStepPreempted?.Invoke();state=StartStory(state,coord.ScriptId);}
                    continue;
                }
                if(next=="@spawn"){state=RefreshActors(state);continue;}
                if(next.StartsWith("@table:",StringComparison.Ordinal))
                {var condition=_content.Conditions(next[7..]).FirstOrDefault(c=>state.Script.Variables.GetValueOrDefault(c.Variable)==c.Expected);if(condition is null)continue;next=condition.ScriptId;}
                state=StartStory(state,next);continue;
            }
            if(state.ArrivalFacing is {} facing){state=state with{World=state.World with{Player=state.World.Player with{Facing=facing}},ArrivalFacing=null};}
            var table=_content.MapDefinition(state.World.Player.MapId).Programs.OnFrameTable;
            if(table is null)return state;
            var trigger=_content.Conditions(table).FirstOrDefault(c=>state.Script.Variables.GetValueOrDefault(c.Variable)==c.Expected);
            if(trigger is null)return state;
            state=StartStory(state,trigger.ScriptId);
        }
        throw new InvalidDataException("campaign_event_budget");
    }
    private CampaignState Advance(CampaignState state,StoryTransition transition)
    {
        if(transition.Status!=TransitionStatus.Accepted)throw new InvalidDataException(transition.Diagnostic?.Code+": "+transition.Diagnostic?.Message);
        state=state with{Script=transition.State};
        foreach(var effect in transition.Effects)
        {
            var a=effect.Args;
            state=state with{PresentationEffects=state.PresentationEffects.Add(new(effect.Sequence,effect.Opcode,state.World.Player.MapId,a))};
            int Int(string token)=>int.TryParse(token,out var n)?n:state.Script.Variables.TryGetValue(token,out var v)?v:_content.TryConstant(token,out var c)?c:throw new InvalidDataException("Unresolved effect value: "+token);
            string ActorId(string id)
            {
                if(id is "LOCALID_LAST_TALKED" or "VAR_LAST_TALKED")return state.LastTalked??throw new InvalidDataException("Last actor missing");
                if(id.StartsWith("VAR_",StringComparison.Ordinal)||int.TryParse(id,out _)){int value=Int(id);return value==255?"LOCALID_PLAYER":state.Actors.Values.Single(x=>x.SourceLocalId==value).LocalId;}
                return id;
            }
            if((effect.Opcode=="applymovement"&&a.Length>2)||(effect.Opcode is "addobject" or "removeobject"&&a.Length>1))
                throw new InvalidDataException("cross_map_effect_not_supported: "+effect.Opcode);
            if(TryApplyVsSeekerFieldEffect(state,effect,out var vsFieldState)){state=vsFieldState;continue;}
            if(TryApplyVsSeekerBattleEffect(state,effect,out var vsBattleState)){state=vsBattleState;continue;}
            if(TryApplySlotMachineEffect(state,effect,out var slotState)){state=slotState;continue;}
            if(TryApplyFieldFlyGiftEffect(state,effect,out var flyGiftState)){state=flyGiftState;continue;}
            if(TryApplyFourIslandDayCareEffect(state,effect,out var fourDaycareState)){state=fourDaycareState;continue;}
            if(TryApplyGiftEggEffect(state,effect,out var giftEggState)){state=giftEggState;continue;}
            if(TryApplyEggHatchEffect(state,effect,out var eggHatchState)){state=eggHatchState;continue;}
            if(TryApplyRoute5DayCareEffect(state,effect,out var daycareState)){state=daycareState;continue;}
            if(TryApplyGameCornerPrizeEffect(state,effect,out var prizeState)){state=prizeState;continue;}
            if(TryApplyGameCornerCoinEffect(state,effect,out var coinState)){state=coinState;continue;}
            if(TryApplyDepartmentRooftopEffect(state,effect,out var rooftopState)){state=rooftopState;continue;}
            if(TryApplyTrainerTowerElevatorEffect(state,effect,out var towerElevatorState)){state=towerElevatorState;continue;}
            if(TryApplyTrainerTowerEffect(state,effect,out var trainerTowerState)){state=trainerTowerState;continue;}
            if(TryApplyArticunoEffect(state,effect,out var articunoState)){state=articunoState;continue;}
            if(TryApplyPowerPlantEffect(state,effect,out var powerPlantState)){state=powerPlantState;continue;}
            if(TryApplyEventIslandBattleEffect(state,effect,out var eventBattleState)){state=eventBattleState;continue;}
            if(TryApplyEventIslandFieldEffect(state,effect,out var eventIslandFieldState)){state=eventIslandFieldState;continue;}
            if(TryApplyCelioEffect(state,effect,out var celioState)){state=celioState;continue;}
            if(TryApplyFiveIslandFieldEffect(state,effect,out var fiveFieldState)){state=fiveFieldState;continue;}
            if(TryApplyFieldItemServiceEffect(state,effect,out var fieldItemServiceState)){state=fieldItemServiceState;continue;}
            if(TryApplySelphyRequestEffect(state,effect,out var selphyState)){state=selphyState;continue;}
            if(TryApplyFiveIslandItemEffect(state,effect,out var fiveItemState)){state=fiveItemState;continue;}
            if(TryApplySeafoamFieldEffect(state,effect,out var seafoamFieldState)){state=seafoamFieldState;continue;}
            if(TryApplySevenIslandFieldEffect(state,effect,out var sevenFieldState)){state=sevenFieldState;continue;}
            if(TryApplySixIslandEffect(state,effect,out var sixIslandState)){state=sixIslandState;continue;}
            if(TryApplyFourIslandEffect(state,effect,out var fourIslandState)){state=fourIslandState;continue;}
            if(TryApplySeviiRubyEffect(state,effect,out var rubyState)){state=rubyState;continue;}
            if(TryApplyTrainerFanClubEffect(state,effect,out var fanClubState)){state=fanClubState;continue;}
            if(TryApplyPokemonLeagueEffect(state,effect,out var pokemonLeagueState)){state=pokemonLeagueState;continue;}
            if(TryApplyVictoryRoadFieldEffect(state,effect,out var victoryRoadFieldState)){state=victoryRoadFieldState;continue;}
            if(TryApplyViridianGymEffect(state,effect,out var viridianGymState)){state=viridianGymState;continue;}
            if(TryApplyFirstSeviiBattleEffect(state,effect,out var seviiBattleState)){state=seviiBattleState;continue;}
            if(TryApplyFirstSeviiEffect(state,effect,out var seviiState)){state=seviiState;continue;}
            if(TryApplySilphFieldEffect(state,effect,out var silphFieldState)){state=silphFieldState;continue;}
            if(TryApplySilphGiftEffect(state,effect,out var silphGiftState)){state=silphGiftState;continue;}
            if(TryApplyCinnabarFieldEffect(state,effect,out var cinnabarFieldState)){state=cinnabarFieldState;continue;}
            if(TryApplyCinnabarEffect(state,effect,out var cinnabarState)){state=cinnabarState;continue;}
            if(TryApplySaffronGymEffect(state,effect,out var saffronGymState)){state=saffronGymState;continue;}
            if(TryApplyFuchsiaGymEffect(state,effect,out var fuchsiaGymState)){state=fuchsiaGymState;continue;}
            if(TryApplySafariEffect(state,effect,out var safariState)){state=safariState;continue;}
            if(TryApplyMewtwoEffect(state,effect,out var mewtwoState)){state=mewtwoState;continue;}
            if(TryApplySnorlaxEffect(state,effect,out var snorlaxState)){state=snorlaxState;continue;}
            if(TryApplyTowerEffect(state,effect,out var towerState)){state=towerState;continue;}
            if(TryApplyMagikarpRecordEffect(state,effect,out var magikarpRecordState)){state=magikarpRecordState;continue;}
            if(TryApplyMoveDeleterEffect(state,effect,out var moveDeleterState)){state=moveDeleterState;continue;}
            if(TryApplyMoveReminderEffect(state,effect,out var moveReminderState)){state=moveReminderState;continue;}
            if(TryApplyCopycatItemEffect(state,effect,out var copycatItemState)){state=copycatItemState;continue;}
            if(TryApplyUltimateTutorEffect(state,effect,out var ultimateTutorState)){state=ultimateTutorState;continue;}
            if(TryApplyMoveTutorEffect(state,effect,out var moveTutorState)){state=moveTutorState;continue;}
            if(TryApplyNpcTradeEffect(state,effect,out var npcTradeState)){state=npcTradeState;continue;}
            if(TryApplyNameRaterEffect(state,effect,out var nameRaterState)){state=nameRaterState;continue;}
            if(TryApplyDepartmentStoreEffect(state,effect,out var departmentState)){state=departmentState;continue;}
            if(TryApplyRocketHideoutEffect(state,effect,out var rocketState)){state=rocketState;continue;}
            if(TryApplyRockTunnelEffect(state,effect,out var rockState)){state=rockState;continue;}
            if(TryApplyItemfinderAideEffect(state,effect,out var itemfinderAideState)){state=itemfinderAideState;continue;}
            if(TryApplyRoute11Effect(state,effect,out var route11State)){state=route11State;continue;}
            if(TryApplyCityServicesEffect(state,effect,out var cityServiceState)){state=cityServiceState;continue;}
            if(TryApplyVermilionGymEffect(state,effect,out var vermilionGymState)){state=vermilionGymState;continue;}
            if(TryApplySsAnneEffect(state,effect,out var ssAnneState)){state=ssAnneState;continue;}
            if(TryApplyFieldPickupEffect(state,effect,out var pickupState)){state=pickupState;continue;}
            if(TryApplyBillRocketEffect(state,effect,out var billRocketState)){state=billRocketState;continue;}
            if(TryApplyCeladonGymEffect(state,effect,out var celadonGymState)){state=celadonGymState;continue;}
            if(TryApplyCeruleanEffect(state,effect,out var ceruleanState)){state=ceruleanState;continue;}
            if(TryApplyOldAmberEffect(state,effect,out var oldAmberState)){state=oldAmberState;continue;}
            if(TryApplyFossilLabEffect(state,effect,out var fossilLabState)){state=fossilLabState;continue;}
            if(TryApplyMtMoonEffect(state,effect,out var mtMoonState)){state=mtMoonState;continue;}
            if(TryApplyBillEffect(state,effect,out var billState)){state=billState;continue;}
            if(TryApplyWallTownMapEffect(state,effect,out var wallMapState)){state=wallMapState;continue;}
            if(TryApplyRegionalDialogueEffect(state,effect,out var regionalDialogueState)){state=regionalDialogueState;continue;}
            if(TryApplyRegionalTeaEffect(state,effect,out var teaState)){state=teaState;continue;}
            if(TryApplyFightingDojoEffect(state,effect,out var dojoState)){state=dojoState;continue;}
            if(TryApplyRegionalGiftEffect(state,effect,out var regionalGiftState)){state=regionalGiftState;continue;}
            if(TryApplySharedScriptGiftEffect(state,effect,out var scriptGiftState)){state=scriptGiftState;continue;}
            if(TryApplyMagikarpEffect(state,effect,out var magikarpState)){state=magikarpState;continue;}
            if(TryApplyRoute3Effect(state,effect,out var route3State)){state=route3State;continue;}
            if(TryApplyVictoryRoadGateEffect(state,effect,out var victoryGateState)){state=victoryGateState;continue;}
            if(TryApplyRoute22Effect(state,effect,out var route22State)){state=route22State;continue;}
            if(TryApplyNationalDexEffect(state,effect,out var nationalState)){state=nationalState;continue;}
            if(TryApplyHallOfFamePcEffect(state,effect,out var hallPcState)){state=hallPcState;continue;}
            if(TryApplyOakRatingEffect(state,effect,out var oakState)){state=oakState;continue;}
            if(TryApplyOldManEffect(state,effect,out var tutorialState)){state=tutorialState;continue;}
            if(TryApplyPcEffect(state,effect,out var pcState)){state=pcState;continue;}
            if(TryApplyFieldSurfEffect(state,effect,out var surfState)){state=surfState;continue;}
            if(TryApplyFieldStrengthEffect(state,effect,out var strengthState)){state=strengthState;continue;}
            if(TryApplyFieldFlashEffect(state,effect,out var flashState)){state=flashState;continue;}
            if(TryApplyFieldRockSmashEffect(state,effect,out var smashState)){state=smashState;continue;}
            if(TryApplyFieldCutEffect(state,effect,out var cutState)){state=cutState;continue;}
            if(TryApplyCenterEffect(state,effect,out var centerState)){state=centerState;continue;}
            if(TryApplyPewterGymEffect(state,effect,out var gymState)){state=gymState;continue;}
            if(TryApplyParcelEffect(state,effect,out var parcelState)){state=parcelState;continue;}
            switch(effect.Opcode)
            {
                case "multichoicegrid":
                    if(StoryGridMenuRules.Pending(state.Script,_content) is null)throw new InvalidDataException("campaign_grid_menu_invalid");
                    break;
                case "pokemart":state=OpenShop(state,effect);break;
                case "lock":case "lockall":break;
                case "release":case "releaseall":state=state with{Dialogue=null,SignMessage=null,MessagePointer=null,GiftDelivery=null,PrizeExchange=null,NameRater=null,NpcTrade=null,UltimateTutor=null,MoveReminder=null,MoveDeleter=null,DayCareInteraction=null,MagikarpRecord=null,FossilMenu=null};break;
                case "signmsg":state=state with{SignMessage=(state.SignMessage??new()) with{SignStyle=true}};break;
                case "normalmsg":state=state with{SignMessage=state.SignMessage is {} signStyle?signStyle with{SignStyle=false}:null};break;
                case "setrespawn":state=state with{LastHealingLocation=a[0]};break;
                case "textcolor":state=state with{TextColor=a[0]};break;
                case "playbgm":state=state with{Music=a[0]};break;
                case "savebgm":state=state with{SavedMusic=a[0]};break;
                case "fadedefaultbgm":state=state with{Music=state.SavedMusic=="MUS_DUMMY"?_content.MapDefinition(state.World.Player.MapId).Music:state.SavedMusic};break;
                case "fadeoutbgm":
                    int fadeSpeed=Int(a[0]);if(fadeSpeed is <0 or >255)throw new InvalidDataException("audio_fade_speed_invalid");
                    state=state with{PresentationTicks=Math.Max(state.PresentationTicks,16*(fadeSpeed==0?4:4*fadeSpeed))};break;
                case "playmoncry":
                    if(Int(a[1])!=0)throw new InvalidDataException("audio_cry_mode_not_supported");
                    if(Int(a[0]) is <1 or >411)throw new InvalidDataException("audio_cry_species_invalid");break;
                case "waitmoncry":break;
                case "playse":case "playfanfare":case "waitfanfare":case "waitse":break; // Semantic effects preserved in transition; actual audio remains an explicit P0 gap.
                case "msgbox":case "message":var textSymbol=a[0]=="0x0"?state.MessagePointer??throw new InvalidDataException("message_pointer_missing"):a[0];state=state with{Dialogue=new(textSymbol,textSymbol=="gStringVar4"&&state.TextBuffers.TryGetValue(textSymbol,out var bufferedTowerSpeech)?[bufferedTowerSpeech]:_content.TextPages(textSymbol),0,a.Length>1&&a[1]=="MSGBOX_YESNO",effect.Opcode=="msgbox"&&a.Length>1&&(a[1] is "MSGBOX_NPC" or "MSGBOX_SIGN"||_content.ForestTrainersEnabled&&a[1]=="MSGBOX_AUTOCLOSE"))};if(textSymbol=="gStringVar4")state=state with{TextBuffers=state.TextBuffers.Remove(textSymbol)};break;
                case "waitmessage":if(state.Dialogue is null)throw new InvalidDataException("waitmessage_without_text");break;
                case "closemessage":state=state with{Dialogue=null};break;
                case "delay":state=state with{PresentationTicks=Math.Max(state.PresentationTicks,Int(a[0]))};break;
                case "setobjectxyperm":
                    var actor=state.Actors[ActorId(a[0])];state=state with{Actors=state.Actors.SetItem(actor.LocalId,actor with{TemplateX=Int(a[1]),TemplateY=Int(a[2])})};break;
                case "setobjectmovementtype":
                    var mover=state.Actors[ActorId(a[0])];state=state with{Actors=state.Actors.SetItem(mover.LocalId,mover with{MovementType=a[1],Facing=DirectionFromType(a[1])})};break;
                case "addobject":case "removeobject":
                    var shown=state.Actors[ActorId(a[0])];
                    if(effect.Opcode=="removeobject")
                    {
                        if(shown.Active&&shown.HideFlag is not ("0" or "0x0"))state=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem(shown.HideFlag,true)}};
                        state=state with{Actors=state.Actors.SetItem(shown.LocalId,shown with{Active=false}),ObjectSlots=state.ObjectSlots?.RemoveRange(state.ObjectSlots.Where(x=>x.Value==shown.LocalId).Select(x=>x.Key))};
                    }
                    else if(!shown.Active)
                    {
                        int free=state.ObjectSlots is null?1:Enumerable.Range(1,15).FirstOrDefault(i=>!state.ObjectSlots.ContainsKey(i));
                        if(free!=0)state=state with{Actors=state.Actors.SetItem(shown.LocalId,shown with{Active=true,Invisible=false,X=shown.TemplateX,Y=shown.TemplateY}),ObjectSlots=state.ObjectSlots?.Add(free,shown.LocalId)};
                    }
                    break;
                case "turnobject":state=Face(state,ActorId(a[0]),SourceDirection(Int(a[1])));break;
                case "faceplayer":state=FaceSelectedObject(state);break;
                case "applymovement":state=ApplyMovement(state,ActorId(a[0]),a[1]);break;
                case "waitmovement":break;
                case "opendoor":case "closedoor":case "waitdooranim":break; // Door transition plan is recorded below; art animation pending explicit presentation support.
                case "warp":
                    var destination=_content.MapFromSourceSymbol(a[0]);int x,y;
                    if(a.Length==3){x=Int(a[1]);y=Int(a[2]);}else{var index=Int(a[1]);if(index>=0&&index<_content.Maps[destination].FieldWarps.Length){var w=_content.Maps[destination].FieldWarps[index];x=w.X;y=w.Y;}else{x=Int(a[2]);y=Int(a[3]);}}
                    var cell=_content.Maps[destination].GetTile(x,y)??throw new InvalidDataException("Script warp out of map");
                    var arrivalBehavior=_content.Maps[destination].SourceBehaviorAt(x,y);
                    var facing=arrivalBehavior switch{0x62=>Direction.West,0x63=>Direction.East,0x64=>Direction.South,0x65=>Direction.North,0x6c or 0x6e=>Direction.West,0x6d or 0x6f=>Direction.East,_=>Direction.South};
                    if(arrivalBehavior==0x69){y++;cell=_content.Maps[destination].GetTile(x,y)??throw new InvalidDataException("Script door exit missing.");}
                    state=state with{World=state.World with{Player=new(destination,x,y,cell.Elevation,facing)}};
                    // Source map loading initializes the script context and unlocks field controls
                    // (overworld.c CB2_LoadMap, lines 1568-1572). Safari's lockall/warp/waitstate/end scripts rely
                    // on that reset rather than an explicit releaseall in the old map.
                    if(_content.SafariEnabled&&(effect.Source.Path is SafariEntranceSource or SafariSharedSource)&&
                        (destination.Value is "map.safari-zone-center" or SafariEntranceMap))
                        state=state with{Script=state.Script with{Locked=false}};
                    state=EnterMap(state,facing);break;
                case "showmonpic":
                    var pictureSpecies=Int(a[0]);RequireSpecies(pictureSpecies);
                    if(pictureSpecies is not (1 or 4 or 7))throw new InvalidDataException("pokemon_picture_not_implemented");
                    if(Int(a[1]) is <0 or >20||Int(a[2]) is <0 or >10)throw new InvalidDataException("pokemon_picture_bounds");
                    state=state with{MonPicture=new(pictureSpecies,Int(a[1]),Int(a[2]))};break;
                case "hidemonpic":state=state with{MonPicture=null,PresentationTicks=Math.Max(state.PresentationTicks,1)};break;
                case "bufferspeciesname":
                    if(a[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))throw new InvalidDataException("string_buffer_unsupported");
                    state=state with{TextBuffers=state.TextBuffers.SetItem(a[0],_content.SourceSpeciesName(Int(a[1]))??throw new InvalidDataException("species_name_missing"))};break;
                case "givemon":
                    var species=RequireSpecies(Int(a[0]));
                    if(state.Party.Length>=6)throw new InvalidDataException("gift_pc_destination_not_implemented");
                    var held=a.Length>2?Int(a[2]):0;if(held!=0)throw new InvalidDataException("gift_held_item_not_implemented");
                    var created=PokemonFactory.Create(_content.PokemonContent!.Catalog,new(species.Definition.Id,Int(a[1]),state.TrainerId,IvPolicy.Random,PersonalityPolicy.Random),state.World.Rng);
                    var met=_content.MapDefinition(state.World.Player.MapId).RegionMapSection;if(string.IsNullOrEmpty(met))throw new InvalidDataException("met_location_missing");
                    var owned=new PartyPokemon(state.NextPokemonId,created.Pokemon,species.Name,state.PlayerName,state.PlayerGender,2,met,Int(a[1]),4,"ITEM_POKE_BALL","ITEM_NONE");
                    state=state with{World=state.World with{Rng=created.Rng},Party=state.Party.Add(owned),NextPokemonId=checked(state.NextPokemonId+1),SeenSpecies=state.SeenSpecies.Add(species.Definition.SourceSpeciesId),CaughtSpecies=state.CaughtSpecies.Add(species.Definition.SourceSpeciesId),Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",0)}};break;
                case "fadescreen":
                    if(a[0] is not ("FADE_TO_BLACK" or "FADE_FROM_BLACK"))throw new InvalidDataException("screen_fade_mode_unsupported");
                    state=state with{ScreenIsBlack=a[0]=="FADE_TO_BLACK",PresentationTicks=16};break;
                case "trainerbattle_double":case "trainerbattle_rematch_double":state=BeginDoubleBattle(state,effect);break;
                case "trainerbattle_no_intro":case "trainerbattle_single":state=BeginForestTrainerBattle(state,effect);break;
                case "trainerbattle_earlyrival":state=effect.Source.Path=="data/maps/Route22/scripts.inc"?BeginRoute22RivalBattle(state,effect):BeginLabBattle(state,effect);break;
                case "special":
                    if(a[0]=="SetWalkingIntoSignVars"){state=state with{SignMessage=(state.SignMessage??new()) with{Cancelable=true,InhibitTicks=6}};break;}
                    if(a[0]=="DisableMsgBoxWalkaway"){state=state with{SignMessage=(state.SignMessage??new()) with{WalkawayDisabled=true}};break;}
                    if(a[0] is "BedroomPC" or "Script_SetHelpContext" or "AnimatePcTurnOn" or "AnimatePcTurnOff" or "SetHelpContextForMap")
                    {state=ApplyItemSpecial(state,effect);break;}
                    if(a[0]=="HealPlayerParty")
                    {if(!state.Party.IsEmpty)state=state with{Party=state.Party.Select(p=>Game.Domain.FirstBattle.LabBattleRewards.Heal(_content.PokemonContent!.Catalog,p)).ToImmutableArray()};break;}
                    if(a[0]!="ChangePokemonNickname")throw new InvalidDataException("native_special_unsupported: "+a[0]);
                    var partyIndex=state.Script.Variables.GetValueOrDefault("VAR_0x8004");if(partyIndex<0||partyIndex>=state.Party.Length)throw new InvalidDataException("nickname_party_index_invalid");
                    var originalName=state.Party[partyIndex].Nickname;
                    state=state with{Nickname=new(partyIndex,originalName,false),TextBuffers=state.TextBuffers.SetItem("STR_VAR_2",originalName).SetItem("STR_VAR_3",originalName)};break;
                case "waitstate":
                    if(state.WildBattle is not null)state=state with{Script=state.Script with{Wait=state.Script.Wait! with{Kind=StoryWaitKind.Battle}}};
                    else if(state.OldManTutorial is not null)state=state with{Script=state.Script with{Wait=state.Script.Wait! with{Kind=StoryWaitKind.Battle}}};
                    else if(state.ItemMenu is not null)state=state with{Script=state.Script with{Wait=state.Script.Wait! with{Kind=StoryWaitKind.Inventory}}};
                    else if(state.Nickname is {Submitted:false})state=state with{Script=state.Script with{Wait=state.Script.Wait! with{Kind=StoryWaitKind.Nickname}}};break;
                default:if(!TryApplySourceItemEffect(state,effect,out state))throw new InvalidDataException("campaign_effect_unsupported: "+effect.Opcode+" at "+effect.Source.Path+":"+effect.Source.Symbol);break;
            }
        }
        return state;
    }
    private (PokemonSpeciesDefinition Definition,string Name) RequireSpecies(int sourceId)
    {
        var content=_content.PokemonContent??throw new InvalidDataException("pokemon_content_missing");
        var definition=content.Catalog.Species.Values.SingleOrDefault(x=>x.SourceSpeciesId==sourceId)??throw new InvalidDataException("pokemon_species_not_supported: "+sourceId);
        return (definition,content.SpeciesNames[sourceId]);
    }
    private CampaignState ApplyMovement(CampaignState state,string actorId,string program)=>ApplyMovementActions(state,actorId,_content.Movement(program),program);
    private CampaignState ApplyMovementActions(CampaignState state,string actorId,ImmutableArray<string> actions,string? sourceProgram=null)
    {
        var path=ImmutableArray.CreateBuilder<TilePosition>();
        var actor=actorId=="LOCALID_PLAYER"?null:state.Actors[actorId];var position=actor is null?state.World.Player:new(state.World.Player.MapId,actor.X,actor.Y,actor.Elevation,actor.Facing);
        path.Add(position);
        bool visible=actor is null?state.PlayerVisible:!actor.Invisible;
        bool facingLocked=false;
        bool dottedThiefFall=_content.SixIslandEnabled&&sourceProgram=="SixIsland_DottedHole_SapphireRoom_Movement_ThiefFallIn"&&
            actorId=="LOCALID_DOTTED_HOLE_THIEF"&&position.MapId.Value==SixIslandSapphireMap&&position.X==5&&position.Y==0&&
            actions.SequenceEqual(new[]{"face_down","disable_anim","slide_down","slide_down","slide_down","slide_down","slide_down","slide_down","slide_down","slide_down","slide_down","restore_anim","step_end"});
        foreach(var action in actions)
        {
            if(action=="step_end")break;
            if(dottedThiefFall&&action is "disable_anim" or "restore_anim")continue;
            if(action=="lock_facing_direction"){facingLocked=true;continue;}
            if(action=="unlock_facing_direction"){facingLocked=false;continue;}
            if(action is "set_invisible" or "set_visible"){visible=action=="set_visible";continue;}
            if(action is "emote_exclamation_mark" or "emote_question_mark"||action.StartsWith("delay_",StringComparison.Ordinal))continue;
            if(action=="nurse_joy_bow"){if(!_content.ViridianCenterEnabled||!IsSupportedHealingCenter(state.World.Player.MapId)||actor is null||actor.LocalId!=HealingNurse(state.World.Player.MapId).LocalId)throw new InvalidDataException("nurse_bow_context_invalid");position=position with{Facing=facingLocked?position.Facing:Direction.South};path.Add(position);continue;}
            if(action=="face_original_direction")
            {
                // Source event_object_movement.c uses the current movement type's initial
                // facing, not the facing captured before talking. Bound support to fixed
                // facing types until the full source movement-direction table is imported.
                if(actor?.MovementType is not ("MOVEMENT_TYPE_FACE_UP" or "MOVEMENT_TYPE_FACE_DOWN" or "MOVEMENT_TYPE_FACE_LEFT" or "MOVEMENT_TYPE_FACE_RIGHT"))
                    throw new InvalidDataException("original_facing_movement_type_unsupported");
                position=position with{Facing=facingLocked?position.Facing:DirectionFromType(actor.MovementType)};path.Add(position);continue;
            }
            if(action=="face_player"){position=position with{Facing=facingLocked?position.Facing:Opposite(state.World.Player.Facing)};continue;}
            if(action=="jump_2_down"&&_content.ViridianGymEnabled)
            {
                if(actor is not null||sourceProgram!="ViridianCity_Movement_JumpDownLedge"||position.MapId.Value!="map.viridian-city"||
                    position.X!=36||position.Y!=11||position.Elevation!=3||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR")!=0||
                    !actions.SequenceEqual(new[]{"jump_2_down","step_end"})||_content.Maps[position.MapId].GetTile(36,13) is not {Blocked:false,Elevation:3})
                    throw new InvalidDataException("viridian_gym_locked_door_movement_context");
                position=position with{Y=13,Facing=facingLocked?position.Facing:Direction.South};path.Add(position);continue;
            }
            var suffix=action.Split('_')[^1];var direction=suffix switch{"up"=>Direction.North,"down"=>Direction.South,"left"=>Direction.West,"right"=>Direction.East,_=>throw new InvalidDataException("movement_unsupported: "+action)};
            bool scriptedRun=action.StartsWith("player_run_",StringComparison.Ordinal);
            if(scriptedRun&&actor is not null)throw new InvalidDataException("movement_player_run_actor_unsupported");
            bool walk=scriptedRun||dottedThiefFall&&action=="slide_down"||action.StartsWith("walk_",StringComparison.Ordinal)&&!action.StartsWith("walk_in_place",StringComparison.Ordinal);
            if(!walk&&!action.StartsWith("walk_in_place",StringComparison.Ordinal)&&!action.StartsWith("face_",StringComparison.Ordinal))throw new InvalidDataException("movement_unsupported: "+action);
            var (dx,dy)=walk?Delta(direction):(0,0);var tile=TrainerTowerMovementMaps(state)[position.MapId].GetTile(position.X+dx,position.Y+dy);
            // The original Cinnabar boat starts at x=30 and approaches to x=25,
            // beyond the 24-cell map. Its authenticated border blocks have elevation
            // zero, so the source object retains its elevation. This exception never
            // admits a player, a general object, or an arbitrary off-map movement.
            bool boatBorder=tile is null&&_content.FirstSeviiEnabled&&actorId=="LOCALID_CINNABAR_SEAGALLOP"&&
                sourceProgram=="CinnabarIsland_Movement_BoatArrive"&&position.MapId.Value=="map.cinnabar-island"&&
                actor is {TemplateX:30,Elevation:1}&&actor.TemplateY==position.Y&&position.Y is 8 or 12&&
                position.X is >=26 and <=30&&dx==-1&&dy==0;
            if(tile is null&&!boatBorder)throw new InvalidDataException("scripted_move_out_of_bounds");
            position=position with{X=position.X+dx,Y=position.Y+dy,Facing=facingLocked?position.Facing:direction,Elevation=tile is null||tile.Elevation==0?position.Elevation:tile.Elevation};path.Add(position);
        }
        // Source SetObjectEventDirection still changes movement direction while facing is locked.
        // Current source programs balance this flag locally; cross-program locks need persistent state.
        if(facingLocked)throw new InvalidDataException("movement_facing_lock_unbalanced");
        state=actor is null?state with{World=state.World with{Player=position},PlayerVisible=visible}:state with{Actors=state.Actors.SetItem(actorId,actor with{X=position.X,Y=position.Y,Elevation=position.Elevation,Facing=position.Facing,Invisible=!visible})};
        if(actor is null)state=RefreshActors(state);
        return state with{Motions=state.Motions.Add(new(actorId,path.ToImmutable(),actions))};
    }
    private CampaignState RefreshActors(CampaignState state)
    {
        if(state.ObjectSlots is not null)return RefreshSlottedActors(state);
        var p=state.World.Player;
        bool InView(int x,int y)=>x+7>=p.X-2&&x+7<=p.X+17&&y+7>=p.Y&&y+7<=p.Y+16;
        var actors=state.Actors;
        foreach(var actor in actors.Values.OrderBy(x=>x.SourceLocalId))
        {
            var next=actor;
            if(next.Active&&!InView(next.X,next.Y)&&!InView(next.TemplateX,next.TemplateY))next=next with{Active=false};
            if(!next.Active&&InView(next.TemplateX,next.TemplateY)&&(next.HideFlag is "0" or "0x0"||!state.Script.Flags.GetValueOrDefault(next.HideFlag)))
                next=next with{Active=true,Invisible=false,X=next.TemplateX,Y=next.TemplateY,Facing=DirectionFromType(next.MovementType)};
            actors=actors.SetItem(next.LocalId,next);
        }
        return state with{Actors=actors};
    }
    private static CampaignState Face(CampaignState state,string actorId,Direction direction)=>actorId=="LOCALID_PLAYER"?state with{World=state.World with{Player=state.World.Player with{Facing=direction}}}:state with{Actors=state.Actors.SetItem(actorId,state.Actors[actorId] with{Facing=direction})};
    private static CampaignActor? VisibleActorAt(CampaignState state,int x,int y)=>state.Actors.Values.FirstOrDefault(a=>a.Active&&a.X==x&&a.Y==y);
    private Direction DirectionFromType(string type)=>_content.Route3Enabled&&type is "MOVEMENT_TYPE_WANDER_UP_AND_DOWN" or "MOVEMENT_TYPE_WALK_UP_AND_DOWN" or "MOVEMENT_TYPE_FACE_UP_AND_LEFT" or "MOVEMENT_TYPE_FACE_UP_AND_RIGHT" or "MOVEMENT_TYPE_FACE_UP_LEFT_AND_RIGHT"?Direction.North:_content.Route3Enabled&&type is "MOVEMENT_TYPE_WANDER_LEFT_AND_RIGHT" or "MOVEMENT_TYPE_WALK_LEFT_AND_RIGHT" or "MOVEMENT_TYPE_FACE_LEFT_AND_RIGHT"?Direction.West:_content.Route3Enabled&&type is "MOVEMENT_TYPE_WANDER_RIGHT_AND_LEFT" or "MOVEMENT_TYPE_WALK_RIGHT_AND_LEFT"?Direction.East:type=="MOVEMENT_TYPE_FACE_DOWN_AND_UP"?Direction.South:type.EndsWith("_UP",StringComparison.Ordinal)?Direction.North:type.EndsWith("_LEFT",StringComparison.Ordinal)?Direction.West:type.EndsWith("_RIGHT",StringComparison.Ordinal)?Direction.East:Direction.South;
    private static int SourceDirectionValue(Direction direction)=>direction switch{Direction.South=>1,Direction.North=>2,Direction.West=>3,_=>4};
    private static Direction SourceDirection(int value)=>value switch{1=>Direction.South,2=>Direction.North,3=>Direction.West,4=>Direction.East,_=>throw new InvalidDataException("source_direction_invalid")};
    private static Direction Opposite(Direction d)=>d switch{Direction.North=>Direction.South,Direction.South=>Direction.North,Direction.East=>Direction.West,_=>Direction.East};
    private static (int,int) Delta(Direction d)=>d switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),_=>(-1,0)};
}
