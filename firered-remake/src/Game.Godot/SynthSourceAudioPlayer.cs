using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FireRedAudio;
using Godot;

/// <summary>Opt-in original sequence/sample synthesis. Scene thread owns controls and PCM production.</summary>
public partial class SynthSourceAudioPlayer : Node, ISourceAudioBackend
{
    public const int BlockFrames = 512;
    public const int TargetQueuedFrames = 5120;
    public const string BackendId = "original-csharp-source-filtered-v1";
    private readonly float[] _pcm = new float[BlockFrames * 2];
    private readonly Vector2[] _frames = new Vector2[BlockFrames];
    private readonly Dictionary<string, SynthCue> _catalog;
    private readonly Dictionary<int, ActiveCue> _effects = [];
    private readonly FireRedAudio.Resources _resources;
    private readonly NormalCryCatalog _cryCatalog;
    private Synth _synth;
    private AudioStreamPlayer? _output;
    private AudioStreamGenerator? _generator;
    private AudioStreamGeneratorPlayback? _playback;
    private ActiveCue? _music, _cry;
    private SynthCue? _fanfareCue;
    private long _nextToken, _generation, _fanfareToken, _fanfareMusicToken;
    private int _fanfareCounter, _cryGrace, _capacity, _retiredSkips;
    private double _sourceRemainder, _fadeRemaining, _fadeDuration;
    private float _musicGain = 1, _fadeStartGain, _fadeTargetGain;
    private bool _fadePauseAtEnd, _musicTemporarilyPaused;
    private bool _cryDucking, _renderCryDucking, _stereo = true, _faulted;
    private long _pushedFrames, _capturedCompletionFrames;
    public bool AutomaticSourceClock { get; set; } = true;
    public event Action<SourceAudioPlaybackEvent>? PlaybackEvent;
    public int RegisteredClipCount => _catalog.Count;
    public int UnderrunSkips => _retiredSkips + (_playback?.GetSkips() ?? 0);
    public int QueuedFrames => _playback is null ? 0 : _capacity - _playback.GetFramesAvailable();
    public double QueueBoundMilliseconds => 1000d * TargetQueuedFrames / Synth.Rate;
    public long ProducedFrames => _pushedFrames;
    public long ConsumedFrames => Math.Max(0, _pushedFrames - QueuedFrames);
    public bool Faulted => _faulted;
    public double Peak => _synth.Peak;
    public long Clipped => _synth.Clipped;

    public SynthSourceAudioPlayer(string projectRoot, bool includeRoute11 = false, bool includeRockTunnel = false, bool includeCeladonApproach = false, bool includeCeladonGym = false, bool includeRocketHideout = false, bool includePokemonTower = false, bool includeSnorlax = false, bool includeFuchsiaRoute = false, bool includeSafari = false, bool includeSurf = false, string? surfContentRoot = null, bool includeSilph = false, bool includeSaffronGym = false, bool includeCinnabar = false, bool includeFirstSevii = false, bool includeVictoryRoad = false, bool includePokemonLeague = false, bool includeSeviiRuby = false, bool includeFourIsland = false, bool includeSixIsland = false, bool includeFiveIsland = false, bool includeCelio = false, bool includeCeruleanCave = false, bool includeLeagueRematch = false, bool includeSevenIsland = false, bool includeEventIslands = false, bool includePowerPlant = false, bool includeTrainerTower = false, bool includeFossilLab = false, bool includeSeafoam = false, bool includeMoveDeleter = false, bool includeVending = false, bool includeFieldFly = false, bool includeSlotMachine = false, bool includeFameChecker = false, bool includeVsSeeker = false, bool includeTeachyTv = false)
    {
        if(includeSilph&&!includeSurf)throw new ArgumentException("Silph audio requires Surf.");
        if(includeSurf&&!includeSafari)throw new ArgumentException("Surf audio requires Safari.");
        if(includeSafari&&!includeFuchsiaRoute)throw new ArgumentException("Safari audio requires Fuchsia route.");
        if(includeFuchsiaRoute&&!includeSnorlax)throw new ArgumentException("Fuchsia route audio requires Snorlax.");
        if(includeSnorlax&&!includePokemonTower)throw new ArgumentException("Snorlax audio requires Pokemon Tower.");
        if(includePokemonTower&&!includeRocketHideout)throw new ArgumentException("Pokemon Tower audio requires Rocket Hideout.");
        if(includeRocketHideout&&!includeCeladonGym)throw new ArgumentException("Rocket Hideout audio requires Celadon Gym.");
        if(includeCeladonGym&&!includeCeladonApproach)throw new ArgumentException("Celadon Gym audio requires Celadon approach.");
        if(includeCeladonApproach&&!includeRockTunnel)throw new ArgumentException("Celadon approach audio requires RockTunnel.");
        if(includeRockTunnel&&!includeRoute11)throw new ArgumentException("RockTunnel audio requires Route11.");
        string directory = Path.Combine(Path.GetFullPath(projectRoot), "content/generated/synth-audio");
        string pack = Path.Combine(directory, "firered.pack");
        if (new FileInfo(pack).Length != FireRedAudio.Resources.End - FireRedAudio.Resources.Base)
            throw new InvalidDataException("Synth resource pack length differs.");
        _resources = FireRedAudio.Resources.FromPack(pack);
        _cryCatalog = NormalCryCatalog.FromFile(Path.Combine(directory, "normal-cries.bin"));
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (new FileInfo(manifestPath).Length != SynthManifestIdentity.Bytes)
            throw new InvalidDataException("Synth manifest length differs.");
        byte[] bytes = File.ReadAllBytes(manifestPath);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != SynthManifestIdentity.Sha256)
            throw new InvalidDataException("Synth manifest identity differs.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var manifest = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Missing synth manifest.");
        if (manifest.SchemaVersion != 1 || manifest.SourceCommit != SynthManifestIdentity.SourceCommit
            || manifest.Cues.Length != SynthManifestIdentity.CueCount)
            throw new InvalidDataException("Synth manifest contract differs.");
        var cues = manifest.Cues;
        if (includeRoute11)
        {
            string extraPath = Path.Combine(Path.GetFullPath(projectRoot), "content/generated/route11-synth-audio/manifest.json");
            if (new FileInfo(extraPath).Length != Route11SynthManifestIdentity.Bytes)
                throw new InvalidDataException("Route11 synth manifest length differs.");
            byte[] extraBytes = File.ReadAllBytes(extraPath);
            if (Convert.ToHexStringLower(SHA256.HashData(extraBytes)) != Route11SynthManifestIdentity.Sha256)
                throw new InvalidDataException("Route11 synth manifest identity differs.");
            var extra = JsonSerializer.Deserialize<SynthManifest>(extraBytes, options)
                ?? throw new InvalidDataException("Missing Route11 synth manifest.");
            if (extra.SchemaVersion != 1 || extra.SourceCommit != manifest.SourceCommit ||
                extra.SourceCommit != Route11SynthManifestIdentity.SourceCommit || extra.Cues.Length != Route11SynthManifestIdentity.CueCount)
                throw new InvalidDataException("Route11 synth manifest contract differs.");
            cues = [.. cues, .. extra.Cues];
        }
        if(includeRockTunnel)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/rocktunnel-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=RockTunnelSynthManifestIdentity.Bytes)throw new InvalidDataException("RockTunnel synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=RockTunnelSynthManifestIdentity.Sha256)throw new InvalidDataException("RockTunnel synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing RockTunnel synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=RockTunnelSynthManifestIdentity.SourceCommit||extension.Cues.Length!=RockTunnelSynthManifestIdentity.CueCount)throw new InvalidDataException("RockTunnel synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeCeladonApproach)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/celadon-approach-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=CeladonApproachSynthManifestIdentity.Bytes)throw new InvalidDataException("CeladonApproach synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=CeladonApproachSynthManifestIdentity.Sha256)throw new InvalidDataException("CeladonApproach synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing CeladonApproach synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=CeladonApproachSynthManifestIdentity.SourceCommit||extension.Cues.Length!=CeladonApproachSynthManifestIdentity.CueCount)throw new InvalidDataException("CeladonApproach synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeCeladonGym)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/celadon-gym-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=CeladonGymSynthManifestIdentity.Bytes)throw new InvalidDataException("CeladonGym synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=CeladonGymSynthManifestIdentity.Sha256)throw new InvalidDataException("CeladonGym synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing CeladonGym synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=CeladonGymSynthManifestIdentity.SourceCommit||extension.Cues.Length!=CeladonGymSynthManifestIdentity.CueCount)throw new InvalidDataException("CeladonGym synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeRocketHideout)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/rocket-hideout-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=RocketHideoutSynthManifestIdentity.Bytes)throw new InvalidDataException("RocketHideout synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=RocketHideoutSynthManifestIdentity.Sha256)throw new InvalidDataException("RocketHideout synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing RocketHideout synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=RocketHideoutSynthManifestIdentity.SourceCommit||extension.Cues.Length!=RocketHideoutSynthManifestIdentity.CueCount)throw new InvalidDataException("RocketHideout synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includePokemonTower)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/pokemon-tower-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=PokemonTowerSynthManifestIdentity.Bytes)throw new InvalidDataException("PokemonTower synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=PokemonTowerSynthManifestIdentity.Sha256)throw new InvalidDataException("PokemonTower synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing PokemonTower synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=PokemonTowerSynthManifestIdentity.SourceCommit||extension.Cues.Length!=PokemonTowerSynthManifestIdentity.CueCount)throw new InvalidDataException("PokemonTower synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSnorlax)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/snorlax-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=SnorlaxSynthManifestIdentity.Bytes)throw new InvalidDataException("Snorlax synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=SnorlaxSynthManifestIdentity.Sha256)throw new InvalidDataException("Snorlax synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing Snorlax synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=SnorlaxSynthManifestIdentity.SourceCommit||extension.Cues.Length!=SnorlaxSynthManifestIdentity.CueCount)throw new InvalidDataException("Snorlax synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeFuchsiaRoute)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/fuchsia-route-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=FuchsiaRouteSynthManifestIdentity.Bytes)throw new InvalidDataException("Fuchsia route cry manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=FuchsiaRouteSynthManifestIdentity.Sha256)throw new InvalidDataException("Fuchsia route cry manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing Fuchsia route cry manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=FuchsiaRouteSynthManifestIdentity.CueCount)throw new InvalidDataException("Fuchsia route cry manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSafari)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/safari-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=SafariSynthManifestIdentity.Bytes)throw new InvalidDataException("Safari cry manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=SafariSynthManifestIdentity.Sha256)throw new InvalidDataException("Safari cry manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing Safari cry manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=SafariSynthManifestIdentity.SourceCommit||extension.Cues.Length!=SafariSynthManifestIdentity.CueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"CRY_CHANSEY_NORMAL","CRY_SCYTHER_NORMAL","CRY_TAUROS_NORMAL"}))
                throw new InvalidDataException("Safari cry manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSurf)
        {
            string path=Path.Combine(Path.GetFullPath(surfContentRoot??projectRoot),"content/generated/surf-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=SurfSynthManifestIdentity.Bytes)throw new InvalidDataException("Surf cry manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=SurfSynthManifestIdentity.Sha256)throw new InvalidDataException("Surf cry manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing Surf cry manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=SurfSynthManifestIdentity.SourceCommit||extension.Cues.Length!=SurfSynthManifestIdentity.CueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"CRY_PSYDUCK_NORMAL","CRY_GOLDUCK_NORMAL"}))
                throw new InvalidDataException("Surf cry manifest contract differs.");
            // Tower already admitted Psyduck's scripted cry. Reuse only this exact source cue.
            var inheritedPsyduck=cues.SingleOrDefault(c=>c.Id=="CRY_PSYDUCK_NORMAL");
            if(inheritedPsyduck is null||inheritedPsyduck!=extension.Cues.Single(c=>c.Id=="CRY_PSYDUCK_NORMAL")||cues.Any(c=>c.Id=="CRY_GOLDUCK_NORMAL"))
                throw new InvalidDataException("Inherited Surf cry differs.");
            cues=[..cues,extension.Cues.Single(c=>c.Id=="CRY_GOLDUCK_NORMAL")];
        }
        if(includeSilph)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/silph-synth-audio/manifest.json");
            if(new FileInfo(path).Length!=SilphSynthManifestIdentity.Bytes)throw new InvalidDataException("Silph synth manifest length differs.");
            byte[] extensionBytes=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=SilphSynthManifestIdentity.Sha256)throw new InvalidDataException("Silph synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Missing Silph synth manifest.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=SilphSynthManifestIdentity.SourceCommit||extension.Cues.Length!=SilphSynthManifestIdentity.CueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_SILPH","SE_WARP_OUT","CRY_ALAKAZAM_NORMAL","CRY_MR_MIME_NORMAL","CRY_LAPRAS_NORMAL"}))
                throw new InvalidDataException("Silph synth manifest contract differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSaffronGym)
        {
            if(!includeSilph)throw new InvalidDataException("Saffron Gym audio requires Silph predecessor.");
            byte[] extensionBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),"content/generated/saffron-gym-synth-audio/manifest.json"));
            if(extensionBytes.Length!=SaffronGymSynthManifestIdentity.Bytes||Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=SaffronGymSynthManifestIdentity.Sha256)
                throw new InvalidDataException("Saffron Gym cry manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Saffron Gym cry manifest missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=SaffronGymSynthManifestIdentity.SourceCommit||
                extension.Cues.Length!=1||extension.Cues[0].Id!="CRY_SLOWBRO_NORMAL"||extension.Cues[0].Species!=80||cues.Any(c=>c.Id=="CRY_SLOWBRO_NORMAL"))
                throw new InvalidDataException("Saffron Gym source cry scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeCinnabar)
        {
            if(!includeSaffronGym)throw new InvalidDataException("Cinnabar audio requires Saffron predecessor.");
            var extensionBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),"content/generated/cinnabar-synth-audio/manifest.json"));
            if(extensionBytes.Length!=CinnabarSynthManifestIdentity.Bytes||Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=CinnabarSynthManifestIdentity.Sha256)
                throw new InvalidDataException("Cinnabar synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("Cinnabar synth manifest missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.SourceCommit!=CinnabarSynthManifestIdentity.SourceCommit||
                extension.Cues.Length!=CinnabarSynthManifestIdentity.CueCount||!extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_CINNABAR","MUS_POKE_MANSION","SE_FALL","CRY_NINETALES_NORMAL","CRY_ARCANINE_NORMAL","CRY_RAPIDASH_NORMAL","CRY_CLOYSTER_NORMAL","CRY_KRABBY_NORMAL","CRY_KINGLER_NORMAL"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Cinnabar source cue scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeFirstSevii)
        {
            if(!includeCinnabar)throw new InvalidDataException("First-Sevii audio requires Cinnabar predecessor.");
            byte[] extensionBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),"content/generated/first-sevii-presentation/synth-manifest.json"));
            if(extensionBytes.Length!=Game.Content.FirstSeviiPresentationIdentity.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(extensionBytes))!=Game.Content.FirstSeviiPresentationIdentity.SynthSha256)
                throw new InvalidDataException("First-Sevii synth manifest identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(extensionBytes,options)??throw new InvalidDataException("First-Sevii synth manifest missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=Game.Content.FirstSeviiPresentationIdentity.SynthCueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_SEVII_123","MUS_NET_CENTER","SE_SHIP"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("First-Sevii source cue scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeVictoryRoad)
        {
            if(!includeFirstSevii)throw new InvalidDataException("Victory Road audio requires its predecessor.");
            byte[] addition=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),VictoryRoadPresentationAssets.Base+"synth-manifest.json"));
            if(addition.Length!=VictoryRoadPresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(addition))!=VictoryRoadPresentationAssets.SynthSha256)
                throw new InvalidDataException("Victory Road synth identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(addition,options)??throw new InvalidDataException("Victory Road synth metadata missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=1||
                extension.Cues[0].Id!="SE_ICE_BREAK"||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Victory Road source cue scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includePokemonLeague)
        {
            if(!includeVictoryRoad)throw new InvalidDataException("Pokemon League audio requires Victory Road.");
            byte[] leagueBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),PokemonLeaguePresentationAssets.Base+"synth-manifest.json"));
            if(leagueBytes.Length!=PokemonLeaguePresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(leagueBytes))!=PokemonLeaguePresentationAssets.SynthSha256)throw new InvalidDataException("Pokemon League synth identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(leagueBytes,options)??throw new InvalidDataException("Pokemon League synth missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=17||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))throw new InvalidDataException("Pokemon League cue scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSeviiRuby)
        {
            if(!includePokemonLeague)throw new InvalidDataException("Sevii Ruby audio requires Pokemon League.");
            byte[] rubyBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),SeviiRubyPresentationAssets.Base+"synth-manifest.json"));
            if(rubyBytes.Length!=SeviiRubyPresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(rubyBytes))!=SeviiRubyPresentationAssets.SynthSha256)
                throw new InvalidDataException("Sevii Ruby synth identity differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(rubyBytes,options)??throw new InvalidDataException("Sevii Ruby synth missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=3||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_SEVII_CAVE","CRY_SLUGMA_NORMAL","CRY_MAGCARGO_NORMAL"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Sevii Ruby source cue scope differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeFourIsland)
        {
            if(!includeSeviiRuby)throw new InvalidDataException("Four Island audio requires Ruby.");
            byte[] fourIslandBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),FourIslandPresentationAssets.Base+"synth-manifest.json"));
            if(fourIslandBytes.Length!=FourIslandPresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(fourIslandBytes))!=FourIslandPresentationAssets.SynthSha256)
                throw new InvalidDataException("Four Island synth identity differs.");
            var extension=JsonSerializer.Deserialize<FourIslandSynthManifest>(fourIslandBytes,options)??throw new InvalidDataException("Four Island synth missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=7||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"CRY_LAPRAS_ENCOUNTER","MUS_SEVII_45","CRY_WOOPER_NORMAL","CRY_QUAGSIRE_NORMAL","CRY_SWINUB_NORMAL","CRY_PILOSWINE_NORMAL","CRY_DELIBIRD_NORMAL"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Four Island source cue scope differs.");
            if(extension.InheritedCues is not {Length:1})throw new InvalidDataException("Four Island inherited cue scope differs.");
            var inheritedSeel=extension.InheritedCues[0];
            if(inheritedSeel is null||inheritedSeel.Id!="CRY_SEEL_NORMAL"||cues.SingleOrDefault(c=>c.Id=="CRY_SEEL_NORMAL")!=inheritedSeel)
                throw new InvalidDataException("Four Island inherited Seel cue differs.");
            cues=[..cues,..extension.Cues];
        }
        if(includeSixIsland)
        {
            if(!includeFourIsland)throw new InvalidDataException("Six Island audio requires Four Island.");
            byte[] sixIslandBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),SixIslandPresentationAssets.Base+"synth-manifest.json"));
            if(sixIslandBytes.Length!=SixIslandPresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(sixIslandBytes))!=SixIslandPresentationAssets.SynthSha256)
                throw new InvalidDataException("Six Island synth identity differs.");
            var extension=JsonSerializer.Deserialize<SixIslandSynthManifest>(sixIslandBytes,options)??throw new InvalidDataException("Six Island synth missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=SixIslandPresentationAssets.SynthCueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_SEVII_67","MUS_SEVII_DUNGEON","MUS_SEVII_ROUTE","SE_BANG","CRY_SENTRET_NORMAL","CRY_FURRET_NORMAL","CRY_NATU_NORMAL","CRY_XATU_NORMAL","CRY_YANMA_NORMAL","CRY_WOBBUFFET_NORMAL","CRY_BELLOSSOM_NORMAL","CRY_SUNKERN_NORMAL","CRY_SUNFLORA_NORMAL","CRY_CHINCHOU_NORMAL","CRY_LANTURN_NORMAL","CRY_SUDOWOODO_NORMAL"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Six Island source cue scope differs.");
            if(extension.InheritedCues is not {Length:2}||!extension.InheritedCues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"SE_FALL","SE_M_STRENGTH"})||
                extension.InheritedCues.Any(c=>cues.SingleOrDefault(old=>old.Id==c.Id)!=c))
                throw new InvalidDataException("Six Island inherited cues differ.");
            cues=[..cues,..extension.Cues];
        }
        if(includeFiveIsland)
        {
            if(!includeSixIsland)throw new InvalidDataException("Five Island audio requires Six Island.");
            byte[] fiveIslandBytes=File.ReadAllBytes(Path.Combine(Path.GetFullPath(projectRoot),FiveIslandPresentationAssets.Base+"synth-manifest.json"));
            if(fiveIslandBytes.Length!=FiveIslandPresentationAssets.SynthBytes||Convert.ToHexStringLower(SHA256.HashData(fiveIslandBytes))!=FiveIslandPresentationAssets.SynthSha256)
                throw new InvalidDataException("Five Island synth identity differs.");
            var extension=JsonSerializer.Deserialize<FiveIslandSynthManifest>(fiveIslandBytes,options)??throw new InvalidDataException("Five Island synth missing.");
            if(extension.SchemaVersion!=1||extension.SourceCommit!=manifest.SourceCommit||extension.Cues.Length!=FiveIslandPresentationAssets.SynthCueCount||
                !extension.Cues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"CRY_HOPPIP_NORMAL","CRY_SKIPLOOM_NORMAL","CRY_JUMPLUFF_NORMAL","CRY_HOUNDOUR_NORMAL","CRY_HOUNDOOM_NORMAL","CRY_PORYGON_NORMAL"})||extension.Cues.Any(c=>cues.Any(old=>old.Id==c.Id)))
                throw new InvalidDataException("Five Island source cue scope differs.");
            if(extension.InheritedCues is not {Length:5}||!extension.InheritedCues.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"MUS_SEVII_45","MUS_SEVII_ROUTE","MUS_ROCKET_HIDEOUT","MUS_POKE_CENTER","SE_PIN"})||
                extension.InheritedCues.Any(c=>cues.SingleOrDefault(old=>old.Id==c.Id)!=c))
                throw new InvalidDataException("Five Island inherited cues differ.");
            cues=[..cues,..extension.Cues];
        }
        if (includeCelio) cues = ExtendCelioCues(projectRoot,includeFiveIsland,cues,options,manifest.SourceCommit);
        if (includeCeruleanCave) cues = ExtendMewtwoCues(projectRoot,includeCelio,cues,options,manifest.SourceCommit);
        if (includeLeagueRematch) cues = ExtendLeagueRematchCues(projectRoot,includeCeruleanCave,cues,options,manifest.SourceCommit);
        if (includeSevenIsland) cues = ExtendSevenIslandCues(projectRoot,includeLeagueRematch,cues,options,manifest.SourceCommit);
        if (includeEventIslands) cues = ExtendEventIslandCues(projectRoot,includeSevenIsland,cues,options,manifest.SourceCommit);
        if (includePowerPlant) cues = ExtendPowerPlantCues(projectRoot,includeEventIslands,cues,options,manifest.SourceCommit);
        if (includeTrainerTower) cues = ExtendTrainerTowerCues(projectRoot,includePowerPlant,cues,options,manifest.SourceCommit);
        if (includeFossilLab) cues = ExtendFossilLabCues(projectRoot,includeTrainerTower,cues,options,manifest.SourceCommit);
        if (includeSeafoam) cues = ExtendSeafoamCues(projectRoot,includeFossilLab,cues,options,manifest.SourceCommit);
        if (includeMoveDeleter) cues = ExtendMoveDeleterCues(projectRoot,includeSeafoam,cues,options,manifest.SourceCommit);
        if (includeVending) cues = ExtendVendingCues(projectRoot,cues,options,manifest.SourceCommit);
        if (includeFieldFly) cues = ExtendFieldFlyCues(projectRoot,cues,options,manifest.SourceCommit);
        if (includeSlotMachine) cues = ExtendSlotMachineCues(projectRoot,cues,options,manifest.SourceCommit);
        if (includeFameChecker) cues = ExtendFameCheckerCues(cues,manifest.SourceCommit);
        if (includeVsSeeker) cues = ExtendVsSeekerCues(cues,manifest.SourceCommit);
        if (includeTeachyTv) cues = ExtendTeachyTvCues(projectRoot,cues,options,manifest.SourceCommit);
        // Dedicated animation cues reuse the locked pack without modifying regional catalogs.
        if(includeFuchsiaRoute)
        {
            string path=Path.Combine(Path.GetFullPath(projectRoot),"content/generated/source-explosion-animations/synth-manifest.json");
            if(new FileInfo(path).Length!=831)throw new InvalidDataException("Explosion synth manifest length differs.");
            var addition=File.ReadAllBytes(path);
            if(Convert.ToHexStringLower(SHA256.HashData(addition))!="c8b16dbdac6ad1ad6cf101ffe3625886a8331d3a03d07d9e617aff50d834426a")throw new InvalidDataException("Explosion synth manifest differs.");
            var extension=JsonSerializer.Deserialize<SynthManifest>(addition,options)??throw new InvalidDataException("Missing explosion sound metadata.");
            if(extension.SourceCommit!=manifest.SourceCommit||extension.SchemaVersion!=1||extension.Cues.Length!=2)throw new InvalidDataException("Explosion sound metadata contract differs.");
            cues=[..cues,..extension.Cues];
        }
        _catalog = new(StringComparer.Ordinal);
        foreach (var cue in cues)
        {
            if (string.IsNullOrWhiteSpace(cue.Id) || cue.Id.Length > 100 || cue.SourcePlayer is < 0 or > 4
                || cue.Priority is < 0 or > 255 || cue.SourceCounter is < 0 or > 36000
                || cue.SourceCommit != manifest.SourceCommit || !_catalog.TryAdd(cue.Id, cue))
                throw new InvalidDataException("Invalid or duplicated synth cue.");
            if (cue.SourcePlayer == 4)
            {
                _cryCatalog.Get(cue.Species);
                if (cue.SongAddress != 0 || cue.Looping || cue.SourceCounter is not null ||
                    cue.CryMode != 0 && !(cue.CryMode == 2 && (includeSnorlax && cue.Species == 143 && cue.Id == "CRY_SNORLAX_ENCOUNTER" || includeFourIsland && cue.Species == 131 && cue.Id == "CRY_LAPRAS_ENCOUNTER" || includeCeruleanCave && cue.Species == 150 && cue.Id == "CRY_MEWTWO_ENCOUNTER" || includeEventIslands && ((cue.Species,cue.Id) is (249,"CRY_LUGIA_ENCOUNTER") or (250,"CRY_HO_OH_ENCOUNTER") or (410,"CRY_DEOXYS_ENCOUNTER")) || includePowerPlant && ((cue.Species,cue.Id) is (101,"CRY_ELECTRODE_ENCOUNTER") or (145,"CRY_ZAPDOS_ENCOUNTER")) || includeSeafoam && cue.Species == 144 && cue.Id == "CRY_ARTICUNO_ENCOUNTER")))
                    throw new InvalidDataException("Invalid normal cry cue.");
            }
            else
            {
                var song = Song.Read(_resources, cue.SongAddress);
                if (song.Priority != cue.Priority || cue.Species != 0 || cue.CryMode != 0)
                    throw new InvalidDataException("Synth song metadata differs from authenticated resource.");
            }
        }
        _synth = NewSynth();
    }

    private Synth NewSynth()
    {
        var synth = new Synth(_resources, AudioProfile.SourceFiltered, _cryCatalog) { EnableTrace = false };
        foreach (var cue in _catalog.Values)
            if (cue.SourcePlayer == 4) synth.PrepareNormalCry(cue.Species);
        return synth;
    }

    public SourceAudioMetadata Metadata(string id) => _catalog.TryGetValue(id, out var cue)
        ? cue.Metadata : throw new InvalidDataException("Unknown synth cue: " + id);
    public string? CrySymbol(int species) => _catalog.Values.FirstOrDefault(c => c.Species == species)?.SourceSymbol;
    public bool HasClip(string id) => !_faulted && _catalog.ContainsKey(id);
    public void Register(VerifiedSourceAudioClip clip) => throw new NotSupportedException("Synth admits only its frozen sequence catalog; no recorded fallback.");

    public long PlayMusic(string id, bool restart = false)
    {
        if (!TryCue(id, 0, out var cue)) return 0;
        if (!restart && _music?.Cue.Id == id && _music.EndFrame is null) return _music.Token;
        CancelFanfare(resumeMusic: false);
        CancelCry();
        _synth.Stop(0);
        _seagallopMusicFade=null;
        _musicGain = 1; _fadeRemaining = 0; _fadePauseAtEnd = _musicTemporarilyPaused = false;
        _music = Start(cue!, "music");
        ApplyMusicGain();
        return _music.Token;
    }

    public long PlaySoundEffect(string id)
    {
        if (!TryCue(id, null, out var cue)) return 0;
        if (cue!.SourcePlayer is < 1 or > 3) throw new InvalidOperationException("Not an SE player: " + id);
        return StartEffect(cue, "effect");
    }

    public long PlayFanfare(string id)
    {
        if (!TryCue(id, 2, out var cue)) return 0;
        if (cue!.SourceCounter is null || cue.Looping)
            throw new InvalidOperationException("Fanfare requires a finite sequence and independent source counter.");
        long token = StartEffect(cue, "fanfare");
        if (token == 0) token = ++_nextToken;
        _fanfareToken = token; _fanfareCounter = cue.SourceCounter.Value;
        _fanfareCue = cue; _fanfareMusicToken = _music?.Token ?? 0;
        if (_music is not null) _synth.PausePlayer(0);
        return token;
    }

    public long PlayCry(string id)
    {
        if (!TryCue(id, 4, out var cue)) return 0;
        if (cue!.CryMode == 2) _synth.StartEncounterCry(cue.Species, duckBgm: false);
        else _synth.StartNormalCry(cue.Species, duckBgm: false);
        _cry = new ActiveCue(cue, ++_nextToken, _pushedFrames, _synth.GameFrames);
        _cryDucking = _renderCryDucking = true; _cryGrace = 2; ApplyMusicGain();
        Emit("started", _cry, "cry");
        return _cry.Token;
    }

    public void CancelFanfare(long? expectedToken = null, bool resumeMusic = true)
    {
        if (_fanfareToken == 0 || expectedToken.HasValue && expectedToken != _fanfareToken) return;
        if (_effects.TryGetValue(2, out var cue) && cue.Token == _fanfareToken)
        { _synth.Stop(3); _effects.Remove(2); }
        FinishFanfare(resumeMusic, "cancelled");
    }

    public void CancelCry(long? expectedToken = null)
    {
        if (expectedToken.HasValue && _cry?.Token != expectedToken) return;
        _synth.Stop(2); _cry = null; _cryDucking = _renderCryDucking = false; _cryGrace = 0; ApplyMusicGain();
    }

    public void StopSoundEffects()
    {
        CancelFanfare();
        foreach (int sourcePlayer in _effects.Keys) _synth.Stop(CoreSlot(sourcePlayer));
        _effects.Clear();
    }

    public void FadeOutMusic(double seconds, bool temporarily = false)
    {
        _seagallopMusicFade=null;
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _fadeTargetGain = 0; _fadePauseAtEnd = temporarily;
        _fadeRemaining = _fadeDuration = seconds; _fadeStartGain = _musicGain;
        if (seconds == 0) FinishMusicFade();
    }

    /// <summary>Source temporary fade resumes the retained sequence; it does not start its song again.</summary>
    public void FadeInMusic(double seconds)
    {
        _seagallopMusicFade=null;
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_music is null) return;
        _musicTemporarilyPaused = _fadePauseAtEnd = false;
        _musicGain = _fadeStartGain = 0; _fadeTargetGain = 1;
        _fadeRemaining = _fadeDuration = seconds;
        if (_fanfareToken == 0) _synth.ResumePlayer(0);
        ApplyMusicGain();
        if (seconds == 0) FinishMusicFade();
    }

    private void FinishMusicFade()
    {
        _musicGain = _fadeTargetGain; _fadeRemaining = 0; ApplyMusicGain();
        if (_music is null || _fadeTargetGain != 0) return;
        if (_fadePauseAtEnd) { _musicTemporarilyPaused = true; _synth.PausePlayer(0); }
        else { _synth.Stop(0); _music = null; _musicTemporarilyPaused = false; }
    }

    public void ResetPlayback()
    {
        _seagallopMusicFade=null;
        _generation++;
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0; _fanfareCue = null;
        _music = _cry = null; _effects.Clear(); _cryDucking = _renderCryDucking = false; _cryGrace = 0;
        _sourceRemainder = _fadeRemaining = 0; _musicGain = 1;
        _fadePauseAtEnd = _musicTemporarilyPaused = false;
        ReleaseOutput();
        // A new mixer clears feedback tails as well as voices on session replacement.
        _synth = NewSynth();
        _pushedFrames = _capturedCompletionFrames = 0;
    }

    public void SetStereo(bool stereo) => _stereo = stereo;

    public void AdvanceSourceFrame()
    {
        AdvanceSeagallopMusicFade();
        if (_fanfareToken != 0)
        {
            if (_fanfareCounter > 0) _fanfareCounter--;
            else FinishFanfare(true, "source-counter-complete");
        }
        if (_cryDucking)
        {
            if (_cryGrace > 0) _cryGrace--;
            else if (_cry is null) { _cryDucking = false; ApplyMusicGain(); }
        }
    }

    public SourceAudioPlaybackState Snapshot() => new(State(_music),
        _effects.Values.OrderBy(c => c.Cue.SourcePlayer).Select(c => State(c)!).ToArray(), State(_cry),
        _fanfareToken == 0 ? null : _fanfareToken, _fanfareToken == 0 ? null : _fanfareCounter,
        _cryDucking, _stereo, _generation);

    public override void _Process(double delta)
    {
        if (_faulted) return;
        try
        {
            UpdateCompletions();
            if (AutomaticSourceClock)
            {
                _sourceRemainder += delta * SourceAudioPlayer.SourceFramesPerSecond;
                while (_sourceRemainder >= 1) { _sourceRemainder--; AdvanceSourceFrame(); }
            }
            if (_fadeRemaining > 0 && _music is not null)
            {
                _fadeRemaining = Math.Max(0, _fadeRemaining - delta);
                _musicGain = _fadeTargetGain + (_fadeStartGain - _fadeTargetGain) * (float)(_fadeRemaining / _fadeDuration); ApplyMusicGain();
                if (_fadeRemaining == 0) FinishMusicFade();
            }
            if (_music is not null || _cry is not null || _effects.Count != 0 || _playback is not null) Pump();
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or NotSupportedException or ArithmeticException)
        {
            _faulted = true; ResetPlayback();
            PlaybackEvent?.Invoke(new("fault", "", 0, -1, error.Message, false));
        }
    }

    private void Pump()
    {
        EnsureOutput();
        while (_playback!.GetFramesAvailable() >= BlockFrames && QueuedFrames + BlockFrames <= TargetQueuedFrames)
        {
            _synth.Render(_pcm);
            for (int n = 0; n < BlockFrames; n++)
            {
                float left = _pcm[n * 2], right = _pcm[n * 2 + 1];
                if (!float.IsFinite(left) || !float.IsFinite(right)) throw new InvalidDataException("Nonfinite synthesized PCM.");
                if (!_stereo) left = right = (left + right) * .5f;
                _frames[n] = new Vector2(left, right);
            }
            if (!_playback.PushBuffer(_frames)) throw new InvalidOperationException("PCM ring rejected a checked block.");
            _pushedFrames += BlockFrames;
            MarkEnded(_music); MarkEnded(_cry);
            foreach (var cue in _effects.Values) MarkEnded(cue);
        }
        _output!.StreamPaused = false;
    }

    private void EnsureOutput()
    {
        if (_playback is not null) return;
        _generator = new AudioStreamGenerator { MixRate = Synth.Rate, BufferLength = .1f,
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom };
        _output = new AudioStreamPlayer { Stream = _generator, VolumeDb = 0 };
        AddChild(_output); _output.Play(); _output.StreamPaused = true;
        _playback = (AudioStreamGeneratorPlayback)_output.GetStreamPlayback();
        _capacity = _playback.GetFramesAvailable();
        if (_capacity < TargetQueuedFrames) throw new InvalidOperationException("PCM ring capacity is below configured queue target.");
    }

    private void MarkEnded(ActiveCue? cue)
    {
        if (cue is not null && cue.EndFrame is null && !_synth.PlayerPaused(CoreSlot(cue.Cue.SourcePlayer)) && !(cue.Cue.SourcePlayer == 4 ? _synth.GameFrames <= cue.StartGameFrame || _synth.CryPlaying : _synth.PlayerPlaying(CoreSlot(cue.Cue.SourcePlayer))))
        {
            cue.EndFrame = _pushedFrames;
            if (cue.Cue.SourcePlayer == 4)
            {
                // Restore future PCM at the synthesized cry boundary; the public wait/state
                // remains active until that boundary is consumed by the output ring.
                _renderCryDucking = false;
                ApplyMusicGain();
            }
        }
    }

    private void UpdateCompletions()
    {
        _capturedCompletionFrames = ConsumedFrames;
        if (Completed(_music)) { Emit("finished", _music!, "music"); _music = null; }
        if (Completed(_cry)) { Emit("finished", _cry!, "cry"); _cry = null; }
        foreach (int key in _effects.Where(p => Completed(p.Value)).Select(p => p.Key).ToArray())
        { Emit("finished", _effects[key], "effect"); _effects.Remove(key); }
    }
    private bool Completed(ActiveCue? cue) => cue?.EndFrame is long end && _capturedCompletionFrames >= end;

    private bool TryCue(string id, int? expectedPlayer, out SynthCue? cue)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Attach synth backend before playback.");
        if (_faulted || !_catalog.TryGetValue(id, out cue))
        { cue = null; PlaybackEvent?.Invoke(new("missing", id, 0, expectedPlayer ?? -1, "No admitted synthesized cue", false)); return false; }
        if (expectedPlayer.HasValue && cue.SourcePlayer != expectedPlayer)
            throw new InvalidOperationException("Wrong source player for " + id);
        return true;
    }

    private long StartEffect(SynthCue cue, string kind)
    {
        int player = cue.SourcePlayer;
        if (player is 1 or 2 && _effects.TryGetValue(player, out var previous)
            && !Completed(previous) && previous.Cue.Priority > cue.Priority)
        {
            PlaybackEvent?.Invoke(new("rejected", cue.Id, 0, player, "active-source-priority:" + previous.Cue.Priority, false, cue.Metadata));
            return 0;
        }
        var active = Start(cue, kind); _effects[player] = active; return active.Token;
    }

    private ActiveCue Start(SynthCue cue, string kind)
    {
        _synth.Start(cue.SongAddress, CoreSlot(cue.SourcePlayer));
        var active = new ActiveCue(cue, ++_nextToken, _pushedFrames, _synth.GameFrames); Emit("started", active, kind); return active;
    }

    private void FinishFanfare(bool resume, string reason)
    {
        long token = _fanfareToken; var cue = _fanfareCue;
        if (resume && !_musicTemporarilyPaused && _music?.Token == _fanfareMusicToken) _synth.ResumePlayer(0);
        _fanfareToken = _fanfareMusicToken = 0; _fanfareCounter = 0; _fanfareCue = null;
        PlaybackEvent?.Invoke(new("fanfare-task-finished", cue?.Id ?? "", token, 2, reason, false, cue?.Metadata));
    }

    private void ApplyMusicGain() => _synth.SetPlayerGain(0, _musicGain * (_renderCryDucking ? SourceAudioPlayer.CryDuckGain : 1f));
    private SourceAudioVoiceState? State(ActiveCue? cue) => cue is null ? null : new(cue.Cue.Id, cue.Token,
        cue.Cue.SourcePlayer, cue.Cue.Priority, !Completed(cue), _synth.PlayerPaused(CoreSlot(cue.Cue.SourcePlayer)),
        (double)_synth.PlayerElapsedFrames(CoreSlot(cue.Cue.SourcePlayer)) / Synth.Rate,
        cue.Cue.SourcePlayer == 0 ? _musicGain * (_cryDucking ? SourceAudioPlayer.CryDuckGain : 1f) : 1);
    private void Emit(string kind, ActiveCue cue, string reason) => PlaybackEvent?.Invoke(new(kind,
        cue.Cue.Id, cue.Token, cue.Cue.SourcePlayer, reason, false, cue.Cue.Metadata));
    private static int CoreSlot(int player) => player switch { 0 => 0, 1 => 1, 2 => 3, 3 => 4, 4 => 2, _ => throw new ArgumentOutOfRangeException(nameof(player)) };

    private void ReleaseOutput()
    {
        if (_playback is not null) { _retiredSkips += _playback.GetSkips(); }
        if (_output is not null) { _output.Stop(); _output.Stream = null; _output.QueueFree(); _output = null; }
        _playback?.Dispose(); _playback = null;
        _generator?.Dispose(); _generator = null; _capacity = 0;
    }
    public override void _ExitTree() => ResetPlayback();

    private sealed class ActiveCue(SynthCue cue, long token, long startFrame, long startGameFrame)
    {
        public SynthCue Cue { get; } = cue;
        public long Token { get; } = token;
        public long StartFrame { get; } = startFrame;
        public long StartGameFrame { get; } = startGameFrame;
        public long? EndFrame { get; set; }
    }
    private sealed record SynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues);
    private sealed record FiveIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);
    private sealed record SixIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);
    private sealed record FourIslandSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);
    private sealed record SynthCue(string Id, string SourceCommit, string SourceSymbol, string SourcePath,
        int SourcePlayer, int Priority, int? SourceCounter, uint SongAddress, int Species, bool Looping, int CryMode = 0)
    {
        public SourceAudioMetadata Metadata => new(Id, SourceCommit, SourceSymbol, SourcePath, SourcePlayer, Priority, SourceCounter, BackendId);
    }
}
