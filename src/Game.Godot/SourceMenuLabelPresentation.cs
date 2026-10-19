using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using Game.Domain.Events;

/// <summary>The source's CLEAR_TO/FONT_SMALL price suffix; labels remain source data.</summary>
internal sealed record SourceMenuLabelPresentation(string Label,int? SuffixX,string? Suffix)
{
    internal static string Plain(IReadOnlyDictionary<string,string> source,string symbol,
        IReadOnlyDictionary<string,string>? controls=null)
    {
        if(!source.TryGetValue(symbol,out var text) || string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Missing source menu text: "+symbol);
        if(controls is not null && controls.Keys.Any(key=>!text.Contains("{"+key+"}",StringComparison.Ordinal)))
            throw new InvalidDataException("Missing source menu text control: "+symbol);
        var pages=SourceBattleText.Pages(source,symbol,controls??ImmutableDictionary<string,string>.Empty);
        if(pages.Length!=1 || string.IsNullOrWhiteSpace(pages[0]) || pages[0].IndexOfAny(['\r','\n'])>=0)
            throw new InvalidDataException("Unsupported source menu text: "+symbol);
        var label=Parse(pages[0]);
        if(label.SuffixX is not null)throw new InvalidDataException("Unexpected source menu suffix: "+symbol);
        return label.Label;
    }

    internal static SourceMenuLabelPresentation Parse(string text)
    {
        var match=Regex.Match(text,@"^([^{}]+)\{CLEAR_TO 0x([0-9A-Fa-f]+)\}\{FONT_SMALL\}([^{}]+)$");
        if(match.Success)return new(match.Groups[1].Value,int.Parse(match.Groups[2].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture),match.Groups[3].Value);
        if(text.Contains('{')||text.Contains('}'))throw new InvalidDataException("source_menu_text_control");
        return new(text,null,null);
    }
}
