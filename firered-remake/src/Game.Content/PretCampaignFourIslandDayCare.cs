using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretFourIslandDayCare FourIslandDayCare { get; }
    public bool FourIslandDayCareEnabled => true;
    public BreedingSpeciesMetadataCatalog? BreedingMetadata => BreedingSpeciesMetadataCatalog.For(PokemonContent!.Catalog);
    public IReadOnlyDictionary<int, string> DayCareCompatibilityTextSymbols { get; } =
        new Dictionary<int, string> { [70] = "gDaycareText_GetAlongVeryWell",[50] = "gDaycareText_GetAlong",[20] = "gDaycareText_DontLikeOther",[0] = "gDaycareText_PlayOther" }.ToImmutableDictionary();

    private static void AppendFourIslandDayCareTexts(PretFourIslandDayCare source,ImmutableDictionary<string, string>.Builder bank)
    {
        string text = new UTF8Encoding(false, true).GetString(source.CopySource("src/strings.c"));
        foreach (string symbol in new[] { "gDaycareText_GetAlongVeryWell","gDaycareText_GetAlong","gDaycareText_DontLikeOther","gDaycareText_PlayOther","gText_HatchedFromEgg","gText_NickHatchPrompt" })
        {
            var matches = Regex.Matches(text, "const u8 " + Regex.Escape(symbol) + @"\[\]\s*=\s*_\(""((?:\\.|[^""\\])*)""\);", RegexOptions.CultureInvariant);
            if (matches.Count != 1) throw new InvalidDataException("daycare_native_text: " + symbol);
            string value = matches[0].Groups[1].Value + "$";
            if (bank.TryGetValue(symbol, out var previous) && previous != value)
                throw new InvalidDataException("daycare_native_text_conflict: " + symbol);
            bank[symbol] = value;
        }
    }

    private static PretStoryCompilation CompleteFourIslandDayCareFallthrough(PretStoryCompilation compiled)
    {
        var branch = compiled.FindProgram("FourIsland_EventScript_TrySetDayCareManPos")
            ?? throw new InvalidDataException("daycare_position_program_missing");
        var end = compiled.FindProgram("FourIsland_EventScript_EndSetDayCareManPos")
            ?? throw new InvalidDataException("daycare_position_end_missing");
        if (!branch.Instructions.Select(i => i.Opcode).SequenceEqual(new[] { "goto_if_unset","setobjectxyperm" }) ||
            !branch.Instructions.Select(i => i.Source.Line).SequenceEqual(new[] { 17,18 }) ||
            branch.Instructions.Any(i => i.Source.Path != "data/maps/FourIsland/scripts.inc" || i.Source.Symbol != branch.Id || i.Op == StoryOp.Unsupported) ||
            !end.Instructions.Select(i => i.Opcode).SequenceEqual(new[] { "return" }) ||
            end.Instructions[0].Source.Line != 20 || end.Instructions[0].Source.Path != "data/maps/FourIsland/scripts.inc")
            throw new InvalidDataException("daycare_position_source_fallthrough");
        return compiled with { Programs = compiled.Programs.SetItem(branch.Id,
            branch with { Instructions = branch.Instructions.AddRange(end.Instructions) }) };
    }

    private static void ValidateFourIslandDayCareComposition(PretFourIslandDayCare daycare,ImmutableDictionary<MapId, PretWorldMap> maps,ImmutableDictionary<MapId, PretWorldMap> previous,PretStoryCompilation scripts)
    {
        if (maps.Count != previous.Count + 1 || previous.ContainsKey(PretFourIslandDayCare.HouseMapId) )
            throw new InvalidDataException("four_daycare_map_composition");
        var house = maps[PretFourIslandDayCare.HouseMapId]; var town = maps[new("map.four-island")];
        var door = town.FieldWarps.Single(w => w.Index == 1);
        if (door.X != 12 || door.Y != 13 || door.DestinationMapId != house.Id || door.DestinationWarpId != 0 ||
            house.FieldWarps.Length != 1 || house.FieldWarps[0] is not { X: 4, Y: 7, DestinationWarpId: 1 } ||
            house.FieldWarps[0].DestinationMapId != town.Id)
            throw new InvalidDataException("four_daycare_original_door");
        foreach (var root in new[] { PretFourIslandDayCare.EntryScript,PretFourIslandDayCare.ManEntryScript,"EventScript_EggHatch" })
            if (scripts.FindProgram(root) is not { Instructions.IsEmpty: false } p || p.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("four_daycare_story_missing: " + root);
    }
}
