using System.Text.RegularExpressions;
namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool IsEmptySourceMapHeader(PretSourceScriptFile file,string label,string[] body)
    {
        // Empty headers are admitted only under their matching map name.
        if (!IsMapScriptSource(file) ||
            body.Length != 1 || body[0] != ".byte 0") return false;
        var map = Regex.Match(file.Path, @"^data/maps/([A-Za-z0-9_]+)/scripts\.inc$");
        return map.Success && label == map.Groups[1].Value + "_MapScripts";
    }

    private static bool IsMapScriptSource(PretSourceScriptFile file) => Regex.IsMatch(file.Path, @"^data/maps/[A-Za-z0-9_]+/scripts\.inc$");
}
