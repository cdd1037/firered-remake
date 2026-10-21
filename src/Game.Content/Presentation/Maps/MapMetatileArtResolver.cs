namespace Game.Content;

/// <summary>Immutable presentation bindings. Layout art overrides map art; unbound tiles
/// may be copied from the explicitly supplied historical or live source layout.</summary>
public sealed class MapMetatileArtResolver<T> where T : class
{
    private readonly IReadOnlyDictionary<(string Map, int Tile, int Layer), T> _maps;
    private readonly IReadOnlyDictionary<(string Layout, int Tile, int Layer), T> _layouts;

    public MapMetatileArtResolver(
        IReadOnlyDictionary<(string Map, int Tile, int Layer), T> maps,
        IReadOnlyDictionary<(string Layout, int Tile, int Layer), T> layouts)
    {
        _maps = new Dictionary<(string, int, int), T>(maps);
        _layouts = new Dictionary<(string, int, int), T>(layouts);
        if (_maps.Keys.Any(k => !Valid(k.Tile, k.Layer)) || _layouts.Keys.Any(k => !Valid(k.Tile, k.Layer)))
            throw new InvalidDataException("map_metatile_art_binding_invalid");
    }

    public T Resolve(string map, string layout, int tile, int layer, Func<T?> sourceTile)
    {
        if (!Valid(tile, layer)) throw new ArgumentOutOfRangeException(nameof(tile));
        if (_layouts.TryGetValue((layout, tile, layer), out var art) || _maps.TryGetValue((map, tile, layer), out art)) return art;
        return sourceTile() ?? throw new InvalidDataException($"map_metatile_art_missing: {map}/{layout}/{tile}/{layer}");
    }

    private static bool Valid(int tile, int layer) => tile is >= 0 and <= 1023 && layer is >= 0 and <= 2;
}
