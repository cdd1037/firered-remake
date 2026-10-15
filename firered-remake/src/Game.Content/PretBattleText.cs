using System.Collections.Immutable;
using System.Text;
namespace Game.Content;

/// <summary>Imported battle text and presentation control expansion.</summary>
public static class PretBattleText
{
    public static ImmutableSortedDictionary<string,string> Load(string root)
    {
        using var stream = File.OpenRead(Path.Combine(root, "content/generated/battle-text/catalog.json"));
        var texts = System.Text.Json.JsonSerializer.Deserialize<SortedDictionary<string, string>>(stream)
            ?? throw new InvalidDataException("Battle text catalog is empty.");
        return texts.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }
    public static ImmutableArray<string> Pages(IReadOnlyDictionary<string,string> source,string symbol,IReadOnlyDictionary<string,string> values)
    {
        var text=source[symbol];
        foreach(var value in values)text=text.Replace("{"+value.Key+"}",value.Value,StringComparison.Ordinal);
        // These supported controls affect presentation waits, never battle rules or random draws.
        text=text.Replace("{PAUSE 60}","",StringComparison.Ordinal).Replace("{WAIT_SE}","",StringComparison.Ordinal).Replace("{PAUSE_UNTIL_PRESS}","",StringComparison.Ordinal);
        if(text.Contains('{')||text.Contains('}'))throw new InvalidDataException("Unresolved battle text field: "+symbol);
        // \l waits then scrolls the previous bottom line upwards; it does not clear a page.
        var pages=ImmutableArray.CreateBuilder<string>();var lines=new List<string>();var line=new StringBuilder();
        for(int i=0;i<text.Length;i++)
        {
            if(text[i]!='\\'){line.Append(text[i]);continue;}
            if(++i==text.Length)throw new InvalidDataException("Trailing battle text escape: "+symbol);
            char control=text[i];
            if(control is '\"' or '\\'){line.Append(control);continue;}
            if(control is not ('n' or 'l' or 'p'))throw new InvalidDataException("Unsupported battle text escape: "+symbol);
            lines.Add(line.ToString());line.Clear();
            if(control=='p'){pages.Add(string.Join('\n',lines));lines.Clear();}
            else if(control=='l'){pages.Add(string.Join('\n',lines));var last=lines[^1];lines.Clear();lines.Add(last);}
        }
        if(line.Length>0||lines.Count>0){lines.Add(line.ToString());pages.Add(string.Join('\n',lines));}
        return pages.ToImmutable();
    }
}
