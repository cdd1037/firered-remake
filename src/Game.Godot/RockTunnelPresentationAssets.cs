using Game.Content;
using System.Text.Json;
internal static class RockTunnelPresentationAssets
{

    internal static Dictionary<string,byte[]> Load(string root)
    {
        return PresentationPackage.Load(root, "content/generated/sprites/trainers/rocktunnel/", relativeOutputKeys: true).Files;
    }
}
