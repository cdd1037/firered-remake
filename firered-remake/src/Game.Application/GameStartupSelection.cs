namespace Game.Application;

public enum GameStartupMode { Current, Probe, Harness }

/// <summary>Pure composition input parsing. Hosts snapshot arguments/environment once and pass this result onward.</summary>
public sealed record GameStartupSelection(GameStartupMode Mode,string ProjectRoot,string Checkpoint)
{
    private static readonly string[] RetiredEnvironmentSelectors =
    [
        "FIRERED_REFERENCE_ROOT",
        "FIRERED_REFERENCE_SOURCE",
        "FIRERED_BILL",
        "FIRERED_CAPTURE",
        "FIRERED_CELADON_APPROACH",
        "FIRERED_CELADON_GYM",
        "FIRERED_CELIO",
        "FIRERED_CERULEAN",
        "FIRERED_CINNABAR",
        "FIRERED_CITY_SERVICES",
        "FIRERED_EARLY_PARTY",
        "FIRERED_EARLY_STARTER",
        "FIRERED_FIELD_PICKUPS",
        "FIRERED_FIRST_SEVII",
        "FIRERED_FIVE_ISLAND",
        "FIRERED_FOREST",
        "FIRERED_FOREST_TRAINERS",
        "FIRERED_FOUR_ISLAND",
        "FIRERED_FUCHSIA_GYM",
        "FIRERED_FUCHSIA_ROUTE",
        "FIRERED_LEAGUE_RIVAL",
        "FIRERED_MAGIKARP",
        "FIRERED_MTMOON",
        "FIRERED_NATIONAL_DEX",
        "FIRERED_OAK_RATING",
        "FIRERED_OLD_MAN",
        "FIRERED_PARTY_TRAINING",
        "FIRERED_PEWTER",
        "FIRERED_POKEMON_LEAGUE",
        "FIRERED_POKEMON_STORAGE",
        "FIRERED_POKEMON_TOWER",
        "FIRERED_PRESET_DOUBLES",
        "FIRERED_REGIONAL",
        "FIRERED_REGIONAL_INTERIORS",
        "FIRERED_REGIONAL_SERVICES",
        "FIRERED_REPEL",
        "FIRERED_ROCKET_HIDEOUT",
        "FIRERED_ROCKTUNNEL",
        "FIRERED_ROUTE11",
        "FIRERED_ROUTE22",
        "FIRERED_ROUTE3",
        "FIRERED_SAFARI",
        "FIRERED_SAFFRON_GYM",
        "FIRERED_SEVII_RUBY",
        "FIRERED_SILPH",
        "FIRERED_SIX_ISLAND",
        "FIRERED_SNORLAX",
        "FIRERED_SS_ANNE",
        "FIRERED_SURF",
        "FIRERED_SURF_CONTENT_ROOT",
        "FIRERED_TOWN_MAP",
        "FIRERED_VERMILION",
        "FIRERED_VERMILION_GYM",
        "FIRERED_VICTORY_ROAD",
        "FIRERED_VIRIDIAN_CENTER",
        "FIRERED_VIRIDIAN_GYM",
        "FIRERED_VIRIDIAN_REGION",
    ];

    public static GameStartupSelection Resolve(IReadOnlyList<string> args,IReadOnlyDictionary<string,string?> environment,string? defaultResourceRoot=null)
    {
        ArgumentNullException.ThrowIfNull(args);ArgumentNullException.ThrowIfNull(environment);
        string Env(string key)=>environment.GetValueOrDefault(key)??"";
        string? profile=null;
        var modes=new HashSet<GameStartupMode>();
        void SelectProfile(string value)
        {
            if(string.IsNullOrWhiteSpace(value))throw new InvalidOperationException("startup_profile_missing");
            if(profile is not null && profile!=value)throw new InvalidOperationException("startup_profile_conflict");
            profile=value;
        }
        for(int i=0;i<args.Count;i++)
        {
            string arg=args[i];
            if(arg=="--profile")
            {
                if(++i==args.Count)throw new InvalidOperationException("startup_profile_missing");
                SelectProfile(args[i]);
            }
            else if(arg.StartsWith("--profile=",StringComparison.Ordinal))SelectProfile(arg[10..]);
            else if(arg is "--story" or "--story-current")SelectProfile(CurrentGameProfile.Id);
            else if(arg.StartsWith("--story-",StringComparison.Ordinal))throw new InvalidOperationException("startup_profile_retired: "+arg);
            else if(arg=="--probe")modes.Add(GameStartupMode.Probe);
            else if(arg=="--harness")modes.Add(GameStartupMode.Harness);
            else if(arg=="--pallet")throw new InvalidOperationException("startup_mode_retired: --pallet");
        }
        string fromEnvironment=Env("FIRERED_PROFILE");
        if(fromEnvironment.Length>0)
        {
            if(profile is not null && profile!=fromEnvironment)throw new InvalidOperationException("startup_profile_conflict");
            SelectProfile(fromEnvironment);
        }
        if(profile is not null && profile!=CurrentGameProfile.Id)throw new InvalidOperationException("startup_profile_unsupported: "+profile);
        if(profile is not null)modes.Add(GameStartupMode.Current);
        if(modes.Count>1)throw new InvalidOperationException("startup_mode_conflict");
        var mode=modes.Count==0?GameStartupMode.Current:modes.Single();
        foreach(string name in RetiredEnvironmentSelectors)
            if(Env(name).Length>0)throw new InvalidOperationException("startup_selector_retired: "+name);
        if(Env("FIRERED_COMPACT_MAPS") is not ("" or "1"))throw new InvalidOperationException("startup_compact_maps_required");
        string root=Env("FIRERED_PROJECT_ROOT"),checkpoint=Env("FIRERED_CAMPAIGN_CHECKPOINT");
        // The host supplies its executable directory (or the editor project root).
        // Never search the working directory or fall back to a development tree.
        if(string.IsNullOrWhiteSpace(root))root=defaultResourceRoot??AppContext.BaseDirectory;
        root=Path.GetFullPath(root);
        if(mode!=GameStartupMode.Current && checkpoint.Length>0)throw new InvalidOperationException("startup_checkpoint_requires_current");
        return new(mode,root,checkpoint);
    }
}
