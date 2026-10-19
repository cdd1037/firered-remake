using Game.Content;
using System.Text.Json;

internal sealed record SeviiRubyPresentationData(JsonElement Manifest, Dictionary<string, byte[]> Files);

/// <summary>Original pixels and cue metadata; gameplay state remains outside presentation.</summary>
internal static class SeviiRubyPresentationAssets
{
    internal const string Base = "content/generated/sprites/pokemon/sevii-ruby/";

    // The story bank owns words; this package supplies only the admitted label and glyph geometry.
    internal static string BrailleText(JsonElement braille, JsonElement message, string symbol, string? text)
    {
        if (message.ValueKind == JsonValueKind.Undefined || message.GetProperty("label").GetString() != symbol ||
            string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Braille message identity is missing.");
        var glyphs = braille.GetProperty("glyphs").EnumerateArray()
            .Select(glyph => glyph.GetProperty("character").GetString()).ToHashSet(StringComparer.Ordinal);
        if (text.Any(character => !glyphs.Contains(character.ToString())))
            throw new InvalidDataException("Braille text contains a character outside its glyph catalog.");
        return text;
    }

    internal static SeviiRubyPresentationData Load(string root)
    {
        var package = PresentationPackage.Load(root, Base, false, "assets");
        var manifest = package.Manifest;
        var files = package.Files;
        var sprites = manifest.GetProperty("sprites").EnumerateArray().ToArray();
        var expectedSprites = (from species in new[] { 218, 219 } from side in new[] { "front", "back" }
            from variant in new[] { "normal", "shiny" } select (species, side, variant)).ToHashSet();
        if (!sprites.Select(s => (s.GetProperty("sourceSpeciesId").GetInt32(),
                s.GetProperty("side").GetString()!, s.GetProperty("paletteVariant").GetString()!)).ToHashSet().SetEquals(expectedSprites) ||
            sprites.Any(s => s.GetProperty("speciesId").GetString() != (s.GetProperty("sourceSpeciesId").GetInt32() == 218 ? "pret:SPECIES_SLUGMA" : "pret:SPECIES_MAGCARGO")) || !manifest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetString()).ToHashSet().SetEquals(
                ["pret:ITEM_RUBY", "pret:ITEM_RAINBOW_PASS", "pret:ITEM_HM06", "pret:ITEM_BLACK_BELT", "pret:ITEM_DIRE_HIT", "pret:ITEM_FIRE_STONE"]))
            throw new InvalidDataException("Sevii Ruby presentation inventory differs.");
        return new(manifest.Clone(), files);
    }
}
