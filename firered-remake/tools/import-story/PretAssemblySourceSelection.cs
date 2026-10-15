namespace Game.Content;

/// <summary>Bounded assembly edition selection; blank lines preserve original source line numbers.</summary>
public static class PretAssemblySourceSelection
{
    public static string[] SelectFireRedLines(string path, IReadOnlyList<string> lines)
    {
        var result = new string[lines.Count];
        var stack = new Stack<(bool ParentActive, bool Condition, bool HadElse)>();
        bool active = true;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].Split('@')[0].Trim();
            string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string directive = tokens.FirstOrDefault() ?? "";
            if (directive == ".ifdef")
            {
                if (tokens.Length != 2 || tokens[1] is not ("FIRERED" or "LEAFGREEN") || stack.Count >= 16)
                    throw Error(index, "Unsupported edition condition.");
                bool condition = tokens[1] == "FIRERED";
                stack.Push((active, condition, false));
                active = active && condition;
                result[index] = "";
            }
            else if (directive == ".else")
            {
                if (tokens.Length != 1 || !stack.TryPop(out var frame) || frame.HadElse)
                    throw Error(index, "Unmatched or repeated else.");
                stack.Push((frame.ParentActive, frame.Condition, true));
                active = frame.ParentActive && !frame.Condition;
                result[index] = "";
            }
            else if (directive == ".endif")
            {
                if (tokens.Length != 1 || !stack.TryPop(out var frame))
                    throw Error(index, "Unmatched endif.");
                active = frame.ParentActive;
                result[index] = "";
            }
            else
            {
                if (directive.StartsWith(".if", StringComparison.Ordinal) ||
                    directive.StartsWith(".else", StringComparison.Ordinal) ||
                    directive.StartsWith(".elif", StringComparison.Ordinal) ||
                    directive.StartsWith(".endif", StringComparison.Ordinal))
                    throw Error(index, "Unsupported conditional directive.");
                result[index] = active ? lines[index] : "";
            }
        }
        if (stack.Count != 0) throw Error(lines.Count, "Unclosed edition condition.");
        return result;

        ContentValidationException Error(int index, string message) =>
            ContentJson.Error("assembly_source_condition", $"{path}:{index + 1}", message);
    }
}
