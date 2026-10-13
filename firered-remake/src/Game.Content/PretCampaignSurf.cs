using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string SurfContentProfile="firered-surf-campaign-v1";
    public PretSurfPokemon? SurfPokemon { get; }
    public PretSurfArt? SurfArt { get; }
    public PretSurfEncounters? SurfEncounters { get; }
    public SurfOwnedPokemonCatalog? SurfOwnership { get; }
    public bool SurfEnabled=>SurfOwnership is not null;
    public IReadOnlyDictionary<MapId,WaterEncounterTable>? WaterEncounters=>SeviiRubyWaterEncounters?.Tables??VictoryRoadWaterEncounters?.Tables??FirstSeviiWaterEncounters?.Tables??CinnabarWaterEncounters?.Tables??SurfEncounters?.Tables;
    public static PretCampaignCatalog LoadSurfProfile(string root,bool useCompactMapData=true,string? surfContentRoot=null)=>
        LoadFuchsiaGymProfile(root,useCompactMapData,includePresetDoubles:true,includeSafari:true,includeSurf:true,surfContentRoot:surfContentRoot);
    private static void AppendSurfSources(PretSurfScripts surf,List<PretSourceScriptFile> sources,List<string> texts,
        Dictionary<string,int> constants,List<string> hashes)
    {
        var prior=sources.SingleOrDefault(s=>s.Path==surf.Script.Path);
        if(prior is not null && prior!=surf.Script)throw new InvalidDataException("surf_source_conflict");
        if(prior is null)sources.Add(surf.Script);
        if(!texts.Contains(surf.Text,StringComparer.Ordinal))texts.Add(surf.Text);
        MergeRegionalServiceConstants(constants,surf.Constants);hashes.AddRange(surf.AdditionalSourceHashes);
        hashes.Add("surf-campaign:"+SurfContentProfile);
    }
    // The source assembler places these three labels contiguously. The general compiler
    // represents one program per label, so the successor explicitly includes the original
    // fallthrough suffix instead of depending on dictionary order or inventing instructions.
    private static PretStoryCompilation CompleteSurfSourceFallthrough(PretStoryCompilation compiled)
    {
        StoryProgram Program(string label, int[] lines, string[] opcodes)
        {
            var program=compiled.FindProgram(label)??throw new InvalidDataException("surf_source_program_missing: "+label);
            if(!program.Instructions.Select(i=>i.Source.Line).SequenceEqual(lines)||
                !program.Instructions.Select(i=>i.Opcode).SequenceEqual(opcodes)||
                program.Instructions.Any(i=>i.Source.Path!="data/scripts/surf.inc"||i.Source.Symbol!=label))
                throw new InvalidDataException("surf_source_fallthrough_identity: "+label);
            return program;
        }
        var use=Program("EventScript_UseSurf",[2,3,4,5,6,7,8,9,10,11],
            ["goto_if_questlog","checkpartymove","goto_if_eq","bufferpartymonnick","setfieldeffectargument",
             "lockall","msgbox","goto_if_eq","msgbox","dofieldeffect"]);
        var release=Program("EventScript_UseSurfRelease",[13],["releaseall"]);
        var end=Program("EventScript_UseSurfEnd",[15],["end"]);
        var programs=compiled.Programs.SetItem(use.Id,use with{Instructions=use.Instructions.AddRange(release.Instructions).AddRange(end.Instructions)})
            .SetItem(release.Id,release with{Instructions=release.Instructions.AddRange(end.Instructions)});
        return compiled with{Programs=programs};
    }

}
