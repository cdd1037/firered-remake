namespace Game.Content;

/// <summary>Resolves generated resource paths without following file or directory links.</summary>
public static class GeneratedResourceFile
{
    public static string SafePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') || relativePath.Contains(':') ||
            relativePath.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Unsafe generated resource path: " + relativePath);

        var directory = new DirectoryInfo(Path.GetFullPath(root));
        for (var ancestor = directory; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.LinkTarget is not null)
                throw new InvalidDataException("Generated resource directory symlink is unsupported: " + relativePath);

        var path = directory.FullName;
        foreach (var part in relativePath.Split('/'))
        {
            path = Path.Combine(path, part);
            if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
                throw new InvalidDataException("Generated resource symlink is unsupported: " + relativePath);
        }
        return path;
    }

    public static byte[] Read(string root, string relativePath) => File.ReadAllBytes(SafePath(root, relativePath));
}
