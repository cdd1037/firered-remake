using System.Collections.Immutable;
using System.Text;
namespace Game.Content;

/// <summary>Imported battle text and presentation control expansion.</summary>
public static class PretBattleText
{
    public static ImmutableSortedDictionary<string,string> Load(string root, SourceTextPack? pack = null)
    {
        using var stream = File.OpenRead(Path.Combine(root, "content/generated/battle/text.json"));
        var texts = System.Text.Json.JsonSerializer.Deserialize<SortedDictionary<string, string>>(stream)
            ?? throw new InvalidDataException("Battle text catalog is empty.");
        return (pack ?? SourceTextPack.Current).Apply("battle", texts, preserveControls: true)
            .ToImmutableSortedDictionary(StringComparer.Ordinal);
    }
    public static ImmutableArray<string> Pages(IReadOnlyDictionary<string,string> source,string symbol,IReadOnlyDictionary<string,string> values)
    {
        var text=source[symbol];
        // \l waits then scrolls the previous bottom line upwards; it does not clear a page.
        var pages=ImmutableArray.CreateBuilder<string>();var lines=new List<string>();var line=new StringBuilder();
        for(int i=0;i<text.Length;i++)
        {
            if(text[i]=='{')
            {
                int end=text.IndexOf('}',i+1);
                if(end<0)throw new InvalidDataException("Unresolved battle text field: "+symbol);
                string token=text[(i+1)..end];
                // Parse only authored template controls. Names and other bound values are literal.
                if(token is not ("PAUSE 60" or "WAIT_SE" or "PAUSE_UNTIL_PRESS"))
                {
                    if(!values.TryGetValue(token,out var value))
                        throw new InvalidDataException("Unresolved battle text field: "+symbol);
                    line.Append(value);
                }
                i=end;continue;
            }
            if(text[i]=='}')throw new InvalidDataException("Unresolved battle text field: "+symbol);
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
