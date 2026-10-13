using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

var root = args.Length > 0 ? args[0] : "tests/fixtures/content";
var original = File.ReadAllText(Path.Combine(root, "original-two-rooms.json"));
var sourceShape = File.ReadAllText(Path.Combine(root, "original-pret-map-shape.json"));
var sourceRef = new SourceRef(SourceKind.OriginalFixture, "tests/fixtures/content/original-pret-map-shape.json", "MAP_ENGINEERING_ROOM");
var tests = new List<(string, Action)>();
void Check(bool value, string message) { if (!value) throw new Exception(message); }
void Reject(string id, Action action, string? code = null) => tests.Add((id, () =>
{
    try { action(); } catch (ContentValidationException exception)
    { Check(code is null || exception.Diagnostics.Any(d => d.Code == code), "Unexpected diagnostic: " + exception.Message); return; }
    throw new Exception("Invalid content was accepted.");
}));
string Mutate(Action<JsonNode> action) { var node = JsonNode.Parse(original)!; action(node); return node.ToJsonString(); }
string SourceMutate(Action<JsonNode> action) { var node = JsonNode.Parse(sourceShape)!; action(node); return node.ToJsonString(); }
tests.Add(("content.original_fixture", () => { var b = ContentJson.ParseAndValidate(original); Check(b.Maps.Length == 2 && b.IsOriginalFixture && b.Rewards.Length == 1, "Fixture changed."); }));
tests.Add(("content.canonical_roundtrip", () => { var a = ContentJson.ParseAndValidate(original); var canonical = ContentJson.ToCanonicalJson(a); Check(canonical == ContentJson.ToCanonicalJson(ContentJson.ParseAndValidate(canonical)), "Canonical output drifted."); }));
tests.Add(("content.catalog_order_invariant", () => { var a = ContentJson.ParseAndValidate(original); var b = a with { Maps = a.Maps.Reverse().ToImmutableArray(), Texts = a.Texts.Reverse().ToImmutableArray(), Scripts = a.Scripts.Reverse().ToImmutableArray() }; Check(ContentJson.Sha256(a) == ContentJson.Sha256(b), "Catalog ordering changed hash."); }));
tests.Add(("content.cell_order_semantic", () => { var a = ContentJson.ParseAndValidate(original); var cells = a.Maps[0].Tiles.SetItem(6, "tile.wall"); var b = a with { Maps = a.Maps.SetItem(0, a.Maps[0] with { Tiles = cells }) }; Check(ContentJson.Sha256(a) != ContentJson.Sha256(b), "Changed grid lost meaning."); }));
Reject("content.unknown_root_field", () => ContentJson.ParseAndValidate(Mutate(n => n["surprise"] = 1)), "json_schema");
Reject("content.missing_required_field", () => ContentJson.ParseAndValidate(Mutate(n => n.AsObject().Remove("maps"))), "json_schema");
Reject("content.duplicate_json_property", () => ContentJson.ParseAndValidate(original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")), "duplicate_property");
Reject("content.duplicate_id", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![1]!["stableId"] = "map.workshop")), "duplicate_id");
Reject("content.null_definition", () => ContentJson.ParseAndValidate(Mutate(n => n["texts"]![0] = null)));
Reject("content.null_source", () => ContentJson.ParseAndValidate(Mutate(n => n["sourceRef"] = null)));
Reject("content.source_traversal", () => ContentJson.ParseAndValidate(Mutate(n => n["sourceRef"]!["path"] = "../outside")), "source_ref");
Reject("content.false_game_label", () => ContentJson.ParseAndValidate(Mutate(n => n["isOriginalFixture"] = false)), "source_scope");
Reject("content.unknown_opcode", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["op"] = "teleportAnything")), "json_schema");
Reject("content.opcode_extra_field", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["surprise"] = true)), "json_schema");
Reject("content.unknown_enum", () => ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "lava")), "json_schema");
Reject("content.integer_enum", () => ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = 0)), "json_schema");
Reject("content.tile_count", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["width"] = 6)), "tile_count");
Reject("content.dangling_tile", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["tiles"]![6] = "tile.missing")), "dangling_reference");
Reject("content.dangling_npc_script", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["npcs"]![0]!["scriptId"] = "script.missing")), "dangling_reference");
Reject("content.npc_bounds", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["npcs"]![0]!["x"] = -1)), "npc_bounds");
Reject("content.warp_destination", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["warps"]![0]!["destinationMapId"] = "map.missing")), "dangling_reference");
Reject("content.warp_blocked_destination", () => ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["warps"]![0]!["destinationX"] = 4)), "warp_destination_blocked");
Reject("content.reward_quantity", () => ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0]!["quantity"] = 0)), "reward_quantity");
Reject("content.reward_claim_reference", () => ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["claimFlagId"] = "flag.missing")), "dangling_reference");
Reject("content.script_label_target", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["ifFalse"] = "missing")), "script_target");
Reject("content.script_no_exit", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"] = JsonNode.Parse("[{\"op\":\"jump\",\"label\":\"loop\",\"target\":\"loop\"}]"))), "script_no_exit");
Reject("content.script_fallthrough", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"]!.AsArray().RemoveAt(1))), "script_fallthrough");
Reject("content.future_schema", () => ContentJson.ParseAndValidate(Mutate(n => n["schemaVersion"] = 2)), "schema_version");
tests.Add(("pret.structure_only", () => { var result = PretMapImporter.ImportStructure(sourceShape, sourceRef); Check(!result.IsPlayable && !result.LayoutResolved && !result.ScriptConversionAttempted && result.ReferencedScriptSymbols.Length == 3 && result.Limitations.Length == 4, "Importer overclaimed conversion."); }));
tests.Add(("pret.canonical_deterministic", () => { var one = PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef)); var two = PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef)); Check(one == two, "Structural serialization drifted."); }));
Reject("pret.unknown_field", () => PretMapImporter.ImportStructure(SourceMutate(n => n["shared_events_map"] = "Other"), sourceRef), "json_schema");
Reject("pret.unknown_event_field", () => PretMapImporter.ImportStructure(SourceMutate(n => n["bg_events"]![0]!["item"] = "ITEM_NONE"), sourceRef), "json_schema");
Reject("pret.unsupported_bg_union", () => PretMapImporter.ImportStructure(SourceMutate(n => n["bg_events"]![0]!["type"] = "hidden_item"), sourceRef), "unsupported_bg_type");
Reject("pret.dynamic_warp", () => PretMapImporter.ImportStructure(SourceMutate(n => n["warp_events"]![0]!["dest_warp_id"] = "-1"), sourceRef), "unsupported_warp_target");
Reject("pret.source_hash", () => PretMapImporter.ImportStructure(sourceShape, sourceRef with { Sha256 = new string('0', 64) }), "source_hash_mismatch");
Reject("pret.false_playability", () => PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef) with { IsPlayable = true }), "structure_claim");
Reject("pret.wrong_commit", () => PretMapImporter.ImportStructure(sourceShape, sourceRef with { Kind = SourceKind.Pret, Repository = "pret/pokefirered", Commit = new string('0', 40), Sha256 = new string('0', 64) }), "source_ref");
Reject("content.enum_composite", () => ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "floor,wall")), "json_schema");
Reject("content.enum_case_alias", () => ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "Wall")), "json_schema");
Reject("content.dangling_text", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"]![0]!["textId"] = "text.missing")), "dangling_reference");
Reject("content.dangling_species_name", () => ContentJson.ParseAndValidate(Mutate(n => n["species"]![0]!["nameTextId"] = "text.missing")), "dangling_reference");
Reject("content.dangling_item_name", () => ContentJson.ParseAndValidate(Mutate(n => n["items"]![0]!["nameTextId"] = "text.missing")), "dangling_reference");
Reject("content.dangling_reward_item", () => ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0]!["itemId"] = "item.missing")), "dangling_reference");
Reject("content.dangling_script_reward", () => ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![2]!["rewardId"] = "reward.missing")), "dangling_reference");
Reject("content.map_dimension_overflow", () => ContentJson.ParseAndValidate(Mutate(n => { n["maps"]![0]!["width"] = int.MaxValue; n["maps"]![0]!["height"] = int.MaxValue; })), "map_dimensions");
Reject("content.duplicate_global_id", () => ContentJson.ParseAndValidate(Mutate(n => n["texts"]![0]!["stableId"] = "map.workshop")), "duplicate_id");
Reject("content.reward_null_grant", () => ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0] = null)), "null_grant");
Reject("pret.duplicate_property", () => PretMapImporter.ImportStructure(sourceShape.Replace("\"id\": \"MAP_ENGINEERING_ROOM\"", "\"id\": \"MAP_ENGINEERING_ROOM\", \"id\": \"MAP_ENGINEERING_ROOM\""), sourceRef), "duplicate_property");
Reject("pret.scalar_expression", () => PretMapImporter.ImportStructure(SourceMutate(n => n["object_events"]![0]!["flag"] = "FLAG_A + 1"), sourceRef), "unsupported_scalar");
var results = tests.Select(t => { try { t.Item2(); return new Result(t.Item1, true, null); } catch (Exception e) { return new Result(t.Item1, false, e.ToString()); } }).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { suite = "content-engineering-fixtures", count = results.Length, passed = results.Count(r => r.Passed), failed = results.Count(r => !r.Passed), gameContentCoverageClaimed = false, results }, new JsonSerializerOptions { WriteIndented = true }));
return results.Length > 0 && results.All(r => r.Passed) ? 0 : 1;
record Result(string Id, bool Passed, string? Error);
