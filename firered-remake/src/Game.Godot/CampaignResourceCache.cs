using Godot;

/// <summary>Scene-local ownership of exact RGBA8, no-mipmap textures. Use only on Godot's main thread.</summary>
public sealed class CampaignResourceCache : IDisposable
{
    private sealed record Entry(Texture2D Texture, long Bytes) { public int Owners; }
    private readonly Dictionary<string, Entry> _textures = new(StringComparer.Ordinal);
    private readonly HashSet<Owner> _owners = [];
    private long _serial, _loads, _hits, _releases, _peakBytes;
    private bool _disposed;
    public sealed record Statistics(int Owners, int Textures, long RgbaBytes, long PeakRgbaBytes,
        long Decodes, long CacheHits, long Releases);
    public Statistics Snapshot => new(_owners.Count, _textures.Count, _textures.Values.Sum(e => e.Bytes),
        _peakBytes, _loads, _hits, _releases);

    public Owner CreateOwner(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var owner = new Owner(this, name); _owners.Add(owner); return owner;
    }

    public sealed class Owner : IDisposable
    {
        private readonly CampaignResourceCache _cache;
        internal readonly HashSet<string> Keys = new(StringComparer.Ordinal);
        private bool _disposed;
        public string Name { get; }
        internal Owner(CampaignResourceCache cache, string name) { _cache = cache; Name = name; }
        // Resource files are static for a scene. Share each path and conversion mode across owners.
        public Texture2D Texture(string path, bool darkened = false)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string key = Path.GetFullPath(path) + (darkened ? ":rgba8-bitrep-dark8-no-mips" : ":rgba8-bitrep-no-mips");
            if (_cache._textures.TryGetValue(key, out var shared))
            {
                _cache._hits++; Retain(key, shared); return shared.Texture;
            }
            byte[] bytes = File.ReadAllBytes(path);
            using var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok || image.IsEmpty())
                throw new InvalidDataException("Cannot decode texture: " + path);
            image.Convert(Image.Format.Rgba8); var pixels = image.GetData();
            for (int i = 0; i < pixels.Length; i += 4)
                if (pixels[i + 3] != 0) for (int c = 0; c < 3; c++)
                {
                    int value = pixels[i + c] >> 3;
                    if (darkened) value += (-value * 8) >> 4;
                    pixels[i + c] = (byte)((value << 3) | (value >> 2));
                }
            using var display = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, pixels);
            var texture = ImageTexture.CreateFromImage(display);
            var entry = new Entry(texture, pixels.LongLength);
            _cache._textures.Add(key, entry); Retain(key, entry); _cache._loads++; _cache.ObservePeak();
            return texture;
        }
        public Texture2D FromImage(Image image)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (image.GetFormat() != Image.Format.Rgba8 || image.HasMipmaps())
                throw new InvalidDataException("Owned map edit must be RGBA8 without mipmaps.");
            string key = "transient:" + (++_cache._serial);
            var texture = ImageTexture.CreateFromImage(image);
            var entry = new Entry(texture, (long)image.GetWidth() * image.GetHeight() * 4);
            _cache._textures.Add(key, entry); Retain(key, entry); _cache.ObservePeak(); return texture;
        }
        private void Retain(string key, Entry entry) { if (Keys.Add(key)) entry.Owners++; }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (string key in Keys)
            {
                var entry = _cache._textures[key];
                if (--entry.Owners != 0) continue;
                _cache._textures.Remove(key); entry.Texture.Dispose(); _cache._releases++;
            }
            Keys.Clear(); _cache._owners.Remove(this);
        }
    }
    private void ObservePeak() => _peakBytes = Math.Max(_peakBytes, _textures.Values.Sum(e => e.Bytes));
    private long BytesFor(IEnumerable<Owner> owners) => owners.SelectMany(o => o.Keys).Distinct(StringComparer.Ordinal).Sum(k => _textures[k].Bytes);
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var owner in _owners.ToArray()) owner.Dispose();
    }

    /// <summary>LRU of map owners; active/current-frame pins are never evicted, even above budget.</summary>
    public sealed class MapCache<T>(CampaignResourceCache resources, int capacity = 3, long byteBudget = 24 * 1024 * 1024) : IDisposable where T : class
    {
        private sealed record MapEntry(T Art, Owner Owner) { public long LastUse; }
        private readonly Dictionary<string, MapEntry> _maps = new(StringComparer.Ordinal);
        private HashSet<string> _pins = new(StringComparer.Ordinal);
        private long _clock;
        private bool _disposed;
        public int Count => _maps.Count;
        public long RgbaBytes => resources.BytesFor(_maps.Values.Select(m => m.Owner));
        public int Capacity => capacity;
        public long ByteBudget => byteBudget;
        public long Loads { get; private set; }
        public long Evictions { get; private set; }
        public bool PinnedOverBudget => Count > capacity || RgbaBytes > byteBudget;
        public string[] ResidentIds => _maps.Keys.Order(StringComparer.Ordinal).ToArray();
        public void SetPins(IEnumerable<string> ids)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pins = ids.ToHashSet(StringComparer.Ordinal); Trim();
        }
        public T Get(string id, Func<Owner, T> load)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_pins.Contains(id)) throw new InvalidOperationException("Pin a map before acquiring it: " + id);
            if (_maps.TryGetValue(id, out var present)) { present.LastUse = ++_clock; return present.Art; }
            var owner = resources.CreateOwner("map:" + id);
            try
            {
                T art = load(owner); _maps.Add(id, new(art, owner) { LastUse = ++_clock }); Loads++; Trim(); return art;
            }
            catch { owner.Dispose(); throw; }
        }
        private void Trim()
        {
            while (Count > capacity || RgbaBytes > byteBudget)
            {
                var oldest = _maps.Where(e => !_pins.Contains(e.Key)).OrderBy(e => e.Value.LastUse).FirstOrDefault();
                if (oldest.Value is null) break;
                _maps.Remove(oldest.Key); oldest.Value.Owner.Dispose(); Evictions++;
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var map in _maps.Values) map.Owner.Dispose(); _maps.Clear(); _pins.Clear();
        }
    }
}
