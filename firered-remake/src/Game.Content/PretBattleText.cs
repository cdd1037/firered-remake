using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
namespace Game.Content;

/// <summary>Bounded C text extraction for the lab battle. Does not execute C or silently discard unknown controls.</summary>
public static class PretBattleText
{
    public static ImmutableSortedDictionary<string,string> Load(string root)
    {
        var result=ImmutableSortedDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        foreach (var path in new[] { "src/battle_message.c", "src/strings.c" })
        {
            var bytes=File.ReadAllBytes(Path.Combine(root,PretSourcePaths.FireRedRawDirectory,path));
            foreach(Match m in Regex.Matches(Encoding.UTF8.GetString(bytes),"(?:static )?const u8 ([A-Za-z0-9_]+)\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);",RegexOptions.CultureInvariant))
                result.TryAdd(m.Groups[1].Value,m.Groups[2].Value);
        }
        foreach(var symbol in new[]{"sText_Trainer1WantsToBattle","sText_Trainer1SentOutPkmn","sText_GoPkmn","gText_ForPetesSake","gText_TheTrainerThat","gText_TryBattling","gText_KeepAnEyeOnHP","gText_PkmnHPRestoredByVar2","gText_InflictingDamageIsKey","gText_LoweringStats","gText_OakNoRunningFromATrainer","gText_WinEarnsPrizeMoney","gText_HowDissapointing","gText_OakImportantToGetToKnowPokemonThroughly","gText_OakThisIsListOfPokemon","sText_AttackerUsedX","sText_AttackMissed","sText_DefendersStatFell","sText_StatsWontDecrease","sText_CriticalHit","sText_TargetFainted","sText_PkmnHitWithRecoil","sText_PkmnGainedEXP","sText_PkmnGrewToLv","sText_PlayerGotMoney"})
            if(!result.ContainsKey(symbol))throw new InvalidDataException("Battle text missing: "+symbol);
        return result.ToImmutable();
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
