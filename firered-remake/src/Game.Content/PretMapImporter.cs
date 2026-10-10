using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

// The source map only names a layout; it does not contain layout bytes, graphics or dimensions.
public sealed record PretMapInput(string Id, string Name, string Layout, string Music,
    string RegionMapSection, bool RequiresFlash, string Weather, string MapType,
    bool AllowCycling, bool AllowEscaping, bool AllowRunning, bool ShowMapName,
    int FloorNumber, string BattleScene, ImmutableArray<PretConnection>? Connections,
    ImmutableArray<PretObjectEvent> ObjectEvents, ImmutableArray<PretWarpEvent> WarpEvents,
    ImmutableArray<PretCoordEvent> CoordEvents, ImmutableArray<PretBgEvent> BgEvents);
public sealed record PretConnection(string Map, int Offset, string Direction);
public sealed record PretObjectEvent(string LocalId, string Type, string GraphicsId, int X, int Y,
    int Elevation, string MovementType, int MovementRangeX, int MovementRangeY,
    string TrainerType, string TrainerSightOrBerryTreeId, string Script, string Flag);
public sealed record PretWarpEvent(int X, int Y, int Elevation, string DestMap, string DestWarpId);
public sealed record PretCoordEvent(string Type, int X, int Y, int Elevation,
    string Var, string VarValue, string Script);
public sealed record PretBgEvent(string Type, int X, int Y, int Elevation,
    string PlayerFacingDir, string Script);
public sealed record PretMapStructure(int SchemaVersion, bool IsPlayable,
    bool ScriptConversionAttempted, bool LayoutResolved, SourceRef SourceRef,
    PretMapInput Map, ImmutableArray<string> ReferencedScriptSymbols,
    ImmutableArray<ContentDiagnostic> Limitations);

public static class PretMapImporter
{
    // Pure, read-only parsing. The caller must establish the source's identity independently.
    // This never reads paths, follows includes, invokes upstream tools, or creates a playable map.
    public static PretMapStructure ImportStructure(string json, SourceRef source)
    {
        var diagnostics = ContentValidator.ValidateSource(source, "$.sourceRef").ToList();
        if (diagnostics.Count != 0) throw new ContentValidationException(diagnostics);
        if (json is null) throw ContentJson.Error("json_null", "$", "Map JSON is required.");
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        if (source.Sha256 is not null && source.Sha256 != actualHash)
            throw ContentJson.Error("source_hash_mismatch", "$.sourceRef.sha256", "Input bytes do not match the expected SHA-256.");
        var map = ContentJson.ParseStrict<PretMapInput>(json, ContentJson.CreateOptions(JsonNamingPolicy.SnakeCaseLower));
        void Error(string code, string path, string message) => diagnostics.Add(new(code, path, message));
        void Symbol(string? value, string path)
        {
            if (string.IsNullOrWhiteSpace(value) || !(char.IsAsciiLetter(value[0]) || value[0] == '_') || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_')))
                Error("source_symbol", path, "Expected a C/assembly source symbol.");
        }
        void Scalar(string? value, string path)
        {
            // Source constants or unsigned decimal/hex strings; expressions need an explicit parser.
            if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_')))
                Error("unsupported_scalar", path, "Only explicit source symbols or unsigned scalar strings are supported.");
        }
        void Position(int x, int y, int elevation, string path)
        {
            if (x is < -32768 or > 32767 || y is < -32768 or > 32767 || elevation is < 0 or > 15)
                Error("source_position", path, "Position exceeds the supported source field bounds.");
        }
        Symbol(map.Id, "$.id"); Symbol(map.Name, "$.name"); Symbol(map.Layout, "$.layout");
        Symbol(map.Music, "$.music"); Symbol(map.RegionMapSection, "$.region_map_section");
        Symbol(map.Weather, "$.weather"); Symbol(map.MapType, "$.map_type"); Symbol(map.BattleScene, "$.battle_scene");
        if (map.Id is not null && !map.Id.StartsWith("MAP_", StringComparison.Ordinal)) Error("source_map_id", "$.id", "Expected MAP_ identifier.");
        if (map.Layout is not null && !map.Layout.StartsWith("LAYOUT_", StringComparison.Ordinal)) Error("source_layout_id", "$.layout", "Expected LAYOUT_ identifier.");
        if (map.FloorNumber is < -128 or > 127) Error("source_floor", "$.floor_number", "Floor must fit the supported signed byte field.");
        if (source.Kind == SourceKind.Pret && (source.Path != $"data/maps/{map.Name}/map.json" || source.Symbol != map.Id))
            Error("source_identity", "$.sourceRef", "Pret path/name/id must identify this exact map.json.");
        if (map.Connections is { } connections)
        {
            if (connections.IsDefault) Error("missing_array", "$.connections", "Use an explicit array or null.");
            else
            {
                var directions = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < connections.Length; i++)
                {
                    var c = connections[i]; var p = $"$.connections[{i}]";
                    if (c is null) { Error("null_entry", p, "Null connection."); continue; }
                    Symbol(c.Map, p + ".map");
                    if (c.Direction is not ("up" or "down" or "left" or "right"))
                        Error("unsupported_connection", p + ".direction", "Only the four cardinal connection directions are supported.");
                    if (c.Direction is not null && !directions.Add(c.Direction)) Error("duplicate_connection", p, "Duplicate direction.");
                }
            }
        }
        var refs = new HashSet<string>(StringComparer.Ordinal);
        void Script(string? value, string path, bool allowNullSymbol = false)
        {
            if (allowNullSymbol && value is "0" or "0x0" or "NULL") return;
            Symbol(value, path);
            if (!string.IsNullOrWhiteSpace(value)) refs.Add(value);
        }
        void Events<T>(ImmutableArray<T> events, string name, Action<T, string> validate) where T : class
        {
            if (events.IsDefault) { Error("missing_array", "$.", name + " must be an explicit array."); return; }
            for (var i = 0; i < events.Length; i++)
                if (events[i] is { } item) validate(item, $"$.{name}[{i}]");
                else Error("null_entry", $"$.{name}[{i}]", "Null event.");
        }
        var localIds = new HashSet<string>(StringComparer.Ordinal);
        Events(map.ObjectEvents, "object_events", (o, p) =>
        {
            if (o.Type != "object") Error("unsupported_object_type", p + ".type", "Only ordinary object templates are supported.");
            Scalar(o.LocalId, p + ".local_id"); Symbol(o.GraphicsId, p + ".graphics_id");
            Symbol(o.MovementType, p + ".movement_type"); Symbol(o.TrainerType, p + ".trainer_type");
            Scalar(o.TrainerSightOrBerryTreeId, p + ".trainer_sight_or_berry_tree_id"); Scalar(o.Flag, p + ".flag");
            Position(o.X, o.Y, o.Elevation, p); Script(o.Script, p + ".script", allowNullSymbol: true);
            if (o.LocalId is not null && !localIds.Add(o.LocalId)) Error("duplicate_local_id", p, "Object local IDs must be unique.");
            if (o.MovementRangeX is < 0 or > 15 || o.MovementRangeY is < 0 or > 15)
                Error("source_movement_range", p, "Movement ranges must fit source nibbles.");
        });
        Events(map.WarpEvents, "warp_events", (w, p) =>
        {
            Position(w.X, w.Y, w.Elevation, p); Symbol(w.DestMap, p + ".dest_map");
            if (!int.TryParse(w.DestWarpId, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var id) || id is < 0 or > 127)
                Error("unsupported_warp_target", p + ".dest_warp_id", "Only explicit destination warp indices 0–127 are supported; dynamic/sentinel targets require separate handling.");
        });
        Events(map.CoordEvents, "coord_events", (c, p) =>
        {
            if (c.Type != "trigger") Error("unsupported_coord_type", p + ".type", "Only variable-conditioned triggers are supported.");
            Position(c.X, c.Y, c.Elevation, p); Symbol(c.Var, p + ".var"); Scalar(c.VarValue, p + ".var_value"); Script(c.Script, p + ".script");
        });
        Events(map.BgEvents, "bg_events", (b, p) =>
        {
            if (b.Type != "sign") Error("unsupported_bg_type", p + ".type", "Only sign events are supported; hidden-item and other event unions need explicit adapters.");
            Position(b.X, b.Y, b.Elevation, p); Symbol(b.PlayerFacingDir, p + ".player_facing_dir"); Script(b.Script, p + ".script");
        });
        if (diagnostics.Count != 0) throw new ContentValidationException(diagnostics);
        return new(1, false, false, false, source, map,
            refs.Order(StringComparer.Ordinal).ToImmutableArray(),
            [new("layout_unresolved", "$.layout", "Dimensions, grid bytes, tilesets, rendering and collision were not imported."),
             new("scripts_unconverted", "$.object_events", "Source script symbols are inventory only; no scripts or native specials were converted."),
             new("external_references_unresolved", "$.connections", "Other maps, warp indices, graphics, constants and flags require pinned-catalog resolution."),
             new("source_identity_external", "$.sourceRef", "A matching input hash is not proof of upstream provenance; verify it against source-lock/tree metadata.")]);
    }

    public static string ToCanonicalJson(PretMapStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        // Revalidate even if a caller constructed or modified the public record manually.
        var input = JsonSerializer.Serialize(structure.Map, ContentJson.CreateOptions(JsonNamingPolicy.SnakeCaseLower));
        // Original source hash is for the original input bytes, not a re-serialized map.
        var originalSourceErrors = ContentValidator.ValidateSource(structure.SourceRef, "$.sourceRef");
        if (originalSourceErrors.Count != 0) throw new ContentValidationException(originalSourceErrors);
        if (structure.ReferencedScriptSymbols.IsDefault || structure.Limitations.IsDefault)
            throw ContentJson.Error("structure_claim", "$", "Explicit structure metadata arrays are required.");
        var source = structure.SourceRef with { Sha256 = null };
        if (source.Kind == SourceKind.Pret)
            source = source with { Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))) };
        var validated = ImportStructure(input, source);
        if (structure.IsPlayable || structure.ScriptConversionAttempted || structure.LayoutResolved || structure.SchemaVersion != 1 ||
            !structure.ReferencedScriptSymbols.SequenceEqual(validated.ReferencedScriptSymbols) || !structure.Limitations.SequenceEqual(validated.Limitations))
            throw ContentJson.Error("structure_claim", "$", "Structure-only import metadata has been changed or misrepresented.");
        var sourceErrors = ContentValidator.ValidateSource(structure.SourceRef, "$.sourceRef");
        if (sourceErrors.Count != 0) throw new ContentValidationException(sourceErrors);
        return ContentJson.Canonicalize(JsonSerializer.SerializeToElement(structure, ContentJson.CreateOptions()));
    }
}
