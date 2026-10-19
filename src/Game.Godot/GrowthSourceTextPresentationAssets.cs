using System.Collections.Immutable;

/// <summary>Level-up labels and the evolution cancel control from the shared source bank.</summary>
internal sealed class SourceGrowthText
{
    internal ImmutableArray<string> StatLabels { get; }
    internal string EvolutionCancel { get; }

    internal SourceGrowthText(IReadOnlyDictionary<string,string> source)
    {
        // This view retains its normal-font stat layout; only the source's two font
        // switches are consumed. Unknown controls still fail in the shared parser.
        StatLabels=[SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_MaxHP",
                new Dictionary<string,string>(StringComparer.Ordinal) { ["FONT_SMALL"]="", ["FONT_NORMAL"]="" }),
            SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_Attack"),
            SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_Defense"),
            SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_SpAtk"),
            SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_SpDef"),
            SourceMenuLabelPresentation.Plain(source,"gText_LevelUp_Speed")];
        EvolutionCancel=SourceMenuLabelPresentation.Plain(source,"gText_BButtonCancel",
            new Dictionary<string,string>(StringComparer.Ordinal) { ["B_BUTTON"]="B/Backspace: " });
    }
}
