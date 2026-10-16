using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>The source's CLEAR_TO/FONT_SMALL price suffix; labels remain source data.</summary>
internal sealed record SourceMenuLabelPresentation(string Label,int? SuffixX,string? Suffix)
{
    internal static SourceMenuLabelPresentation Parse(string text)
    {
        var match=Regex.Match(text,@"^([^{}]+)\{CLEAR_TO 0x([0-9A-Fa-f]+)\}\{FONT_SMALL\}([^{}]+)$");
        if(match.Success)return new(match.Groups[1].Value,int.Parse(match.Groups[2].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture),match.Groups[3].Value);
        if(text.Contains('{')||text.Contains('}'))throw new InvalidDataException("source_menu_text_control");
        return new(text,null,null);
    }
}
