using Game.Content;
using System.Text.Json;

/// <summary>Additional original pixels; no runtime decoder or upstream execution.</summary>
internal static class CeladonApproachPresentationAssets
{

    internal static Dictionary<string,byte[]> Load(string root)
    {
        return PresentationPackage.Load(root, "content/generated/sprites/trainers/celadon-approach/", relativeOutputKeys: true).Files;
    }
}
