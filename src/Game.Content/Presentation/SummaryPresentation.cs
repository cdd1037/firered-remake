using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Game.Content;

public sealed record SummarySpritePlacement(int X, int Y, int Width, int Height, bool FlipHorizontal);

public sealed record SummaryMemoFacts(string Nature, string MetLocation, int MetLevel,
    int MetGame, bool ModernFatefulEncounter, bool HeldByOriginalTrainer);

/// <summary>Generated single-player Summary prose and portrait motion, without source-file access.</summary>
public sealed class SummaryPresentation
{
    private static readonly Regex MemoParameter = new(@"\{(?:DYNAMIC 0x0[012]|LV_2)\}", RegexOptions.CultureInvariant);
    public ImmutableDictionary<string, string> Moves { get; }
    public ImmutableDictionary<string, string> Abilities { get; }
    private readonly ImmutableDictionary<string, string> memos, locations;
    private readonly ImmutableDictionary<string, bool> facing;
    private readonly ImmutableDictionary<string, ImmutableArray<int>> motions;
    private SummaryPresentation(JsonElement root, SourceTextPack pack)
    {
        ImmutableDictionary<string,string> Text(string key) => root.GetProperty(key).EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => p.Value.GetString() ?? throw new InvalidDataException("Missing Summary text."));
        Moves = Text("moves"); Abilities = Text("abilities"); memos = Text("memos"); locations = Text("locations");
        facing = root.GetProperty("facing").EnumerateObject().ToImmutableDictionary(p => p.Name, p => p.Value.GetBoolean());
        motions = root.GetProperty("motions").EnumerateObject().ToImmutableDictionary(p => p.Name,
            p => p.Value.EnumerateArray().Select(v => v.GetInt32()).ToImmutableArray());
        if (Moves.Count == 0 || Abilities.Count == 0 || locations.Count == 0 ||
            new[] { "ATrade", "MetInATrade", "FatefulEncounterMet", "Met", "ApparentlyMet", "Hatched", "FatefulEncounterHatched", "ApparentlyFatefulEncounterHatched" }.Any(k => !memos.TryGetValue(k, out var text) || string.IsNullOrWhiteSpace(text)) ||
            motions.Count != 7 || motions.Values.Any(v => v.IsDefaultOrEmpty || v.Sum() != 0))
            throw new InvalidDataException("Incomplete Summary presentation.");
        Moves = pack.Apply("summary-moves", Moves);
        Abilities = pack.Apply("summary-abilities", Abilities);
        memos = pack.Apply("summary-memos", memos, preserveControls: true);
        locations = pack.Apply("summary-locations", locations);
    }
    public static SummaryPresentation Load(string root, SourceTextPack? pack = null)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"content/generated/ui/summary-screen/text.json")));
        return new(document.RootElement, pack ?? SourceTextPack.Current);
    }
    // DrawTextureRectRegion mirrors UVs for negative widths without shifting the
    // supplied left edge. Keep the native sprite centre at (60,65) / (24,32).
    public static SummarySpritePlacement PortraitPlacement(bool icon, bool flip, int verticalOffset = 0)
        => icon ? new(8, 16, 32, 32, flip) : new(28, 33 + verticalOffset, 64, 64, flip);
    public bool FlipPortrait(string species) => facing[species.Replace("pret:", "")];
    public string TrainerMemo(SummaryMemoFacts facts)
    {
        bool local = locations.TryGetValue(facts.MetLocation, out var place);
        bool fateful = facts.MetLocation == "METLOC_FATEFUL_ENCOUNTER";
        string key;
        if (!facts.HeldByOriginalTrainer && (!local || facts.MetGame is < 1 or > 5))
            key = fateful ? "FatefulEncounterMet" : "MetInATrade";
        else if (facts.MetLevel == 0)
            key = facts.ModernFatefulEncounter
                ? facts.HeldByOriginalTrainer ? "FatefulEncounterHatched" : "ApparentlyFatefulEncounterHatched"
                : facts.HeldByOriginalTrainer ? "Hatched" : "ApparentlyMet";
        else key = fateful ? "FatefulEncounterMet" : facts.HeldByOriginalTrainer ? "Met" : "ApparentlyMet";
        // Bind only source-template tokens. Inserted display names stay literal.
        return MemoParameter.Replace(memos[key], match => match.Value switch
        {
            "{DYNAMIC 0x00}" => facts.Nature,
            "{DYNAMIC 0x01}" => (facts.MetLevel == 0 ? 5 : facts.MetLevel).ToString(),
            "{DYNAMIC 0x02}" => place ?? memos["ATrade"],
            _ => "Lv"
        });
    }
    public int PortraitOffset(int frame, int hp, int maxHp, bool egg, int eggCycles, bool status)
    {
        if (frame < 0 || !egg && (status || hp == 0)) return 0;
        string key;
        int steps;
        if (egg)
        {
            key = eggCycles <= 5 ? "AlmostReadyToHatch" : eggCycles <= 10 ? "OccasionallyMoves" : "ItWillTakeSomeTime";
            var deltas = motions["sEggPicShakeXDelta_" + key];
            int delay = eggCycles <= 5 ? 60 : eggCycles <= 10 ? 90 : 120;
            int cycle = delay + deltas.Length;
            if (frame >= cycle * 2) return 0;
            steps = Math.Clamp(frame % cycle - delay + 1, 0, deltas.Length);
            return deltas.Take(steps).Sum();
        }
        key = hp == maxHp ? "Full" : hp * 5 >= maxHp * 4 ? "80to99" : hp * 5 >= maxHp * 3 ? "60to80" : "Under60";
        var bounce = motions["sMonPicBounceYDelta_" + key];
        steps = (frame + 1) / 3;
        return steps >= bounce.Length * 2 ? 0 : bounce.Take(steps % bounce.Length).Sum();
    }
}
