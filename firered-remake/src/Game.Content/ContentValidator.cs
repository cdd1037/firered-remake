using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;

namespace Game.Content;

public static class ContentValidator
{
    public static void ThrowIfInvalid(ContentBundle bundle)
    {
        var diagnostics = Validate(bundle);
        if (diagnostics.Count != 0) throw new ContentValidationException(diagnostics);
    }

    public static IReadOnlyList<ContentDiagnostic> Validate(ContentBundle bundle)
    {
        var errors = new List<ContentDiagnostic>();
        void Error(string code, string path, string message) => errors.Add(new(code, path, message));
        if (bundle is null) return [new("bundle_null", "$", "A bundle is required.")];
        if (bundle.SchemaVersion != 1) Error("schema_version", "$.schemaVersion", "Only engineering schema 1 is supported.");
        if (!StableId.IsValid(bundle.RulesVersion)) Error("rules_version", "$.rulesVersion", "A stable rules version is required.");
        var globalIds = new HashSet<string>(StringComparer.Ordinal);
        void Definition(IContentDefinition? definition, string path)
        {
            if (definition is null) { Error("null_definition", path, "Null definitions are forbidden."); return; }
            if (!StableId.IsValid(definition.StableId)) Error("stable_id", path + ".stableId", "Invalid stable ID.");
            else if (!globalIds.Add(definition.StableId)) Error("duplicate_id", path + ".stableId", $"Duplicate ID '{definition.StableId}'.");
            errors.AddRange(ValidateSource(definition.SourceRef, path + ".sourceRef"));
            if (definition.SourceRef is not null &&
                bundle.IsOriginalFixture != (definition.SourceRef.Kind == SourceKind.OriginalFixture))
                Error("source_scope", path + ".sourceRef", "Original fixtures and reference-game content must not be mislabeled or mixed in schema 1.");
        }
        Definition(bundle, "$");
        Dictionary<string, T> Catalog<T>(ImmutableArray<T> values, string name) where T : class, IContentDefinition
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (values.IsDefault) { Error("missing_array", "$.", $"{name} must be an explicit array."); return result; }
            for (var i = 0; i < values.Length; i++)
            {
                var item = values[i]; Definition(item, $"$.{name}[{i}]");
                if (item is not null && StableId.IsValid(item.StableId)) result.TryAdd(item.StableId, item);
            }
            return result;
        }
        var tiles = Catalog(bundle.Tiles, "tiles"); var maps = Catalog(bundle.Maps, "maps");
        var scripts = Catalog(bundle.Scripts, "scripts"); var texts = Catalog(bundle.Texts, "texts");
        var flags = Catalog(bundle.Flags, "flags"); var items = Catalog(bundle.Items, "items");
        var species = Catalog(bundle.Species, "species"); var rewards = Catalog(bundle.Rewards, "rewards");
        if (maps.Count == 0) Error("maps_empty", "$.maps", "An engineering world bundle must contain at least one map.");
        bool Reference<T>(string? id, IReadOnlyDictionary<string, T> catalog, string path)
        {
            if (id is not null && catalog.ContainsKey(id)) return true;
            Error("dangling_reference", path, $"Unresolved reference '{id ?? "<null>"}'."); return false;
        }
        foreach (var tile in tiles.Values)
        {
            if (!Enum.IsDefined(tile.Kind)) Error("tile_kind", tile.StableId, "Unsupported tile kind.");
            if (tile.Elevation is < 0 or > 15) Error("tile_elevation", tile.StableId, "Elevation must be 0–15.");
            if (tile.Kind == ContentTileKind.Wall && !tile.Blocked) Error("wall_collision", tile.StableId, "A wall must be blocked.");
        }
        foreach (var text in texts.Values)
        {
            if (string.IsNullOrWhiteSpace(text.Language)) Error("text_language", text.StableId, "A language tag is required.");
            if (string.IsNullOrEmpty(text.Value) || text.Value.Length > 16384)
                Error("text_value", text.StableId, "Text must contain 1–16384 characters.");
        }
        foreach (var item in items.Values)
        {
            Reference(item.NameTextId, texts, item.StableId + ".nameTextId");
            if (item.StackLimit is < 1 or > 9999) Error("item_stack", item.StableId, "Stack limit must be 1–9999 in schema 1.");
        }
        foreach (var mon in species.Values)
        {
            Reference(mon.NameTextId, texts, mon.StableId + ".nameTextId");
            if (new[] {mon.BaseHp, mon.BaseAttack, mon.BaseDefense, mon.BaseSpeed, mon.BaseSpecialAttack, mon.BaseSpecialDefense}.Any(n => n is < 1 or > 255))
                Error("species_stats", mon.StableId, "Each base stat must be 1–255. This record is not a full species implementation.");
        }
        var claimFlags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reward in rewards.Values)
        {
            Reference(reward.ClaimFlagId, flags, reward.StableId + ".claimFlagId");
            if (reward.ClaimFlagId is not null && !claimFlags.Add(reward.ClaimFlagId)) Error("reward_claim_conflict", reward.StableId, "Rewards must not share a claim flag.");
            if (reward.Money is < 0 or > 999999) Error("reward_money", reward.StableId, "Money must be 0–999999.");
            if (reward.Items.IsDefault) { Error("missing_array", reward.StableId + ".items", "Explicit items array required."); continue; }
            if (reward.Money == 0 && reward.Items.IsEmpty) Error("reward_empty", reward.StableId, "Reward must contain something.");
            var granted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grant in reward.Items)
            {
                if (grant is null) { Error("null_grant", reward.StableId, "Null item grant."); continue; }
                var exists = Reference(grant.ItemId, items, reward.StableId + ".items");
                if (grant.ItemId is not null && !granted.Add(grant.ItemId)) Error("duplicate_grant", reward.StableId, "Combine duplicate item grants explicitly.");
                if (grant.Quantity < 1 || (exists && grant.Quantity > items[grant.ItemId!].StackLimit))
                    Error("reward_quantity", reward.StableId, "Grant must fit a positive single stack in schema 1.");
            }
        }
        bool InBounds(MapDefinition map, int x, int y) => x >= 0 && y >= 0 && x < map.Width && y < map.Height;
        bool Blocked(MapDefinition map, int x, int y)
        {
            if (!InBounds(map, x, y) || map.Tiles.IsDefault) return true;
            var index = (long)y * map.Width + x;
            return index >= map.Tiles.Length || map.Tiles[(int)index] is not { } id || !tiles.TryGetValue(id, out var tile) || tile.Blocked;
        }
        foreach (var map in maps.Values)
        {
            var path = map.StableId;
            if (map.Width is < 1 or > 256 || map.Height is < 1 or > 256)
                Error("map_dimensions", path, "Engineering schema dimensions must be 1–256.");
            if (map.Tiles.IsDefault || (long)map.Width * map.Height != map.Tiles.Length)
                Error("tile_count", path + ".tiles", "Row-major tile count must equal width × height.");
            if (!map.Tiles.IsDefault)
                for (var i = 0; i < map.Tiles.Length; i++) Reference(map.Tiles[i], tiles, path + $".tiles[{i}]");
            var warps = Catalog(map.Warps, path + ".warps"); var npcs = Catalog(map.Npcs, path + ".npcs");
            var occupied = new HashSet<(int, int)>();
            foreach (var warp in warps.Values)
            {
                if (!InBounds(map, warp.X, warp.Y)) Error("warp_bounds", warp.StableId, "Warp is outside its map.");
                else if (Blocked(map, warp.X, warp.Y)) Error("warp_blocked", warp.StableId, "Warp entrance must be walkable.");
                if (!occupied.Add((warp.X, warp.Y))) Error("object_overlap", warp.StableId, "Warp/NPC overlap is not supported in schema 1.");
                if (Reference(warp.DestinationMapId, maps, warp.StableId + ".destinationMapId"))
                {
                    var target = maps[warp.DestinationMapId];
                    if (!InBounds(target, warp.DestinationX, warp.DestinationY)) Error("warp_destination_bounds", warp.StableId, "Warp destination is outside its map.");
                    else if (Blocked(target, warp.DestinationX, warp.DestinationY)) Error("warp_destination_blocked", warp.StableId, "Warp destination must be walkable.");
                    if (!target.Npcs.IsDefault && target.Npcs.Any(n => n is not null && n.X == warp.DestinationX && n.Y == warp.DestinationY))
                        Error("warp_destination_occupied", warp.StableId, "Warp cannot place the player on an NPC.");
                }
            }
            foreach (var npc in npcs.Values)
            {
                if (!InBounds(map, npc.X, npc.Y)) Error("npc_bounds", npc.StableId, "NPC is outside its map.");
                else if (Blocked(map, npc.X, npc.Y)) Error("npc_blocked", npc.StableId, "NPC must stand on a walkable tile.");
                if (!occupied.Add((npc.X, npc.Y))) Error("object_overlap", npc.StableId, "Warp/NPC overlap is not supported in schema 1.");
                Reference(npc.ScriptId, scripts, npc.StableId + ".scriptId");
            }
        }
        foreach (var script in scripts.Values)
        {
            if (script.Instructions.IsDefaultOrEmpty || script.Instructions.Length > 4096)
            { Error("script_size", script.StableId, "Script requires 1–4096 instructions."); continue; }
            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < script.Instructions.Length; i++)
            {
                var instruction = script.Instructions[i];
                if (instruction is null) { Error("null_instruction", script.StableId, "Null instructions are forbidden."); continue; }
                if (!StableId.IsValid(instruction.Label)) Error("script_label", script.StableId, "Invalid instruction label.");
                else if (!labels.TryAdd(instruction.Label, i)) Error("duplicate_label", script.StableId, $"Duplicate label '{instruction.Label}'.");
            }
            var edges = Enumerable.Range(0, script.Instructions.Length).Select(_ => new List<int>()).ToArray();
            for (var i = 0; i < script.Instructions.Length; i++)
            {
                void Target(string? label)
                {
                    if (label is not null && labels.TryGetValue(label, out var target)) edges[i].Add(target);
                    else Error("script_target", script.StableId, $"Unknown target label '{label}'.");
                }
                var instruction = script.Instructions[i]; var path = script.StableId + $".instructions[{i}]";
                switch (instruction)
                {
                    case SayInstruction say: Reference(say.TextId, texts, path); break;
                    case GiveRewardInstruction give: Reference(give.RewardId, rewards, path); break;
                    case SetFlagInstruction set: Reference(set.FlagId, flags, path); break;
                    case BranchFlagInstruction branch:
                        Reference(branch.FlagId, flags, path); Target(branch.IfTrue); Target(branch.IfFalse); break;
                    case JumpInstruction jump: Target(jump.Target); break;
                    case EndInstruction: break;
                    default: Error("unknown_opcode", path, "Unsupported instruction type."); break;
                }
                if (instruction is SayInstruction or GiveRewardInstruction or SetFlagInstruction)
                {
                    if (i + 1 < script.Instructions.Length) edges[i].Add(i + 1);
                    else Error("script_fallthrough", path, "Script falls off the end without an end instruction.");
                }
            }
            var reachable = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(0);
            while (pending.TryPop(out var index)) if (reachable.Add(index)) foreach (var next in edges[index]) pending.Push(next);
            var reverse = Enumerable.Range(0, edges.Length).Select(_ => new List<int>()).ToArray();
            for (var i = 0; i < edges.Length; i++) foreach (var next in edges[i]) reverse[next].Add(i);
            var canEnd = new HashSet<int>();
            for (var i = 0; i < edges.Length; i++) if (script.Instructions[i] is EndInstruction) pending.Push(i);
            while (pending.TryPop(out var index)) if (canEnd.Add(index)) foreach (var previous in reverse[index]) pending.Push(previous);
            for (var i = 0; i < edges.Length; i++)
            {
                if (!reachable.Contains(i)) Error("unreachable_instruction", script.StableId + $".instructions[{i}]", "Instruction is unreachable from entry.");
                else if (!canEnd.Contains(i)) Error("script_no_exit", script.StableId + $".instructions[{i}]", "No structural path to an end instruction.");
            }
        }
        return errors.AsReadOnly();
    }

    internal static IReadOnlyList<ContentDiagnostic> ValidateSource(SourceRef? source, string path)
    {
        var errors = new List<ContentDiagnostic>();
        void Error(string message) => errors.Add(new("source_ref", path, message));
        if (source is null) { Error("Source reference is required."); return errors; }
        if (!Enum.IsDefined(source.Kind)) Error("Unknown source kind.");
        if (string.IsNullOrWhiteSpace(source.Path) || source.Path.Length > 512 || source.Path.Contains('\\') ||
            source.Path.Contains(':') || source.Path.Split('/').Any(p => p is "" or "." or ".."))
            Error("Source path must be a safe repository-relative path.");
        if (string.IsNullOrWhiteSpace(source.Symbol)) Error("Source symbol is required.");
        return errors.AsReadOnly();
    }
}
