using System.Collections.Immutable;

namespace Game.Content;

public sealed record PokedexAreaSprite(PokedexMenuTexture Texture, int SourceX, int SourceY, int Width, int Height);
public sealed record PokedexAreaRegion(string Id, string? UnlockFlag, int X, int Y, PokedexAreaSprite Sprite);
public sealed record PokedexAreaMarker(string Id, int X, int Y, PokedexAreaSprite Sprite);
public sealed record PokedexAreaBinding(string MapSection, string AreaId, string RegionId);
/// <summary>A source wild header's species, without rates or duplicated gameplay encounter tables.</summary>
public sealed record PokedexAreaEncounter(string MapSection, int AlteringCaveSet, ImmutableArray<int> Species);
public sealed record PokedexAreaRoamer(int SourceSpeciesId, int StarterVariable);
public sealed record PokedexAreaDefinition(ImmutableArray<PokedexAreaRegion> Regions,
    ImmutableArray<PokedexAreaMarker> Markers, ImmutableArray<PokedexAreaBinding> Bindings,
    ImmutableArray<PokedexAreaEncounter> Encounters, ImmutableArray<PokedexAreaRoamer> Roamers,
    int AlteringCaveSetCount, PokedexAreaSprite UnknownSprite);
/// <summary>Read-only live campaign inputs. Null roamer section means inactive or not initialized.</summary>
public sealed record PokedexAreaContext(IReadOnlyDictionary<string, bool> Flags, int AlteringCaveSet,
    int StarterVariable, string? ActiveRoamerMapSection);
public sealed record PokedexAreaPage(ImmutableArray<PokedexAreaRegion> Regions,
    ImmutableArray<PokedexAreaMarker> Markers, int VerticalOffset)
{
    public bool Unknown => Markers.IsEmpty;
}

public sealed partial class PretPokedexMenu
{
    private ImmutableDictionary<string, PokedexAreaMarker> _areaMarkers = ImmutableDictionary<string, PokedexAreaMarker>.Empty;
    private ImmutableDictionary<string, ImmutableArray<PokedexAreaBinding>> _areaBindings = ImmutableDictionary<string, ImmutableArray<PokedexAreaBinding>>.Empty;
    private ImmutableDictionary<int, ImmutableArray<PokedexAreaEncounter>> _speciesAreas = ImmutableDictionary<int, ImmutableArray<PokedexAreaEncounter>>.Empty;
    private ImmutableDictionary<int, PokedexAreaRoamer> _areaRoamers = ImmutableDictionary<int, PokedexAreaRoamer>.Empty;

    private void ValidateAreas()
    {
        var areas = Document.Areas;
        Require(areas is not null && !areas.Regions.IsDefaultOrEmpty && !areas.Markers.IsDefaultOrEmpty &&
            !areas.Bindings.IsDefaultOrEmpty && !areas.Encounters.IsDefaultOrEmpty && !areas.Roamers.IsDefaultOrEmpty &&
            areas.AlteringCaveSetCount > 0, "Pokédex area definitions are missing.");
        Require(areas!.Regions.Select(r => r.Id).Distinct().Count() == areas.Regions.Length &&
            areas.Markers.Select(m => m.Id).Distinct().Count() == areas.Markers.Length &&
            areas.Regions.Count(r => r.UnlockFlag is null) == 1 && areas.Regions[0].UnlockFlag is null,
            "Pokédex area identities or base region are invalid.");
        var regionIds = areas.Regions.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var markerIds = areas.Markers.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var region in areas.Regions)
            Require(!string.IsNullOrWhiteSpace(region.Id) && (region.UnlockFlag is null || !string.IsNullOrWhiteSpace(region.UnlockFlag)) &&
                region.X >= 0 && region.Y >= 0 && region.X + region.Sprite.Width <= 240 && region.Y + region.Sprite.Height <= 144,
                "Pokédex region image bounds are invalid.");
        foreach (var marker in areas.Markers)
            Require(!string.IsNullOrWhiteSpace(marker.Id) && marker.X >= 0 && marker.Y >= 0 &&
                marker.X + marker.Sprite.Width <= 240 && marker.Y + marker.Sprite.Height <= 144,
                "Pokédex marker bounds are invalid.");
        foreach (var binding in areas.Bindings)
            Require(!string.IsNullOrWhiteSpace(binding.MapSection) && regionIds.Contains(binding.RegionId) && markerIds.Contains(binding.AreaId),
                "Pokédex area binding is invalid.");
        foreach (var encounter in areas.Encounters)
            Require(!string.IsNullOrWhiteSpace(encounter.MapSection) && encounter.AlteringCaveSet >= -1 &&
                encounter.AlteringCaveSet < areas.AlteringCaveSetCount && !encounter.Species.IsDefaultOrEmpty &&
                encounter.Species.Distinct().Count() == encounter.Species.Length && encounter.Species.All(Entries.ContainsKey),
                "Pokédex wild area species are invalid.");
        Require(areas.Encounters.Where(e => e.AlteringCaveSet >= 0).Select(e => e.AlteringCaveSet).Order()
            .SequenceEqual(Enumerable.Range(0, areas.AlteringCaveSetCount)), "Pokédex Altering Cave sets are incomplete.");
        Require(areas.Roamers.Select(r => r.SourceSpeciesId).Distinct().Count() == areas.Roamers.Length &&
            areas.Roamers.All(r => Entries.ContainsKey(r.SourceSpeciesId) && r.StarterVariable is >= 0 and <= 2),
            "Pokédex roamer bindings are invalid.");
        foreach (var sprite in areas.Regions.Select(r => r.Sprite).Concat(areas.Markers.Select(m => m.Sprite)).Append(areas.UnknownSprite))
            Require(sprite.Width > 0 && sprite.Height > 0 && sprite.SourceX >= 0 && sprite.SourceY >= 0 &&
                sprite.SourceX + sprite.Width <= sprite.Texture.Width && sprite.SourceY + sprite.Height <= sprite.Texture.Height &&
                Document.Assets.Contains(sprite.Texture), "Pokédex area sprite or shared atlas bounds are invalid.");
        _areaMarkers = areas.Markers.ToImmutableDictionary(m => m.Id, StringComparer.Ordinal);
        _areaBindings = areas.Bindings.GroupBy(b => b.MapSection).ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray(), StringComparer.Ordinal);
        _speciesAreas = areas.Encounters.SelectMany(e => e.Species.Select(s => (Species: s, Encounter: e)))
            .GroupBy(x => x.Species).ToImmutableDictionary(g => g.Key, g => g.Select(x => x.Encounter).ToImmutableArray());
        _areaRoamers = areas.Roamers.ToImmutableDictionary(r => r.SourceSpeciesId);
    }

    /// <summary>
    /// Wild-table areas only, visible for seen species even when uncaught. Uses actual
    /// visited-island flags, selected Altering Cave table, and the active starter roamer's
    /// live location. Does not add gifts, static legendary encounters or acquisition hints.
    /// </summary>
    public PokedexAreaPage BuildAreaPage(int sourceSpeciesId, IEnumerable<int> seenSpecies,
        IEnumerable<int> caughtSpecies, PokedexMenuAccess access, PokedexAreaContext context)
    {
        _ = BuildPage(sourceSpeciesId, seenSpecies, caughtSpecies, access);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Flags);
        Require(context.AlteringCaveSet is >= 0 and <= ushort.MaxValue && context.StarterVariable is >= 0 and <= ushort.MaxValue,
            "Pokédex area variables are out of source bounds.");
        var regions = Document.Areas.Regions.Where(r => r.UnlockFlag is null || context.Flags.GetValueOrDefault(r.UnlockFlag)).ToImmutableArray();
        var visible = regions.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        // Source moves all maps down four tiles until any of islands 4–7 is visited.
        int offset = Document.Areas.Regions.Skip(4).Any(r => visible.Contains(r.Id)) ? 0 : 32;
        var markers = ImmutableArray.CreateBuilder<PokedexAreaMarker>();
        void AppendSection(string section, bool kantoOnly = false)
        {
            if (!_areaBindings.TryGetValue(section, out var bindings)) return;
            foreach (var binding in bindings)
                if (visible.Contains(binding.RegionId) && (!kantoOnly || binding.RegionId == Document.Areas.Regions[0].Id))
                    markers.Add(_areaMarkers[binding.AreaId]);
        }
        if (_areaRoamers.TryGetValue(sourceSpeciesId, out var roamer))
        {
            // GetStarterSpeciesById falls back to starter0 when the source variable is outside 0..2.
            int starter = context.StarterVariable <= 2 ? context.StarterVariable : 0;
            if (roamer.StarterVariable == starter && context.ActiveRoamerMapSection is { } section)
                AppendSection(section, kantoOnly: true);
        }
        else if (_speciesAreas.TryGetValue(sourceSpeciesId, out var encounters))
        {
            int caveSet = context.AlteringCaveSet < Document.Areas.AlteringCaveSetCount ? context.AlteringCaveSet : 0;
            foreach (var encounter in encounters)
                if (encounter.AlteringCaveSet < 0 || encounter.AlteringCaveSet == caveSet)
                    AppendSection(encounter.MapSection);
        }
        return new(regions, markers.DistinctBy(m => m.Id).ToImmutableArray(), offset);
    }
}
