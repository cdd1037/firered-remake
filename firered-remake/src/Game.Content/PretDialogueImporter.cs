using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record PretDialogues(ImmutableDictionary<string, ReadOnlyDialogue> Programs,
    ImmutableArray<ContentDiagnostic> UnsupportedScripts, SourceRef ScriptSource, SourceRef TextSource);
/// <summary>Only recognizes a complete msgbox/end script. It never drops unimplemented side effects.</summary>
public static partial class PretDialogueImporter
{
    public static PretDialogues Import(string scripts, string texts, SourceRef scriptSource, SourceRef textSource)
    {
        VerifySource(scripts, scriptSource); VerifySource(texts, textSource);
        var scriptBlocks = Blocks(scripts); var textBlocks = Blocks(texts);
        var programs = ImmutableDictionary.CreateBuilder<string, ReadOnlyDialogue>(StringComparer.Ordinal);
        var unsupported = ImmutableArray.CreateBuilder<ContentDiagnostic>();
        foreach (var (label, body) in scriptBlocks.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var lines = body.Where(x => x.Length > 0 && !x.StartsWith('@')).ToArray();
            if (lines.Length != 2 || lines[1] != "end" || !Message().IsMatch(lines[0]))
            { unsupported.Add(new("script_not_readonly_subset", label, "Only exactly msgbox then end is enabled; no instruction was skipped.")); continue; }
            var textSymbol = Message().Match(lines[0]).Groups[1].Value;
            if (!textBlocks.TryGetValue(textSymbol, out var parts)) throw ContentJson.Error("dialogue_text_missing", label, textSymbol);
            var encoded = new StringBuilder();
            foreach (var line in parts.Where(x => x.Length > 0 && !x.StartsWith('@')))
            {
                var match=StringLine().Match(line);
                if(!match.Success)throw ContentJson.Error("unsupported_text_directive",textSymbol,line);
                encoded.Append(match.Groups[1].Value);
            }
            var raw=encoded.ToString();
            if(!raw.EndsWith('$')||raw[..^1].Contains('$'))throw ContentJson.Error("text_terminator",textSymbol,"Text must have one final source terminator.");
            raw=raw[..^1];
            var controls=TextControl().Matches(raw).Select(m=>m.Value).ToArray();
            if(controls.Any(x=>x is not ("{PLAYER}" or "{RIVAL}")))throw ContentJson.Error("unsupported_text_control",textSymbol,"A variable or text-control command is unsupported.");
            if(Regex.IsMatch(raw,@"\\(?![np])"))throw ContentJson.Error("unsupported_text_escape",textSymbol,"Only source line and page separators are supported.");
            var pages=raw.Split("\\p",StringSplitOptions.None).Select(x=>x.Replace("\\n","\n",StringComparison.Ordinal)).ToImmutableArray();
            if(pages.IsEmpty||pages.Any(string.IsNullOrEmpty))throw ContentJson.Error("dialogue_empty_page",textSymbol,"Empty text page.");
            programs.Add(label,new(new ScriptId("pret:"+label),new TextId("pret:"+textSymbol),pages));
        }
        return new(programs.ToImmutable(),unsupported.ToImmutable(),scriptSource,textSource);
    }
    private static void VerifySource(string text,SourceRef source)
    {
        var errors=ContentValidator.ValidateSource(source,"sourceRef");
        if(errors.Count>0)throw new ContentValidationException(errors);
        var actual=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        if(source.Kind==SourceKind.Pret&&source.Sha256 is null)throw ContentJson.Error("source_hash_required",source.Path,"Pinned source SHA-256 is required.");
        if(source.Sha256 is not null&&actual!=source.Sha256)throw ContentJson.Error("source_hash_mismatch",source.Path,"Dialogue source bytes differ from the lock.");
    }
    private static Dictionary<string,List<string>> Blocks(string source)
    {
        var result=new Dictionary<string,List<string>>(StringComparer.Ordinal);List<string>? current=null;
        foreach(var line in source.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n'))
        {
            var trimmed=line.Trim();var match=Label().Match(trimmed);
            if(match.Success){current=[];if(!result.TryAdd(match.Groups[1].Value,current))throw ContentJson.Error("duplicate_label",match.Groups[1].Value,"Duplicate source label.");}
            else current?.Add(trimmed);
        }
        return result;
    }
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*):{1,2}$",RegexOptions.CultureInvariant)] private static partial Regex Label();
    [GeneratedRegex(@"^msgbox\s+([A-Za-z_][A-Za-z0-9_]*)(?:,\s*(?:MSGBOX_SIGN|MSGBOX_NPC|MSGBOX_DEFAULT))?$",RegexOptions.CultureInvariant)] private static partial Regex Message();
    [GeneratedRegex("^\\.string\\s+\"(.*)\"$",RegexOptions.CultureInvariant)] private static partial Regex StringLine();
    [GeneratedRegex(@"\{[^}]*\}",RegexOptions.CultureInvariant)] private static partial Regex TextControl();
}
