using System.Collections.ObjectModel;
using System.Text.Json;
namespace Game.Presentation.BattleAnimations;
public readonly record struct AnimationSlot
{
    public int Value { get; }
    public AnimationSlot(int value) => Value = value is >= 0 and < 4 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    public bool IsPlayer => (Value & 1) == 0;
    // Derived navigation is not snapshot data; serializing it recursively never terminates.
    [System.Text.Json.Serialization.JsonIgnore]
    public AnimationSlot Partner => new(Value ^ 2);
}
public readonly record struct PixelPoint(int X, int Y);
public sealed record AnimationBattler(AnimationSlot Slot, string Asset, int YOffset, int Elevation, int SourceWidth, int SourceHeight)
{
    public PixelPoint? DisplayCenter {get;init;}
    public bool Visible {get;init;} = true;
    public SourceBattlerVisibility? SourceVisibility {get;init;}
    public int Subpriority => Slot.Value switch {0=>30,1=>40,2=>20,_=>50};
    public int BackgroundRank => Slot.Value is 0 or 3 ? 2 : 1;
    public PixelPoint Base(bool doubles) => (doubles ? new PixelPoint[]{new(32,80),new(200,40),new(90,88),new(152,32)} : [new(72,80),new(176,40),new(48,40),new(112,80)])[Slot.Value];
    public PixelPoint Center(bool doubles, bool animationTarget = false)
    {
        var p = Base(doubles);
        int y = (p.Y + YOffset - (Slot.IsPlayer ? 0 : Elevation)) & 255;
        if (animationTarget) y = Math.Min(104, y + (Slot.IsPlayer ? 8 : 0));
        return new(p.X,y);
    }
}
public sealed record CommittedMoveAnimation(string BattleId, long Sequence, int MoveId, AnimationSlot Actor, AnimationSlot Target)
{ public int? StatAnimationArgument {get;init;} public string? GeneralAnimationLabel {get;init;} public byte? MoveTurn {get;init;} public byte? Friendship {get;init;} public byte? ActorHpQuarterTier {get;init;} public ushort? MovePower {get;init;} public byte? FuryCutterCounter {get;init;} public byte? RolloutCounter {get;init;} public SourceAnimationTerrain? SourceTerrain {get;init;} public int? MoveDamage {get;init;} public ushort? AnimationWeather {get;init;} public bool? IsHealingMove {get;init;} public uint? VisualSeed {get;init;} }
public sealed record SourceAnimationCommand(string Op, string[] Args, int Line);
public sealed record SourceSpriteAnimFrame(int Tile,int Duration,bool HFlip,bool VFlip);
public sealed record SourceSpriteFrameLoop(int At,int Start,int Count);
public sealed record SourceSpriteSubsprite(int X,int Y,int Width,int Height,int TileOffset,int Priority);
public sealed record SourceSpriteRecipe(string Template,string Asset,string Callback,bool Blend,SourceSpriteAnimFrame[][] Animations)
{
    public SourceSpriteSubsprite[] Subsprites{get;init;}=[];
    public int Width{get;init;}=32;
    public int Height{get;init;}=32;
    public int[] LoopStarts{get;init;}=[];
    public SourceSpriteFrameLoop[][] FrameLoops{get;init;}=[];
    public SourceSpriteAffineCommand[][] AffineAnimations{get;init;}=[];
    public bool AffineDoubleSize{get;init;}
}
public sealed record SourceAnimationScript(int MoveId, string Label, SourceAnimationCommand[] Commands)
{
    // Source goto destinations are canonical programs, not new move invocations.
    public string[] ProgramReferences {get;init;} = [];
    internal IReadOnlyDictionary<string,SourceAnimationScript> TailPrograms {get;init;} = new Dictionary<string,SourceAnimationScript>();
    public IReadOnlyDictionary<string,SourceAnimationCommand[]> Subroutines {get;init;} = new Dictionary<string,SourceAnimationCommand[]>();
    public IReadOnlyDictionary<string,int> Labels {get;init;} = new Dictionary<string,int>();
    public IReadOnlyDictionary<string,IReadOnlyDictionary<string,int>> SubroutineLabels {get;init;} = new Dictionary<string,IReadOnlyDictionary<string,int>>();
    public string[] SpriteDependencies {get;init;} = [];
    public string[] MaterialDependencies {get;init;} = [];
    public string[] OverlayDependencies {get;init;} = [];
    public IReadOnlyDictionary<string,ushort[]> OverlayPalettes {get;init;} = new Dictionary<string,ushort[]>();
    public int[] SineTable {get;init;} = [];
    public IReadOnlyDictionary<string,SourceTaskAffineCommand[]> TaskAffinePrograms {get;init;} = new Dictionary<string,SourceTaskAffineCommand[]>();
    public IReadOnlyDictionary<string,int> SpriteTags {get;init;} = new Dictionary<string,int>();
    public IReadOnlyDictionary<int,ushort[]> SpritePalettes {get;init;} = new Dictionary<int,ushort[]>();
    public IReadOnlyDictionary<int,ushort[][]> SpritePaletteGroups {get;init;} = new Dictionary<int,ushort[][]>();
    public IReadOnlyDictionary<string,int> SoundEffects {get;init;} = new Dictionary<string,int>();
    public IReadOnlyDictionary<int,ushort[]> BackgroundPalettes {get;init;} = new Dictionary<int,ushort[]>();
    public IReadOnlyDictionary<string,int> BackgroundSymbols {get;init;} = new Dictionary<string,int>();
    public IReadOnlyDictionary<string,SourceSpriteRecipe> SpriteRecipes {get;init;} = new Dictionary<string,SourceSpriteRecipe>();
}
public sealed record SourceAnimationSound(string Id, int Pan, int Frame)
{
    public int? PanningPlayer {get;init;}
    public bool ApplyPanning {get;init;} = true;
    public void ValidateSourcePlayer(int sourcePlayer)
    {
        if(sourcePlayer is not (1 or 2)||PanningPlayer is {} player&&sourcePlayer!=player)
            throw new InvalidOperationException("Source animation panning player differs from owned SE song: "+Id);
    }
}
public sealed record SourceAnimationDiagnostic(int MoveId, string Script, int SourceLine, string Operation, string Reason);
public sealed class SourceAnimationException(SourceAnimationDiagnostic diagnostic) : Exception(JsonSerializer.Serialize(diagnostic))
{
    public SourceAnimationDiagnostic Diagnostic { get; } = diagnostic;
}
public sealed record SourceAnimationCry(int Species,int Mode,int Pan,int Frame);
public interface ISourceAnimationAudio
{
    void Play(SourceAnimationSound cue);
    bool SupportsCries=>false;
    void PlayCry(SourceAnimationCry cue)=>throw new NotSupportedException("Source cry modes unavailable.");
    bool IsCryPlaying=>throw new NotSupportedException("Source cry liveness unavailable.");
    void StopAnimationCries(){}
    bool IsPlaying { get; }
    void StopAnimationEffects();
    void SetPan(int pan,int frame)=>throw new NotSupportedException("Animation audio cannot sweep pan.");
}
public sealed record HitSplatFrame(PixelPoint Center, int Scale, string Asset = "impact");
public sealed record SourceVisualSpriteFrame(int Slot,string Template,PixelPoint Position,PixelPoint Offset,int Tile,bool Visible,bool HFlip,bool Blend,int Scale,string? Asset,int Subpriority)
{
    public SourceSpriteSubsprite[] Subsprites{get;init;}=[];
    public int Width{get;init;}=32;
    public int Height{get;init;}=32;
    public bool Affine{get;init;}
    public bool DoubleSize{get;init;}
    public int MatrixA{get;init;}=256;
    public int MatrixB{get;init;}
    public int MatrixC{get;init;}
    public int MatrixD{get;init;}=256;
    public bool VFlip {get;init;}
    public bool ObjectWindow {get;init;}
    public int? PaletteSlot{get;init;}
    public int OamRank{get;init;}
    public int OamPriority{get;init;}=2;
    public int BattlerCopySlot {get;init;} = -1;
    // A copied OBJ can own a palette transform independently of its source battler.
    public SourcePaletteBlend? BattlerCopyPalette {get;init;}
    // Retained sprite materials belong to their invocation, even after script end.
    public SourcePixelAsset? Material {get;init;}
    public ushort[]? MaterialPalette {get;init;}
}
// Detached normal-state prefix preserves every source RGB555 rounding step.
public sealed record SourcePaletteBlend(int Palette,int Coefficient,ushort Color)
{ public SourcePaletteState? Unfaded {get;init;} }
public sealed record SourcePaletteState(SourcePaletteBlend? Blend,bool Inverted,bool Greyscale);
public sealed record SourceBattlerBackgroundCopy(int Slot,PixelPoint Offset,SourcePaletteBlend? Palette,bool Inverted,bool Greyscale);
public sealed record SourceAnimationOverlayFrame(string MaterialId,int ScrollX,int ScrollY);

public sealed record SourceAnimationFrame(int Frame, bool Active, PixelPoint[] Offsets, int[] BackgroundSlots,
    int AlphaA, int AlphaB, HitSplatFrame[] HitSplats, string[] Trace)
{
    public SourceScanlineFrame? Scanline{get;init;}
    public PixelPoint[] BackgroundScrollOffsets{get;init;}=[];
    public SourceBattlerBackgroundCopy[] StaticBackgroundCopies {get;init;}=[];
    public IReadOnlyDictionary<int,ushort[]> SpritePalettes {get;init;}=new Dictionary<int,ushort[]>();
    public ushort[][] SpritePaletteSlots {get;init;}=[];
    public SourceVisualSpriteFrame[] Sprites {get;init;} = [];
    public SourcePaletteBlend[] PaletteBlends {get;init;} = [];
    public SourcePaletteEntry[] BackgroundPaletteEntries {get;init;} = [];
    public SourceMoveBackgroundFrame? MoveBackground {get;init;}
    public SourceAnimationOverlayFrame? Overlay {get;init;}
    public SourceWindow0Frame? Window0 {get;init;}
    public SourceWindow0Frame? Window1 {get;init;}
    public SourceObjectWindowFrame? ObjectWindow {get;init;}
    public SourceBrightnessFrame? Brightness {get;init;}
    public int BlendFirstTargets {get;init;}
    public int BlendSecondTargets {get;init;}=63;
    public short[] AnimationArguments {get;init;}=[];
    public int[] RestoredObjectSlots{get;init;}=[];
    public int[] BlendedObjectSlots{get;init;}=[];
    public SourceStatMaskFrame? StatMask {get;init;}
    public SourceMetallicFrame? Metallic {get;init;}
    public int[] InvertedPalettes {get;init;} = [];
    public int[] BattlerBackgroundPriorities {get;init;} = [0,1];
    public int[] BattlerObjectPriorities {get;init;} = [2,2,2,2];
    public int[] GreyscaleSlots {get;init;} = [];
    public SourceBattlerAffine[] BattlerAffine {get;init;} = [];
    public bool BlendEnabled {get;init;}
    public bool Doubles {get;init;}
    public int[] BattlerOamRanks {get;init;} = [];
    public PixelPoint[] ObjectOffsets {get;init;} = new PixelPoint[4];
    public AnimationBattler[] Battlers {get;init;} = [];
    public SourceBattlerVisibilityFrame[] BattlerVisibility {get;init;} = [];
    public bool[] BattlerBackgroundMosaic {get;init;} = [false,false];
    public bool[] BattlerBackgroundEnabled {get;init;} = [true,true];
    public int BackgroundMosaicHorizontal {get;init;}
    public int BackgroundMosaicVertical {get;init;}
}

/// <summary>Translated bounded original scripts/callbacks. No gameplay state or RNG.</summary>
public sealed partial class SourceBattleAnimation
{
    public const double SourceFrameSeconds=280896d/16777216;
    public static readonly ReadOnlyCollection<string> SupportedCommands=Array.AsReadOnly(new[]{"visible","invisible","setpan","jumpargeq","changebg","playse","setarg","fadetobg","fadetobgfromset","restorebg","waitbgfadein","waitbgfadeout","createsoundtask","waitsound","choosetwoturnanim","jumpifmoveturn","goto","panse","panse_adjustall","panse_adjustnone","splitbgprio","splitbgprio_all","splitbgprio_foes","waitplaysewithpan","call","return","loadspritegfx","unloadspritegfx","monbg","monbg_static","clearmonbg_static","setalpha","playsewithpan","loopsewithpan","createsprite","createvisualtask","delay","waitforvisualfinish","clearmonbg","blendoff","end"});
    private readonly SourceAnimationScript _script;
    private readonly bool _doubles;
    private readonly int _battlerCount;
    private readonly CommittedMoveAnimation _cue;
    private readonly Dictionary<int,AnimationBattler> _battlers;
    private readonly ISourceAnimationAudio _audio;
    private readonly IReadOnlyDictionary<int,int> _originalPartySpecies;
    private readonly SpriteScene _spriteScene;
    private readonly OwnedSprites _sprites;
    private readonly IReadOnlyDictionary<string,SourcePixelAsset>? _spriteMaterials;
    private readonly SourceBattlerFormMaterial? _additionalMonMaterial;
    private readonly List<VisualTask> _tasks=[];
    private readonly HashSet<int> _backgrounds=[];
    private readonly PixelPoint[] _offsets=new PixelPoint[4];
    private readonly PixelPoint[] _objectOffsets=new PixelPoint[4];
    private readonly List<string> _trace=[];
    private int _pc,_frame=-1,_wait,_alphaA,_alphaB,_clearTicks=-1,_soundWait;
    private bool _waitCallback,_cancelled,_blendEnabled;
    private readonly HashSet<string> _loaded=[];
    private int[] _oamOrder {get=>_spriteScene.Order;set=>_spriteScene.Order=value;}
    private SourceVisualSpriteFrame[] _spriteOam=[];
    private SourceBattleAnimation[] _spriteOamOwners=[];
    private int[] _oamY=>_spriteScene.Y;
    public bool Active{get;private set;}=true;
    public CommittedMoveAnimation Cue=>_cue;
    public int ActiveVisualCount=>_sprites.Count(s=>s.Counted)+_tasks.Count(t=>t.Counted);
    public bool PaletteFadeActive=>_fade?.Active==true||_hardwareFade is not null;
    public bool GraphicsLoaded=>_loaded.Count>0||_spriteScene.OwnedDynamicSpriteGraphics(this).Length>0;
    public SourceBattleAnimation(SourceAnimationScript script,CommittedMoveAnimation cue,IEnumerable<AnimationBattler> battlers,bool doubles,ISourceAnimationAudio audio,ushort[]? backgroundPalette=null,IReadOnlyDictionary<int,AnimationBattler>? battlerReplacements=null,SpriteScene? spriteScene=null,IReadOnlyDictionary<string,SourcePixelAsset>? spriteMaterials=null,IReadOnlyDictionary<int,int>? originalPartySpecies=null,SourceBattlerFormMaterial? additionalMonMaterial=null)
        :this(script,cue,battlers,doubles,audio,false,backgroundPalette,battlerReplacements,spriteScene,spriteMaterials,originalPartySpecies,additionalMonMaterial){}
    // Preparation validates against the actual shared scene without acquiring it.
    internal SourceBattleAnimation(SourceAnimationScript script,CommittedMoveAnimation cue,IEnumerable<AnimationBattler> battlers,bool doubles,ISourceAnimationAudio audio,bool deferSceneAttachment,ushort[]? backgroundPalette=null,IReadOnlyDictionary<int,AnimationBattler>? battlerReplacements=null,SpriteScene? spriteScene=null,IReadOnlyDictionary<string,SourcePixelAsset>? spriteMaterials=null,IReadOnlyDictionary<int,int>? originalPartySpecies=null,SourceBattlerFormMaterial? additionalMonMaterial=null)
    {
        if(backgroundPalette is not null&&backgroundPalette.Length!=16)throw new ArgumentException("Initial BG3 palette bank has sixteen colors.");
        _originalPartySpecies=originalPartySpecies?.ToDictionary(p=>p.Key,p=>p.Value)??new Dictionary<int,int>();
        _spriteScene=spriteScene??new();_sprites=new(this,_spriteScene);_spriteMaterials=spriteMaterials;
        if(additionalMonMaterial is {} additional)
        {
            if(additional.Pixels.Width!=64||additional.Pixels.Height!=64||additional.Pixels.Pixels.Length!=4096||string.IsNullOrEmpty(additional.Geometry.Asset))
                throw new ArgumentException("Additional source monster material must be a named 64x64 frame.");
            var pixels=additional.Pixels with{Pixels=(ushort[])additional.Pixels.Pixels.Clone(),PaletteIndices=(byte[])additional.Pixels.PaletteIndices.Clone(),Palette=(ushort[])additional.Pixels.Palette.Clone()};
            _additionalMonMaterial=new(additional.Geometry,pixels);
            var materials=spriteMaterials?.ToDictionary(p=>p.Key,p=>p.Value)??new Dictionary<string,SourcePixelAsset>();
            if(!materials.TryAdd(additional.Geometry.Asset,pixels))throw new ArgumentException("Additional source monster material key is already owned.");
            _spriteMaterials=materials;
        }
        _initialBackgroundPalette=backgroundPalette is null?null:(ushort[])backgroundPalette.Clone();
        _script=script;_programSource=script;_cue=cue;_audio=audio;_doubles=doubles;_battlerCount=doubles?4:2;_battlers=battlers.ToDictionary(x=>x.Slot.Value);
        if(_battlers.Count!=_battlerCount||Enumerable.Range(0,_battlerCount).Any(i=>!_battlers.ContainsKey(i)))throw new InvalidDataException("Animation needs every canonical battle position.");
        if(script.MoveId!=cue.MoveId||!_battlers.ContainsKey(cue.Actor.Value)||!_battlers.ContainsKey(cue.Target.Value))throw new ArgumentException("Animation participants/script differ.");
        foreach(var (slot,battler) in _battlers)_initialBattlers.Add(slot,battler);
        if(battlerReplacements is not null)foreach(var (slot,replacement) in battlerReplacements)
        {
            if(!_battlers.TryGetValue(slot,out var original)||replacement.Slot.Value!=slot||replacement.Visible!=original.Visible)
                throw new ArgumentException("Source replacement battler identity/visibility differs.");
            _battlerReplacements.Add(slot,replacement);
        }
        InitializeSourceVisibility();
        // ValidatePrograms proves bounded termination across scoped source branches.
        // A physical final goto may legitimately rejoin an earlier end.
        if(cue.GeneralAnimationLabel is {} label&&(script.MoveId!=0||script.Label!=label||!(label.StartsWith("General_",StringComparison.Ordinal)||label.StartsWith("Special_",StringComparison.Ordinal))))throw new InvalidDataException("General source label differs.");
        ValidateStatRoot();
        ValidatePrograms();_instructionBudget=checked(SourceAnimationProgramDependencies.MaximumCost(_script,_=>1)+1);
        if(!deferSceneAttachment)AttachPrepared();
    }
    private void Fail(SourceAnimationCommand c,string reason)=>throw new SourceAnimationException(new(_script.MoveId,_script.Label,c.Line,c.Op,reason));
    private void Validate(SourceAnimationCommand c)
    {
        if(!SupportedCommands.Contains(c.Op))Fail(c,"No accepted source adapter.");
        if(c.Op=="unloadspritegfx")
        {
            if(c.Args.Length!=1||!_script.SpriteTags.ContainsKey(c.Args[0]))Fail(c,"Missing source graphics tag for unload.");
            return;
        }
        if(ValidateGeneralTaskCommand(c)||ValidateMementoCommand(c)||ValidateWeatherBallCommand(c)||ValidateSpiteCommand(c)||ValidateBatonPassCommand(c)||ValidatePunchTraceCommand(c)||ValidateVoltTackleCommand(c)||ValidatePresentCommand(c)||ValidateSuperpowerCommand(c)||ValidateSmokescreenCommand(c)||ValidateWaterSportCommand(c)||ValidateSquashWaterFireCommand(c)||ValidatePainSplitCommand(c)||ValidateDamageFamilyCommand(c)||ValidateSpotlightCommand(c)||ValidateExtrasensoryCommand(c)||ValidateFrozenCubeCommand(c)||ValidatePetalPawOperand(c)||ValidateEarthBackgroundCommand(c)||ValidateScanlineBattlerCommand(c)||ValidateSurfCommand(c)||ValidateGhostSongCommand(c)||ValidateItemWindCommand(c)||ValidateFocusSoundCommand(c)||ValidateElectricWispOperand(c)||ValidateUtilityCommand(c)||ValidatePetalIceOperand(c)||ValidateSolarPincerCommand(c)||ValidatePsychicUtilityOperand(c)||ValidateHardwareFadeCommand(c)||ValidateWindowTaskOperand(c)||ValidateFocusPunchCommand(c)||ValidateRolePlayCommand(c)||ValidateRolloutCommand(c)||ValidateFeatherDanceCommand(c)||ValidateShadowCopyOperand(c)||ValidatePaletteBackupOperand(c)||ValidateOverheatOperand(c)||ValidateExpulsionCommand(c)||ValidateBattlerCopyOperand(c)||ValidatePsychoBoostCommand(c)||ValidateChargePairCommand(c)||ValidateRoundShadowChargeCommand(c)||ValidateChargedRootCommand(c)||ValidateCureCommand(c)||ValidateVisibilityRootOperand(c)||ValidateCallerPreludeOperand(c)||ValidateSupportStatusOperand(c)||ValidateLockedPairCommand(c)||ValidateCutWallCommand(c)||ValidateSocialOperand(c)||ValidateOrbDefenseCommand(c)||ValidatePowerGuardCommand(c)||ValidateConfusionKissOperand(c)||ValidateRockStatusCommand(c)||ValidateWaterRootOperand(c)||ValidateFriendshipContactCommand(c)||ValidateReactionCommand(c)||ValidateRecoveryVisualOperand(c)||ValidateWeatherOperand(c)||ValidateOrbitalParticleCommand(c)||ValidateOverlayOperand(c)||ValidateImpactEmissionOperand(c)||ValidatePowerShakeCommand(c)||ValidateFireSeedOperand(c)||ValidateImpactAdapterOperand(c)||ValidatePrimaryStatOperand(c)||ValidateProjectileAdapterOperand(c)||ValidateHyperBeamOperand(c)||ValidateVoiceCommand(c)||ValidateTrappingCommand(c)||ValidateWebCommand(c)||ValidateSelfAttackOperand(c)||ValidateConversionCommand(c)||ValidateFormChangeCommand(c)||ValidateRandomFistOperand(c)||ValidateHailCommand(c)||ValidateWishStarOperand(c)||ValidateMeteorStarOperand(c)||ValidateWaterPulseOperand(c)||ValidateMusicCommand(c)||ValidateMultiStrikeOperand(c)||ValidateHealingItemCommand(c)||ValidateFangOperand(c)||ValidateBulletSeedOperand(c)||ValidateDirtScatterOperand(c)||ValidateSnowCommand(c)||ValidateSpinningHandOperand(c)||ValidateArcProjectileOperand(c)||ValidateHornCommand(c)||ValidateNeedleOperand(c)||ValidateSlidingEffectCommand(c)||ValidateSlidingBackgroundCommand(c)||ValidateFogCommand(c)||ValidateFlyingCommand(c)||ValidateWallCommand(c)||ValidateBackgroundCommand(c)||ValidateSharedCommand(c)||ValidateTaskAffineOperand(c)||ValidateAttachedSpriteOperand(c)||ValidateElementalOperand(c)||ValidatePhysicalOperand(c)||ValidateTraceOperand(c)||ValidateExplosionOperand(c)||ValidateStatMoveOperand(c))return;
        int count=c.Op switch{"loadspritegfx" or "monbg" or "clearmonbg" or "monbg_static" or "clearmonbg_static" or "delay"=>1,"setalpha" or "playsewithpan"=>2,"createsprite"=>c.Args.FirstOrDefault() switch
        {"gBasicHitSplatSpriteTemplate" or "gScratchSpriteTemplate"=>7,"gHorizontalLungeSpriteTemplate"=>5,"gCuttingSliceSpriteTemplate" or "gSlashSliceSpriteTemplate"=>6,"gFalseSwipeSliceSpriteTemplate"=>3,"gFalseSwipePositionedSliceSpriteTemplate"=>4,"gSlideMonToOffsetSpriteTemplate" or "gFistFootSpriteTemplate"=>8,"gSlideMonToOriginalPosSpriteTemplate"=>6,_=>-1},"createvisualtask"=>c.Args.FirstOrDefault() is "AnimTask_Flash" or "AnimTask_StatsChange"?2:7,"loopsewithpan"=>4,_=>0};
        if(c.Args.Length!=count)Fail(c,"Command argument shape differs.");
        if(c.Op=="loadspritegfx"&&c.Args[0] is not ("ANIM_TAG_IMPACT" or "ANIM_TAG_SCRATCH" or "ANIM_TAG_CUT" or "ANIM_TAG_SLASH" or "ANIM_TAG_SLASH_2" or "ANIM_TAG_HANDS_AND_FEET"))Fail(c,"Unadmitted source graphics tag.");
        if(c.Op is "monbg" or "clearmonbg"&&c.Args[0] is not ("ANIM_ATTACKER" or "ANIM_TARGET" or "ANIM_ATK_PARTNER" or "ANIM_DEF_PARTNER")&&!byte.TryParse(c.Args[0],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out _))Fail(c,"Unknown monbg participant.");
        if(c.Op is "monbg_static" or "clearmonbg_static"&&c.Args[0] is not ("ANIM_ATTACKER" or "ANIM_TARGET" or "ANIM_ATK_PARTNER" or "ANIM_DEF_PARTNER"))Fail(c,"Unknown monbg participant.");
        if(c.Op=="createsprite"&&c.Args[0] is not ("gBasicHitSplatSpriteTemplate" or "gHorizontalLungeSpriteTemplate" or "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate"))
        {
            if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var recipe)||recipe.Callback is not ("AnimSpriteOnMonPos" or "AnimCuttingSlice" or "AnimSlashSlice" or "AnimFalseSwipeSlice" or "AnimFalseSwipePositionedSlice" or "AnimBasicFistOrFoot"))Fail(c,"Unknown sprite callback recipe.");
        }
        if(c.Op=="createvisualtask"&&c.Args[0] is not ("AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_TranslateMonEllipticalRespectSide" or "AnimTask_TranslateMonElliptical" or "AnimTask_Flash" or "AnimTask_StatsChange"))Fail(c,"Unknown task callback.");
        if(c.Op is "playsewithpan" or "loopsewithpan"&&(c.Args[0] is not ("SE_M_DOUBLE_SLAP" or "SE_M_COMET_PUNCH" or "SE_M_SCRATCH" or "SE_M_CUT" or "SE_M_RAZOR_WIND" or "SE_M_VITAL_THROW" or "SE_M_DOUBLE_TEAM" or "SE_M_VITAL_THROW2" or "SE_M_TAIL_WHIP" or "SE_M_LEER" or "SE_M_TAKE_DOWN" or "SE_M_HARDEN" or "SE_SHINY" or "SE_M_HEADBUTT" or "SE_M_MINIMIZE")||c.Args[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER")))Fail(c,"Unknown sound/pan command.");
        ValidateOperands(c);
    }
    private static int Number(string s)=>s switch{"TRUE"=>1,"FALSE"=>0,"ANIM_ATTACKER"=>0,"ANIM_TARGET"=>1,"ANIM_ATK_PARTNER"=>2,"ANIM_DEF_PARTNER"=>3,_=>int.Parse(s,System.Globalization.CultureInfo.InvariantCulture)};
    private AnimationSlot Resolve(string s)=>Number(s) switch{0=>_cue.Actor,1=>_cue.Target,_=>throw new InvalidDataException("Unknown animation participant.")};
    public SourceAnimationFrame Snapshot()=>new(_frame,Active,Enumerable.Range(0,4).Select(BattlerBackgroundOffset).ToArray(),[.._backgrounds.Order()],SnapshotAlpha(false),SnapshotAlpha(true),[.._sprites.OfType<HitSplat>().OrderBy(x=>x.Slot).Select(x=>new HitSplatFrame(x.Center,x.Scale))],[.._trace]){Scanline=_spriteScene.ScanlineFrame?.Detach(),BackgroundScrollOffsets=[ScanlineBackgroundOffset(1),ScanlineBackgroundOffset(2)],Battlers=_battlers.Values.OrderBy(b=>b.Slot.Value).ToArray(),BattlerVisibility=SourceVisibilitySnapshot(),BattlerBackgroundMosaic=(bool[])_battlerBackgroundMosaic.Clone(),BattlerBackgroundEnabled=(bool[])_battlerBackgroundEnabled.Clone(),BackgroundMosaicHorizontal=_backgroundMosaicHorizontal,BackgroundMosaicVertical=_backgroundMosaicVertical,StaticBackgroundCopies=_staticBackgroundCopies.Values.ToArray(),SpritePalettes=SpritePaletteTagSnapshot(),SpritePaletteSlots=_spritePaletteBanks.Select(p=>(ushort[])p.Clone()).ToArray(),RestoredObjectSlots=Enumerable.Range(0,_battlerCount).Where(i=>_restoredObjectOam[i]).ToArray(),BlendedObjectSlots=Enumerable.Range(0,_battlerCount).Where(i=>_blendedObjectOam[i]).ToArray(),AnimationArguments=(short[])_animationArgs.Clone(),MoveBackground=BackgroundFrame,Overlay=_overlay,Window0=_window0,Window1=_window1,ObjectWindow=_objectWindow,Brightness=_brightness,BlendFirstTargets=_blendFirstTargets,BlendSecondTargets=_blendSecondTargets,InvertedPalettes=_invertedPalettes.Order().ToArray(),BattlerBackgroundPriorities=(int[])_battlerBackgroundPriorities.Clone(),BattlerObjectPriorities=(int[])_battlerObjectOamPriorities.Clone(),Metallic=_metallic,GreyscaleSlots=[.._greyscale.Order()],BattlerAffine=Enumerable.Range(0,_battlerCount).Select(i=>new SourceBattlerAffine(_affineOamMode[i],_xScale[i],_yScale[i]){MatrixB=_matrixB[i],MatrixC=_matrixC[i]}).ToArray(),StatMask=_statMask,PaletteBlends=[.._paletteBlends.Values.OrderBy(p=>p.Palette)],BackgroundPaletteEntries=[.._fadedBackgroundEntries.OrderBy(p=>p.Key).Select(p=>new SourcePaletteEntry(p.Key,p.Value))],BlendEnabled=_blendEnabled,Doubles=_doubles,BattlerOamRanks=Enumerable.Range(0,_battlerCount).Select(i=>Array.IndexOf(_oamOrder,i)).ToArray(),ObjectOffsets=(PixelPoint[])_objectOffsets.Clone(),Sprites=SpriteOamSnapshot()};
    public bool HasResidualSprites=>!Active&&_sprites.Any(s=>!s.Counted);
    public SourceAnimationFrame Tick()=>Tick(null);
    internal SourceAnimationFrame Tick(Func<SourceBattleAnimation?>? completedMoveHandoff,bool controllerCompletion=false,bool controllerLaunch=false)
    {
        if(!_sceneAttached)throw new InvalidOperationException("A prepared animation has not acquired its scene.");
        _spriteScene.EnsureController(this);
        if(controllerCompletion&&controllerLaunch||Active==controllerCompletion)throw new InvalidOperationException("Animation is not in its requested script/controller phase.");
        _frame++;_trace.Clear();
        // callback1 then BattleMainCB2: AnimateSprites, BuildOamBuffer, palette fade, RunTasks.
        bool general=_cue.StatAnimationArgument is not null||_cue.GeneralAnimationLabel is not null;
        if(!general&&!controllerCompletion&&!controllerLaunch)
        {
            AdvanceScriptCallback();
            // The controller starts its restore table task in callback1, before
            // this frame's single AnimateSprites / BuildOam / RunTasks pass.
            if(!Active&&completedMoveHandoff?.Invoke() is {} successor)return successor.Tick();
        }
        AdvanceSourceSprites();
        for(int i=0;i<_battlerCount;i++)
        {
            var b=_battlers[i];if(!IsSourceBattlerPresent(i)){_objectOffsets[i]=default;continue;}if(SourceBattlerSpriteInvisible(i))continue;
            var center=b.DisplayCenter??b.Center(_doubles);var p=BattlerObjectOffset(i);
            int x=((center.X+p.X-32)&511)-center.X+32;if(x>255)x-=512;
            int y=((center.Y+p.Y-32)&255)-center.Y+32;
            _objectOffsets[i]=new(x,y);
        }
        Array.Copy(_affineMode,_affineOamMode,4);Array.Copy(_restoredObject,_restoredObjectOam,4);Array.Copy(_blendedObject,_blendedObjectOam,4);
        CaptureSourceVisibilityOam();
        SortSourceSprites();
        // BattleMainCB2 updates the global palette fade before RunTasks.
        _fade?.Update(this);_hardwareFade?.Update();
        RunSourceTasks();
        if(_clearTicks>=0&&++_clearTicks==2){_clearTicks=-1;ClearBattlerBackgrounds();_trace.Add("monbg-cleared");}
        // General animations run the script in the priority-10 table task, after
        // OAM construction and the priority-5 visual callback. Newly created tasks
        // therefore begin on the following frame. Dedicated move roots keep callback1.
        if(general&&!controllerCompletion&&!controllerLaunch)AdvanceScriptCallback();
        TransferHardwareFade();
        AdvanceScanlineVBlank();
        return Snapshot();
    }
    private void SortSourceSprites(AnimationBattler[]? idleBattlers=null,bool idleDoubles=false,SourceAnimationFrame? idleFrame=null)
    {
        // Source UpdateOamCoords/BuildSpritePriorities/SortSprites run after sprite
        // callbacks and before tasks. Stable ties retain the preceding frame's OAM order.
        var battlers=idleBattlers?.ToDictionary(b=>b.Slot.Value)??_battlers;
        int[] priorities=Enumerable.Repeat(1023,64).ToArray();
        for(int i=0;i<64;i++)
        {
            if(battlers.TryGetValue(i,out var b)&&(idleBattlers is null?IsSourceBattlerPresent(i):idleFrame?.BattlerVisibility.FirstOrDefault(v=>v.Slot==i)?.Present??InitialSourceVisibility(b).Present))
            {
                var center=b.DisplayCenter??b.Center(idleBattlers is null?_doubles:idleDoubles);var offset=idleBattlers is null?BattlerObjectOffset(i):idleFrame?.ObjectOffsets[i]??default;
                int priority=idleBattlers is null?_battlerObjectPriorities[i]:idleFrame?.BattlerObjectPriorities.ElementAtOrDefault(i)??2;
                priorities[i]=(priority<<8)|unchecked((byte)b.Subpriority);
                bool invisible=idleBattlers is null?SourceBattlerSpriteInvisible(i):idleFrame?.BattlerVisibility.FirstOrDefault(v=>v.Slot==i)?.OamInvisible??InitialSourceVisibility(b).SpriteInvisible;
                bool affine=idleBattlers is null?_affineMode[i]:idleFrame?.BattlerAffine.ElementAtOrDefault(i)?.DoubleSize??false;
                if(!invisible)_oamY[i]=(center.Y+offset.Y-(affine?64:32))&255;
                continue;
            }
            var sprite=_spriteScene.Sprites[i];
            if(sprite is null){_oamY[i]=160;continue;}
            var f=sprite.Frame;priorities[i]=(f.Asset is null?768:f.OamPriority<<8)|unchecked((byte)f.Subpriority);
            if(f.Visible)_oamY[i]=(f.Position.Y+f.Offset.Y-(sprite is MinimizeClone or TraceMonClone?32:f.Affine&&f.DoubleSize?f.Height:f.Height/2))&255;
        }
        _oamOrder=_oamOrder.OrderBy(i=>priorities[i]).ThenByDescending(i=>_oamY[i]>=160?_oamY[i]-256:_oamY[i]).ToArray();
        // BuildOamBuffer precedes RunTasks: later task allocations or mutations
        // become visible next frame, while sprite-phase raw children are included.
        for(int slot=0;slot<4;slot++)_battlerObjectOamPriorities[slot]=idleBattlers is null?_battlerObjectPriorities[slot]:idleFrame?.BattlerObjectPriorities.ElementAtOrDefault(slot)??2;
        var sprites=_spriteScene.Sprites.ToArray();
        _spriteOamOwners=sprites.Select(s=>s.Owner!).ToArray();
        _spriteOam=[..sprites.Select(s=>s.Frame with{OamRank=Array.IndexOf(_oamOrder,s.Slot)})];
    }
    private void Wait(int frames){_wait=frames==0?-1:frames;_waitCallback=true;}
    private void AdvanceScriptCallback()
    {if(_waitCallback){if(_wait<=0){_waitCallback=false;_wait=0;}else _wait--;}else RunScript();}
    private readonly int _instructionBudget;
    private void RunScript()
    {
        // Validated programs are acyclic with non-nested helpers. Count the full
        // expanded path so authored repeated calls never acquire a synthetic yield.
        int budget=_instructionBudget;
        while(Active&&budget-->0)
        {
            if(_pc>=Program.Length)throw new InvalidDataException("Script fell through without end.");
            var c=Program[_pc];var a=c.Args;int visualStart=_tasks.Count,soundStart=_soundTasks.Count;
            switch(c.Op)
            {
                case "setarg":_animationArgs[Number(a[0])]=unchecked((short)AnimationArgument(a[1]));break;
                case "fadetobgfromset":_backgroundFadeState=1;_tasks.Add(new FadeToBackgroundTask(_script.BackgroundSymbols[a[_cue.Target.IsPlayer?1:0]],this));break;
                case "fadetobg":_backgroundFadeState=1;_tasks.Add(new FadeToBackgroundTask(_script.BackgroundSymbols[a[0]],this));break;
                case "restorebg":_backgroundFadeState=1;_tasks.Add(new FadeToBackgroundTask(null,this));break;
                case "waitbgfadeout":if(_backgroundFadeState!=2)return;break;
                case "waitbgfadein":if(_backgroundFadeState!=0)return;break;
                case "jumpargeq":
                    if(_animationArgs[Number(a[0])]==unchecked((short)AnimationArgument(a[1]))){TransferProgram(a[2]);continue;}
                    break;
                case "changebg":SetMoveBackground(_script.BackgroundSymbols[a[0]]);break;
                case "jumpifmoveturn":
                    if(_cue.MoveTurn!.Value==Number(a[0])){TransferProgram(a[1]);continue;}
                    break;
                case "choosetwoturnanim":
                    TransferProgram(a[_cue.MoveTurn!.Value&1]);continue;
                case "goto":
                    TransferProgram(a[0]);continue;
                case "call":
                    if(_subroutine is not null||!_programSource.Subroutines.TryGetValue(a[0],out _subroutine))Fail(c,"Missing or recursive subroutine.");
                    _programLabels=LabelsFor(a[0]);_returnPc=_pc+1;_pc=0;continue;
                case "return":if(_subroutine is null)Fail(c,"Return outside subroutine.");_subroutine=null;_programLabels=_programSource.Labels;_pc=_returnPc;continue;
                case "loadspritegfx":LoadSpriteGraphics(a[0]);_pc++;Wait(1);return;
                case "unloadspritegfx":_loaded.Remove(a[0]);FreeSpritePalette(_script.SpriteTags[a[0]]);break;
                case "monbg":
                    var backgroundSlot=Number(a[0]) is 0 or 2?_cue.Actor:_cue.Target;
                    MoveBattlerToBackground(backgroundSlot.Value,true);
                    if(_doubles)MoveBattlerToBackground(backgroundSlot.Partner.Value,true);
                    break;
                case "monbg_static":
                    var staticSlot=a[0] is "ANIM_ATTACKER" or "ANIM_ATK_PARTNER"?_cue.Actor:_cue.Target;
                    MoveBattlerToStaticBackground(staticSlot.Value);
                    if(_doubles)MoveBattlerToStaticBackground(staticSlot.Partner.Value);
                    break;
                case "clearmonbg_static":
                    var clearSlot=a[0] is "ANIM_ATTACKER" or "ANIM_ATK_PARTNER"?_cue.Actor:_cue.Target;
                    RestoreBattlerObject(clearSlot.Value);
                    if(_doubles)RestoreBattlerObject(clearSlot.Partner.Value);
                    _tasks.Add(new ClearStaticBattlerBackgroundTask(clearSlot.Value,_doubles?clearSlot.Partner.Value:-1){Priority=5});
                    break;
                case "splitbgprio_all":SplitBackgroundPriority();break;
                case "splitbgprio":if(Resolve(a[0]).Value is 0 or 3)SplitBackgroundPriority();break;
                case "splitbgprio_foes":if(_cue.Actor.IsPlayer!=_cue.Target.IsPlayer&&Resolve(a[0]).Value is 0 or 3)SplitBackgroundPriority();break;
                case "panse_adjustall":
                case "panse_adjustnone":
                    int AdjustSweep(int value)=>c.Op=="panse_adjustall"?AdjustPanning2(value):value;
                    int adjustedStart=AdjustSweep(SoundPan(a[1])),adjustedTarget=AdjustSweep(SoundPan(a[2])),adjustedStep=AdjustSweep(Number(a[3]));
                    _audio.Play(new(a[0],adjustedStart,_frame));_soundTasks.Add(new PanSweep(adjustedStart,adjustedTarget,adjustedStep,Number(a[4]),false));break;
                case "panse":
                    int initial=AdjustPanning(SoundPan(a[1])),targetPan=AdjustPanning(a[2]=="SOUND_PAN_ATTACKER"?-64:63);
                    _audio.Play(new(a[0],initial,_frame));_soundTasks.Add(new PanSweep(initial,targetPan,Number(a[3]),Number(a[4])));break;
                case "setalpha":SetAlphaBlend(0,63,Number(a[0]),Number(a[1]));break;
                case "createsprite":
                    WriteAnimationArguments(a.AsSpan(3));
                    int slot=RequireFreeSpriteSlot();
                    _oamY[slot]=a[0] is "gHorizontalLungeSpriteTemplate" or "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate"?160:0;
                    int modifier=Number(a[2]);int priority=Math.Max(3,_battlers[Resolve(a[1]).Value].Subpriority+(modifier>=64?modifier-64:-modifier));
                    if(TryCreateWeatherBallSprite(c,slot,priority)||TryCreateBatonPassSprite(c,slot,priority)||TryCreateVoltTackleSprite(c,slot,priority)||TryCreatePresentSprite(c,slot,priority)||TryCreateSuperpowerSprite(c,slot,priority)||TryCreateSmokescreenSprite(c,slot,priority)||TryCreateSquashWaterFireSprite(c,slot,priority)||TryCreatePainSplitSprite(c,slot,priority)||TryCreateDamageFamilySprite(c,slot,priority)||TryCreateSpotlightSprite(c,slot,priority)||TryCreatePetalPawSprite(c,slot,priority)||TryCreateEarthBackgroundSprite(c,slot,priority)||TryCreateScanlineBattlerSprite(c,slot,priority)||TryCreateGhostSongSprite(c,slot,priority)||TryCreateItemWindSprite(c,slot,priority)||TryCreateFocusSoundSprite(c,slot,priority)||TryCreateElectricWispSprite(c,slot,priority)||TryCreateUtilitySprite(c,slot,priority)||TryCreatePetalIceSprite(c,slot,priority)||TryCreateSolarPincerSprite(c,slot,priority)||TryCreatePsychicUtilitySprite(c,slot,priority)||TryCreateFocusPunchSprite(c,slot,priority)||TryCreateFeatherDanceSprite(c,slot,priority)||TryCreateOverheatSprite(c,slot,priority)||TryCreateExpulsionSprite(c,slot,priority)||TryCreateBattlerCopySprite(c,slot,priority)||TryCreatePsychoBoostSprite(c,slot,priority)||TryCreateChargePairSprite(c,slot,priority)||TryCreateRoundShadowChargeSprite(c,slot,priority)||TryCreateChargedRootSprite(c,slot,priority)||TryCreateCureSprite(c,slot,priority)||TryCreateVisibilityRootSprite(c,slot,priority)||TryCreateCallerPreludeSprite(c,slot,priority)||TryCreateSupportStatusSprite(c,slot,priority)||TryCreateCutWallSprite(c,slot,priority)||TryCreateSocialSprite(c,slot,priority)||TryCreateOrbDefenseSprite(c,slot,priority)||TryCreatePowerGuardSprite(c,slot,priority)||TryCreateConfusionKissSprite(c,slot,priority)||TryCreateRockStatusSprite(c,slot,priority)||TryCreateWaterRootSprite(c,slot,priority)||TryCreateFriendshipContactSprite(c,slot,priority)||TryCreateReactionSprite(c,slot,priority)||TryCreateRecoveryVisualSprite(c,slot,priority)||TryCreateWeatherSprite(c,slot,priority)||TryCreateOrbitalParticle(c,slot,priority)||TryCreateOverlaySprite(c,slot,priority)||TryCreateImpactEmissionSprite(c,slot,priority)||TryCreateFireSeedSprite(c,slot,priority)||TryCreateImpactAdapterSprite(c,slot,priority)||TryCreatePrimaryStatSprite(c,slot,priority)||TryCreateProjectileAdapterSprite(c,slot,priority)||TryCreateHyperBeamSprite(c,slot,priority)||TryCreateVoiceSprite(c,slot,priority)||TryCreateTrappingSprite(c,slot,priority)||TryCreateWebSprite(c,slot,priority)||TryCreateSelfAttackSprite(c,slot,priority)||TryCreateConversionSprite(c,slot,priority)||TryCreateRandomFistSprite(c,slot,priority)||TryCreateWishStarSprite(c,slot,priority)||TryCreateMeteorStarSprite(c,slot,priority)||TryCreateWaterPulseSprite(c,slot,priority)||TryCreateMusicSprite(c,slot,priority)||TryCreateMultiStrikeSprite(c,slot,priority)||TryCreateHealingItemSprite(c,slot,priority)||TryCreateFangSprite(c,slot,priority)||TryCreateBulletSeedSprite(c,slot,priority)||TryCreateDirtScatterSprite(c,slot,priority)||TryCreateSnowSprite(c,slot,priority)||TryCreateSpinningHandSprite(c,slot,priority)||TryCreateArcProjectileSprite(c,slot,priority)||TryCreateHornSprite(c,slot,priority)||TryCreateNeedleSprite(c,slot,priority)||TryCreateFogSprite(c,slot,priority)||TryCreateFlyingSprite(c,slot,priority)||TryCreateWallSprite(c,slot,priority)||TryCreateTaskAffineSprite(c,slot,priority)||TryCreateAttachedSprite(c,slot,priority)||TryCreateElementalSprite(c,slot,priority))break;
                    if(a[0]=="gBowMonSpriteTemplate")_sprites.Add(new BowMon(slot,priority,Number(a[3])));
                    else if(a[0]=="gFlashingHitSplatSpriteTemplate")_sprites.Add(new FlashingHit(slot,priority,a.Skip(3).Select(Number).ToArray(),this));
                    else if(a[0]=="gSimplePaletteBlendSpriteTemplate")_sprites.Add(new SimplePaletteSprite(slot,priority,a.Skip(3).Select(PaletteNumber).ToArray(),this));
                    else if(a[0]=="gVerticalDipSpriteTemplate")_sprites.Add(new VerticalDip(slot,priority,Resolve(a[5]),Number(a[3]),Number(a[4])));
                    else if(a[0] is "gStompFootSpriteTemplate" or "gKarateChopSpriteTemplate" or "gJumpKickSpriteTemplate" or "gSlidingKickSpriteTemplate")_sprites.Add(new PhysicalFoot(slot,priority,a[0],a.Skip(3).Select(Number).ToArray(),this));
                    else if(a[0]=="gComplexPaletteBlendSpriteTemplate")_sprites.Add(new ComplexPaletteSprite(slot,priority,a.Skip(3).Select(PaletteNumber).ToArray(),this));
                    else if(a[0]=="gBasicHitSplatSpriteTemplate")
                    {
                        var target=Resolve(a[5]);var center=_battlers[target.Value].Center(_doubles,true);
                        int x=Number(a[3])*InitialXDirection();
                        if(!_loaded.Contains("ANIM_TAG_IMPACT"))Fail(c,"Impact graphics have not been loaded.");
                        _sprites.Add(new HitSplat(slot,priority,new(center.X+x,center.Y+Number(a[4])),Number(a[6])));
                    }
                    else if(a[0]=="gHorizontalLungeSpriteTemplate")_sprites.Add(new Lunge(slot,priority,_battlers[_cue.Target.Value].Center(_doubles,true),_cue.Actor,Number(a[3]),Number(a[4])));
                    else if(a[0] is "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate")_sprites.Add(new SlideSprite(slot,priority,a[0],a.Skip(3).Select(Number).ToArray(),this));
                    else
                    {
                        var recipe=_script.SpriteRecipes[a[0]];
                        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Sprite graphics have not been loaded.");
                        _sprites.Add(new AnimatedSprite(slot,priority,recipe,a.Skip(3).Select(Number).ToArray(),this));
                    }
                    break;
                case "visible":case "invisible":if(SourceAnimBattlerSlot(Number(a[0])) is {} visibilitySlot)SetSourceBattlerSpriteInvisible(visibilitySlot,c.Op=="invisible");break;
                case "createvisualtask":
                    WriteAnimationArguments(a.AsSpan(2));
                    if(TryCreateDamageFamilyTask(c)||TryCreateSpotlightTask(c)||TryCreateExtrasensoryTask(c)||TryCreateFrozenCubeTask(c)||TryCreateEarthBackgroundTask(c)||TryCreateScanlineBattlerTask(c)||TryCreateSurfTask(c)||TryCreateGhostSongTask(c)||TryCreateFocusSoundTask(c)||TryCreateElectricWispTask(c)||TryCreateUtilityTask(c)||TryRunPetalIceTask(c)||TryCreateSolarPincerTask(c))break;
                    if(a[0] is "SoundTask_PlaySE1WithPanning" or "SoundTask_PlaySE2WithPanning")
                    {_audio.Play(new(a[2],AdjustPanning(SoundPan(a[3])),_frame){PanningPlayer=a[0]=="SoundTask_PlaySE1WithPanning"?1:2});break;}
                    if(a[0] is "AnimTask_SetPsychicBackground" or "AnimTask_FadeScreenToWhite"){_tasks.Add(new PsychicBackgroundTask(a[0]=="AnimTask_FadeScreenToWhite"));break;}
                    if(a[0]=="AnimTask_RockMonBackAndForth")
                    {if(Number(a[3])!=0&&ResolveShakeParticipant(Number(a[2]),false) is {} rocking)_tasks.Add(new RockMonTask(this,rocking,Number(a[3]),Number(a[4])));break;}
                    if(a[0]=="AnimTask_SetGrayscaleOrOriginalPal")
                    {if(ResolveShakeParticipant(Number(a[2]),true) is {} paletteSlot)SetBattlerGreyscaleOrOriginal(paletteSlot.Value,Number(a[3])!=0);break;}
                    if(a[0]=="AnimTask_BlendMonInAndOut")
                    {var blendSlot=ResolveShakeParticipant(Number(a[2]),false);if(blendSlot is {} selected)_tasks.Add(new BlendMonInAndOutTask(selected,(ushort)PaletteNumber(a[3]),Number(a[4]),Number(a[5]),Number(a[6])));break;}
                    if(a[0]=="AnimTask_InvertScreenColor"){InvertSelectedPalettes(Number(a[2]),Number(a[3]),Number(a[4]));break;}
                    if(TryCreateGeneralTask(c)||TryCreateMementoTask(c)||TryRunWeatherPredicate(c)||TryCreateSpiteTask(c)||TryCreatePunchTraceTask(c)||TryCreateVoltTackleTask(c)||TryCreatePresentTask(c)||TryCreateSmokescreenTask(c)||TryCreateWaterSportTask(c)||TryCreateSquashWaterFireTask(c)||TryCreatePainSplitTask(c)||TryCreatePsychicUtilityTask(c)||TryCreateCureTask(c)||TryCreateHardwareFadeTask(c)||TryCreateWindowTask(c)||TryCreateRolePlayTask(c)||TryCreateRolloutTask(c)||TryCreateShadowCopyTask(c)||TryRunPaletteBackupTask(c)||TryCreateExpulsionTask(c)||TryCreateBattlerCopyTask(c)||TryCreateChargePairTask(c)||TryCreateVisibilityRootTask(c)||TryCreateSupportStatusTask(c)||TryCreateLockedPairTask(c)||TryCreateCutWallTask(c)||TryCreateSocialTask(c)||TryCreatePowerGuardTask(c)||TryCreateConfusionKissTask(c)||TryCreateRockStatusTask(c)||TryCreateFriendshipContactTask(c)||TryCreateReactionTask(c)||TryCreateRecoveryVisualTask(c)||TryCreateWeatherTask(c)||TryCreateOrbitalParticleTask(c)||TryCreateOverlayTask(c)||TryCreateImpactEmissionTask(c)||TryCreatePowerShakeTask(c)||TryCreatePrimaryStatTask(c)||TryCreateVoiceTask(c)||TryCreateConversionTask(c)||TryCreateFormChangeTask(c)||TryCreateHailTask(c)||TryCreateMusicTask(c)||TryRunArgumentPredicate(c)||TryCreateSlidingEffectTask(c)||TryCreateSlidingBackgroundTask(c)||TryCreateFlyingTask(c)||TryCreateTaskAffineTask(c)||TryCreateElementalTask(c))break;
                    if(a[0]=="AnimTask_SwayMon"){_tasks.Add(new SwayMonTask(this,a.Skip(2).Select(Number).ToArray()));break;}
                    if(a[0]=="AnimTask_WindUpLunge"){_tasks.Add(new WindUpLunge(this,a.Skip(2).Select(Number).ToArray()));break;}
                    if(a[0] is "AnimTask_RotateMonSpriteToSide" or "AnimTask_RotateMonToSideAndRestore"){_tasks.Add(new RotateToSide(this,Number(a[2]),Number(a[3]),Resolve(a[4]),Number(a[5]),a[0]=="AnimTask_RotateMonToSideAndRestore"));break;}
                    if(a[0]=="AnimTask_ShakeMonInPlace"){if(ResolveInPlaceShakeParticipant(Number(a[2])) is not {} inPlaceSlot)break;var p=_offsets[inPlaceSlot.Value];_offsets[inPlaceSlot.Value]=new(unchecked((short)(p.X+Number(a[3]))),unchecked((short)(p.Y+Number(a[4]))));var shakeInPlace=new InPlaceShake(inPlaceSlot,Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]));shakeInPlace.Step(this);if(!shakeInPlace.Finished)_tasks.Add(shakeInPlace);break;}
                    if(a[0]=="AnimTask_ShakeAndSinkMon"){var sinkSlot=Resolve(a[2]);_offsets[sinkSlot.Value]=new(Number(a[3]),_offsets[sinkSlot.Value].Y);var sink=new ShakeAndSinkTask(sinkSlot,Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]));sink.Step(this);if(!sink.Finished)_tasks.Add(sink);break;}
                    if(a[0]=="AnimTask_TraceMonBlended"){_tasks.Add(new TraceMonTask(Number(a[2]),Number(a[3]),Number(a[4]),Number(a[5])));break;}
                    if(a[0]=="AnimTask_BlendBattleAnimPal"){var blend=new BlendPaletteTask(a.Skip(2).Select(PaletteNumber).ToArray(),this);blend.Step(this);if(!blend.Finished)_tasks.Add(blend);break;}
                    if(a[0]=="AnimTask_Minimize"){_tasks.Add(new MinimizeTask(this));break;}
                    if(a[0]=="AnimTask_Withdraw"){_tasks.Add(new WithdrawTask(this));break;}
                    if(a[0]=="AnimTask_MetallicShine"){_tasks.Add(new MetallicTask(this,Number(a[2])==0,Number(a[3])!=0,(ushort)PaletteNumber(a[4])));break;}
                    if(a[0]=="AnimTask_BlendColorCycle"){_tasks.Add(new BlendCycleTask(this,a.Skip(2).Select(PaletteNumber).ToArray()));break;}
                    if(a[0]=="AnimTask_ScaleMonAndRestore"){_tasks.Add(new ScaleMonTask(this,a.Skip(2).Select(Number).ToArray()));break;}
                    if(a[0]=="AnimTask_StatsChange"){_tasks.Add(new StatMaskTask(this));break;}
                    if(a[0]=="AnimTask_Flash"){_tasks.Add(new FlashTask(this));break;}
                    if(a[0] is "AnimTask_TranslateMonEllipticalRespectSide" or "AnimTask_TranslateMonElliptical")
                    {var ellipse=new EllipseTask(Resolve(a[2]),Number(a[3])*(a[0]=="AnimTask_TranslateMonEllipticalRespectSide"&&!_cue.Actor.IsPlayer?-1:1),Number(a[4]),Number(a[5]),Number(a[6]));ellipse.Step(this);if(!ellipse.Finished)_tasks.Add(ellipse);break;}
                    var shakeSlot=ResolveShakeParticipant(Number(a[2]),a[0]=="AnimTask_ShakeMon2");
                    if(shakeSlot is null)break;
                    var shake=new ShakeTask(shakeSlot.Value,Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]),a[0]=="AnimTask_ShakeMon2");
                    _offsets[shake.Slot.Value]=new(shake.X,shake.Y);shake.Step(this);_tasks.Add(shake);break;
                case "createsoundtask":
                    WriteAnimationArguments(a.AsSpan(1));
                    if(a[0]=="SoundTask_FireBlast"){_soundTasks.Add(new FireBlastSound(a[1],a[2],AdjustPanning(-64),AdjustPanning(63)));break;}
                    var panningLoop=new PanningSoundLoop(a[1],AdjustPanning(SoundPan(a[2])),AdjustPanning(SoundPan(a[3])),Number(a[4]),Number(a[5]),Number(a[6]),Number(a[7]));
                    panningLoop.Step(this);if(!panningLoop.Finished)_soundTasks.Add(panningLoop);break;
                case "waitsound":
                    if(_soundTasks.Count!=0){_soundWait=0;return;}
                    if(_audio.IsPlaying)
                    {if(++_soundWait>90){_audio.StopAnimationEffects();_soundWait=0;}return;}
                    _soundWait=0;break;
                case "waitplaysewithpan":_soundTasks.Add(new DelayedSound(a[0],AdjustPanning(SoundPan(a[1])),Number(a[2])));break;
                case "loopsewithpan":
                    var loop=new SoundLoop(a[0],AdjustPanning(SoundPan(a[1])),Number(a[2]),Number(a[3]));loop.Step(this);if(!loop.Finished)_soundTasks.Add(loop);break;
                case "setpan":_audio.SetPan(AdjustPanning(SoundPan(a[0])),_frame);break;
                case "playse":_audio.Play(new(a[0],0,_frame){ApplyPanning=false});break;
                case "playsewithpan":_audio.Play(new(a[0],AdjustPanning(SoundPan(a[1])),_frame));break;
                case "delay":_pc++;Wait(Number(a[0]));return;
                case "waitforvisualfinish":if(_sprites.Any(s=>s.Counted)||_tasks.Any(t=>t.Counted))return;break;
                case "clearmonbg":RestoreMonBackgroundObjects(a[0]);_clearTicks=0;break;
                case "blendoff":ClearAlphaBlend();break;
                case "end":
                    if(_sprites.Any(s=>s.Counted)||_tasks.Any(t=>t.Counted)||_soundTasks.Count!=0||_backgrounds.Count!=0){_soundWait=0;return;}
                    if(_audio.IsPlaying&&++_soundWait<=90)return;
                    if(_audio.IsPlaying)_audio.StopAnimationEffects();
                    _soundWait=0;RestoreVisibleBattlerPriorities();Active=false;CaptureRetiredSpritePalettes();FreeLoadedSpritePalettes();return;
                default:Fail(c,"Unimplemented source operation.");break;
            }
            AssignTaskOrder(visualStart,soundStart,c);
            _trace.Add($"{c.Line}:{c.Op}");_pc++;
        }
        if(budget<=0)throw new InvalidDataException("Source script exceeded per-frame instruction bound.");
    }
    public void Cancel()=>CancelCore(_sceneAttached);
    internal void CancelCore(bool stopAudio)
    {
        if(_cancelled)return;_cancelled=true;ReleaseOwnedDynamicSpriteGraphics();Active=false;ResetBattlerMaterials();ResetSourceVisibility();_visualRandom=null;_loaded.Clear();ResetSpritePalettes();_retiredSpritePalettes.Clear();
        Array.Clear(_restoredObject);Array.Clear(_restoredObjectOam);Array.Clear(_blendedObject);Array.Clear(_blendedObjectOam);Array.Clear(_animationArgs);_unfadedBackgroundPalette=_fadedBackgroundPalette=null;
        _hardwareFade=null;_hardwareDarkening=0;BackgroundScrollX=BackgroundScrollY=0;BackgroundScreenSize=1;_moveBackground=null;_backgroundFadeState=0;_backgroundRevision=0;
        _spriteScene.CancelScanlines(this);_scanlineTerminalAlpha=null;Array.Clear(_queuedBackgroundRegisters);_queuedAlphaRegister=false;
        _invertedPalettes.Clear();Array.Fill(_battlerObjectPriorities,2);Array.Fill(_battlerObjectOamPriorities,2);SetBackgroundPriority(1,0);SetBackgroundPriority(2,1);_metallic=null;_greyscale.Clear();_fade=null;Array.Clear(_affineMode);Array.Clear(_affineOamMode);Array.Fill(_xScale,256);Array.Fill(_yScale,256);Array.Clear(_matrixB);Array.Clear(_matrixC);_statMask=null;if(stopAudio){_audio.StopAnimationEffects();_audio.StopAnimationCries();}_soundTasks.Clear();_paletteBlends.Clear();_unfadedPaletteStates.Clear();_paletteBackup=null;_fadedBackgroundEntries.Clear();_unfadedBackgroundEntries.Clear();_sprites.Clear();_spriteOam=[];_spriteOamOwners=[];_tasks.Clear();ClearBattlerBackgrounds();Array.Clear(_offsets);Array.Clear(_battlerPositionOffsets);Array.Clear(_objectOffsets);ResetSpriteCoordOffsets();_alphaA=_alphaB=0;_blendEnabled=false;_window0=null;_window1=null;_objectWindow=null;_brightness=null;_overlay=null;_blendFirstTargets=0;_blendSecondTargets=63;_clearTicks=-1;
    }
    // Existing callbacks retain their reviewed combined step; raw-aware callbacks
    // use the separate animation phase, with a fresh registry-cell read between phases.
    private void StepSourceSprite(VisualSprite sprite){sprite.Step(this);if(sprite.Finished)_sprites.Remove(sprite);}
    private bool IsReservedSpriteSlot(int slot)=>_spriteScene.Reserved(slot);
    private void AdvanceSourceSprites()=>_spriteScene.Advance();
    private int FindFreeSpriteSlot(int excluded=-1)=>_sprites.FindFreeSlot(i=>i==excluded||IsReservedSpriteSlot(i));
    private int RequireFreeSpriteSlot()
    {int slot=FindFreeSpriteSlot();return slot<SourceSpriteRegistry<VisualSprite>.Capacity?slot:throw new InvalidDataException("Source sprite capacity exhausted.");}
    internal abstract class VisualSprite(int slot,int subpriority)
    {
        public SourceBattleAnimation? Owner{get;set;}
        public int Slot{get;}=slot;public int Subpriority{get;}=subpriority;
        public bool Finished{get;protected set;}
        public virtual bool Counted=>true;
        public abstract SourceVisualSpriteFrame Frame{get;}
        public abstract void Step(SourceBattleAnimation owner);
        public virtual void AdvanceAnimation(SourceBattleAnimation owner){}
    }
    private sealed class HitSplat:VisualSprite
    {
        public PixelPoint Center{get;}public int Scale{get;}private readonly string _asset,_template;
        private int _state,_delay;
        public override SourceVisualSpriteFrame Frame=>new(Slot,_template,Center,default,0,true,false,true,Scale,_asset,Subpriority);
        public HitSplat(int slot,int subpriority,PixelPoint center,int affine,string asset="impact",string template="gBasicHitSplatSpriteTemplate"):base(slot,subpriority)
        {
            _asset=asset;_template=template;Center=center;Scale=affine switch{0=>256,1=>216,2=>176,3=>128,_=>throw new InvalidDataException("Unknown source affine animation.")};
            // CreateSpriteAndAnimate already executes callback and BeginAffineAnim once.
            if(affine==0){_state=1;_delay=7;}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            // Sprite callback precedes each affine step. End -> swap callback -> destroy
            // happen on separate source frames, not when the hold counter reaches zero.
            switch(_state){case 0:_state=1;_delay=7;break;case 1:if(_delay>0)_delay--;else _state=2;break;case 2:_state=3;break;case 3:Finished=true;break;}
        }
    }
    private sealed class Lunge(int spriteSlot,int subpriority,PixelPoint position,AnimationSlot slot,int duration,int delta):VisualSprite(spriteSlot,subpriority)
    {
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gHorizontalLungeSpriteTemplate",position,default,0,false,false,false,256,null,Subpriority);
        private readonly int _duration=duration;
        private int _remaining=duration,_delta=slot.IsPlayer?delta:-delta,_state;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state is 0 or 2)
            {
                if(_remaining>0){_remaining--;var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(p.X+_delta,p.Y);}
                else _state++;
            }
            else if(_state==1){_remaining=_duration;_delta=-_delta;_state=2;}
            else Finished=true;
        }
    }
    // Source non-affine frame engine and callback chains. Recipes carry the authenticated
    // original template, frame tiles/durations and OBJ blend mode; move IDs choose no behavior.
    private sealed class AnimatedSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private PixelPoint _position,_offset;
        private int _state,_counter,_flickers,_vx,_vy,_fx,_fy,_direction;
        private bool _ended,_invisible,_hFlip;
        private PhysicalLinear? _physicalLinear;
        private int _powderSpeed,_powderAmplitude,_powderWave;
        public AnimatedSprite(int slot,int subpriority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,subpriority)
        {
            _recipe=recipe;int anim=recipe.Callback=="AnimBreathPuff"?(owner._cue.Actor.IsPlayer?0:1):recipe.Callback=="AnimWhipHit"?(owner._cue.Actor.IsPlayer?1:0):recipe.Callback=="AnimViceGripPincer"?args[0]:recipe.Callback=="AnimBasicFistOrFoot"?args[4]:recipe.Callback=="AnimFalseSwipePositionedSlice"?1:0;
            _frames=new(recipe,anim);
            var target=owner._battlers[owner._cue.Target.Value];var center=target.Center(owner._doubles,true);
            switch(recipe.Callback)
            {
                case "AnimBreathPuff":
                    var breathActor=owner._battlers[owner._cue.Actor.Value];var breathCenter=breathActor.Center(owner._doubles,true);int breathDirection=breathActor.Slot.IsPlayer?1:-1;
                    _position=new(breathCenter.X+32*breathDirection,breathCenter.Y);_counter=52;_vx=64*breathDirection;break;
                case "AnimMovePowderParticle":_position=new(center.X+args[0],center.Y+args[1]);_counter=args[2];_powderSpeed=args[3];_powderAmplitude=args[4]*(owner._cue.Actor.IsPlayer?1:-1);_powderWave=args[5];break;
                case "AnimWhipHit":_position=new(center.X+owner.InitialXDirection()*args[0],center.Y+args[1]);break;
                case "AnimViceGripPincer":
                    int sign=args[0]==0?1:-1;_position=new(center.X+32*sign,center.Y-32*sign);_physicalLinear=new(-16*sign,16*sign,6);break;
                case "AnimBasicFistOrFoot":
                    var fistMon=owner._battlers[(args[3]==0?owner._cue.Actor:owner._cue.Target).Value];var fistCenter=fistMon.Center(owner._doubles,true);
                    int direction=owner.InitialXDirection();
                    _position=new(fistCenter.X+direction*args[0],fistCenter.Y+args[1]);_counter=args[2];break;
                case "AnimSpriteOnMonPos":
                    var mon=owner._battlers[(args[2]==0?owner._cue.Actor:owner._cue.Target).Value];
                    var p=args[3]==0?mon.Center(owner._doubles,true):mon.Base(owner._doubles);
                    _position=new(p.X+(owner._cue.Actor.IsPlayer?args[0]:-args[0]),p.Y+args[1]);break;
                case "AnimCuttingSlice":
                    _direction=args[2];_position=new(center.X+(_direction==0?args[0]:-args[0]),target.Base(owner._doubles).Y+(target.Slot.IsPlayer?8:0)+args[1]);
                    _vx=_direction==0?-1024:1024;_vy=1024;_hFlip=_direction!=0;break;
                case "AnimSlashSlice":
                    var slashMon=owner._battlers[(args[0]==0?owner._cue.Actor:owner._cue.Target).Value];var slashCenter=slashMon.Center(owner._doubles,true);
                    _position=new(slashCenter.X+args[1],slashCenter.Y+args[2]);break;
                case "AnimFalseSwipeSlice":_position=new(center.X-48,center.Y);break;
                case "AnimFalseSwipePositionedSlice":_position=new(center.X-48+args[0],center.Y);_state=4;break;
                default:throw new InvalidDataException("Unsupported source sprite callback.");
            }
            // CreateSpriteAndAnimate invokes initializer then BeginAnim immediately.

        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_invisible,_hFlip^_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){VFlip=_frames.Frame.VFlip,Width=_recipe.Width,Height=_recipe.Height};
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Callback)
            {
                case "AnimBreathPuff":
                    if(_state!=0)Finished=true;
                    else if(_counter>0){_counter--;_fx=(short)(_fx+_vx);_offset=new(_fx>>8,0);}
                    else _state=1;
                    break;
                case "AnimMovePowderParticle":
                    if(_counter<=0){Finished=true;break;}
                    _counter--;_offset=new((owner._script.SineTable[_fx]*_powderAmplitude)>>8,_fy>>8);_fy=(short)(_fy+_powderSpeed);_fx=(_fx+_powderWave)&255;break;
                case "AnimViceGripPincer":
                    if(_state==0){if(_physicalLinear!.Step(out var step))_state=1;else _offset=step;}
                    else if(_ended)Finished=true;
                    break;
                case "AnimBasicFistOrFoot":if(_state==1)Finished=true;else if(_counter>0)_counter--;else _state=1;break;
                case "AnimWhipHit":case "AnimSpriteOnMonPos":if(_ended)Finished=true;break;
                case "AnimCuttingSlice":
                    if(_state==0)
                    {
                        _fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);_vx+=_direction==0?24:-24;_vy-=24;
                        _offset=new(_fx>>8,_fy>>8);
                        if(++_counter==20){_state=1;_counter=3;}
                    }
                    else if(_state==1){if(_counter>0)_counter--;else _state=2;}
                    else Finished=true;
                    break;
                default:
                    switch(_state)
                    {
                        case 0:if(_ended)_state=_recipe.Callback=="AnimFalseSwipeSlice"?1:4;break;
                        case 1:if(++_counter>8){_counter=12;_state=2;}break;
                        case 2:if(_counter>0){_counter--;_offset=new(_offset.X+8,_offset.Y);}else _state=3;break;
                        case 3:_counter=0;_flickers=0;_state=4;break;
                        case 4:if(++_counter>1){_counter=0;_invisible=!_invisible;if(++_flickers>8)Finished=true;}break;
                    }
                    break;
            }
            if(Finished)return;
            _frames.Step();_ended=_frames.Ended;
        }
    }
    private sealed class ShakeTask(AnimationSlot slot,int x,int y,int count,int delay,bool negative):VisualTask
    {
        public AnimationSlot Slot=>slot;public int X=>x;public int Y=>y;
        private readonly int _delay=delay;
        private int _count=count,_timer=delay;
        public override void Step(SourceBattleAnimation owner)
        {
            if(Finished)return;
            if(_timer==0)
            {
                var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(negative?(p.X==x?-x:x):(p.X==0?x:0),negative?(p.Y==y?-y:y):(p.Y==0?y:0));_timer=_delay;
                if(--_count==0){owner._offsets[slot.Value]=default;Finished=true;}
            }
            else _timer--;
        }
    }
}

public sealed partial class SourceBattleAnimationGate
{
    private readonly HashSet<(string,long)> _started=[];
    private SourceBattleAnimation? _current;
    private SourceMoveAnimationSequence? _sequence;
    public SourceBattleAnimation? Current=>_sequence?.Current??_current;
    public bool IsBlocking=>_sequence?.Active??_current?.Active==true;
    public bool TryStart(CommittedMoveAnimation cue,Func<SourceBattleAnimation> create)
    {
        if(IsBlocking||_started.Contains((cue.BattleId,cue.Sequence)))return false;
        var candidate=create();_started.Add((cue.BattleId,cue.Sequence));_sequence=null;_current=candidate;return true;
    }
    public void Reset(){if(_sequence is {} sequence)sequence.Cancel();else _current?.Cancel();_sequence=null;_current=null;_started.Clear();}
}

public static class SourceHitAnimationCatalog
{

    public static JsonDocument Read(string folder)
    {
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,"manifest.json"));
        return JsonDocument.Parse(bytes);
    }
    public static SourceAnimationScript Script(JsonDocument manifest,int move)
    {
        var row=manifest.RootElement.GetProperty("scripts").EnumerateArray().Single(x=>x.GetProperty("moveId").GetInt32()==move);
        var recipes=Recipes(manifest.RootElement.GetProperty("spriteRecipes"));
        return new(move,row.GetProperty("label").GetString()!,row.GetProperty("commands").EnumerateArray().Select(x=>new SourceAnimationCommand(x.GetProperty("op").GetString()!,x.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),x.GetProperty("line").GetInt32())).ToArray()){SpriteRecipes=recipes,SineTable=manifest.RootElement.GetProperty("sineTable").EnumerateArray().Select(x=>x.GetInt32()).ToArray()};
    }
    public static IReadOnlyDictionary<string,SourceSpriteRecipe> Recipes(JsonElement rows)=>rows.EnumerateArray().Select(x=>new SourceSpriteRecipe(x.GetProperty("template").GetString()!,x.GetProperty("asset").GetString()!,x.GetProperty("callback").GetString()!,x.GetProperty("blend").GetBoolean(),x.GetProperty("animations").EnumerateArray().Select(a=>a.EnumerateArray().Select(f=>new SourceSpriteAnimFrame(f.GetProperty("tile").GetInt32(),f.GetProperty("duration").GetInt32(),f.GetProperty("hFlip").GetBoolean(),f.GetProperty("vFlip").GetBoolean())).ToArray()).ToArray()){Width=x.TryGetProperty("width",out var w)?w.GetInt32():32,Height=x.TryGetProperty("height",out var h)?h.GetInt32():32,LoopStarts=x.TryGetProperty("loopStarts",out var loops)?loops.EnumerateArray().Select(v=>v.GetInt32()).ToArray():[],FrameLoops=x.TryGetProperty("frameLoops",out var frameLoops)?frameLoops.EnumerateArray().Select(a=>a.EnumerateArray().Select(c=>new SourceSpriteFrameLoop(c.GetProperty("at").GetInt32(),c.GetProperty("start").GetInt32(),c.GetProperty("count").GetInt32())).ToArray()).ToArray():[],AffineAnimations=x.TryGetProperty("affineAnimations",out var affine)?SourceSpriteAffineProgram.Read(affine):[],AffineDoubleSize=x.TryGetProperty("affineDoubleSize",out var doubleSize)&&doubleSize.GetBoolean(),Subsprites=x.TryGetProperty("subsprites",out var subsprites)?subsprites.EnumerateArray().Select(p=>new SourceSpriteSubsprite(p.GetProperty("x").GetInt32(),p.GetProperty("y").GetInt32(),p.GetProperty("width").GetInt32(),p.GetProperty("height").GetInt32(),p.GetProperty("tileOffset").GetInt32(),p.GetProperty("priority").GetInt32())).ToArray():[]}).ToDictionary(x=>x.Template);
    public static SourcePixelAsset Asset(JsonDocument manifest,string folder,string id)
    {
        var row=manifest.RootElement.GetProperty("assets").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==id);
        string path=row.GetProperty("path").GetString()!;
        if(Path.GetFileName(path)!=path)throw new InvalidDataException("Asset path differs.");
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,path));
        int width=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes),height=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
        if(width is not (8 or 16 or 32 or 64 or 128)||height is <8 or >512||height%8!=0||bytes.Length!=4+width*height*2)throw new InvalidDataException("Impact shape differs.");
        var pixels=new ushort[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4+i*2));
        return new(width,height,pixels);
    }
}
