using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Content;
[TestClass]
public sealed class ContentValidationTests : ContentFixture
{

    [TestMethod]
    public void content_original_fixture()
    {
        var b = ContentJson.ParseAndValidate(original);
        Assert.IsTrue(b.Maps.Length == 2 && b.IsOriginalFixture && b.Rewards.Length == 1, "Fixture changed.");
    }

    [TestMethod]
    public void content_canonical_roundtrip()
    {
        var a = ContentJson.ParseAndValidate(original);
        var canonical = ContentJson.ToCanonicalJson(a);
        Assert.IsTrue(canonical == ContentJson.ToCanonicalJson(ContentJson.ParseAndValidate(canonical)), "Canonical output drifted.");
    }

    [TestMethod]
    public void content_catalog_order_invariant()
    {
        var a = ContentJson.ParseAndValidate(original);
        var b = a with
        {
            Maps = a.Maps.Reverse().ToImmutableArray(),
            Texts = a.Texts.Reverse().ToImmutableArray(),
            Scripts = a.Scripts.Reverse().ToImmutableArray()
        };
        Assert.IsTrue(ContentJson.Sha256(a) == ContentJson.Sha256(b), "Catalog ordering changed hash.");
    }

    [TestMethod]
    public void content_cell_order_semantic()
    {
        var a = ContentJson.ParseAndValidate(original);
        var cells = a.Maps[0].Tiles.SetItem(6, "tile.wall");
        var b = a with
        {
            Maps = a.Maps.SetItem(0, a.Maps[0] with { Tiles = cells })
        };
        Assert.IsTrue(ContentJson.Sha256(a) != ContentJson.Sha256(b), "Changed grid lost meaning.");
    }

    [TestMethod]
    public void content_unknown_root_field()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["surprise"] = 1));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_missing_required_field()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n.AsObject().Remove("maps")));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_duplicate_json_property()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "duplicate_property"), exception.Message);
    }

    [TestMethod]
    public void content_duplicate_id()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![1]!["stableId"] = "map.workshop"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "duplicate_id"), exception.Message);
    }

    [TestMethod]
    public void content_null_definition()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["texts"]![0] = null));
        });
    }

    [TestMethod]
    public void content_null_source()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["sourceRef"] = null));
        });
    }

    [TestMethod]
    public void content_source_traversal()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["sourceRef"]!["path"] = "../outside"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "source_ref"), exception.Message);
    }

    [TestMethod]
    public void content_false_game_label()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["isOriginalFixture"] = false));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "source_scope"), exception.Message);
    }

    [TestMethod]
    public void content_unknown_opcode()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["op"] = "teleportAnything"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_opcode_extra_field()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["surprise"] = true));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_unknown_enum()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "lava"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_integer_enum()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = 0));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_tile_count()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["width"] = 6));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "tile_count"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_tile()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["tiles"]![6] = "tile.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_npc_script()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["npcs"]![0]!["scriptId"] = "script.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_npc_bounds()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["npcs"]![0]!["x"] = -1));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "npc_bounds"), exception.Message);
    }

    [TestMethod]
    public void content_warp_destination()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["warps"]![0]!["destinationMapId"] = "map.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_warp_blocked_destination()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["maps"]![0]!["warps"]![0]!["destinationX"] = 4));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "warp_destination_blocked"), exception.Message);
    }

    [TestMethod]
    public void content_reward_quantity()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0]!["quantity"] = 0));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "reward_quantity"), exception.Message);
    }

    [TestMethod]
    public void content_reward_claim_reference()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["claimFlagId"] = "flag.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_script_label_target()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![0]!["ifFalse"] = "missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "script_target"), exception.Message);
    }

    [TestMethod]
    public void content_script_no_exit()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"] = JsonNode.Parse("[{\"op\":\"jump\",\"label\":\"loop\",\"target\":\"loop\"}]")));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "script_no_exit"), exception.Message);
    }

    [TestMethod]
    public void content_script_fallthrough()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"]!.AsArray().RemoveAt(1)));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "script_fallthrough"), exception.Message);
    }

    [TestMethod]
    public void content_future_schema()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["schemaVersion"] = 2));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "schema_version"), exception.Message);
    }

    [TestMethod]
    public void content_enum_composite()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "floor,wall"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_enum_case_alias()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["tiles"]![0]!["kind"] = "Wall"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_text()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![1]!["instructions"]![0]!["textId"] = "text.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_species_name()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["species"]![0]!["nameTextId"] = "text.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_item_name()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["items"]![0]!["nameTextId"] = "text.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_reward_item()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0]!["itemId"] = "item.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_dangling_script_reward()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["scripts"]![0]!["instructions"]![2]!["rewardId"] = "reward.missing"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "dangling_reference"), exception.Message);
    }

    [TestMethod]
    public void content_map_dimension_overflow()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n =>
            {
                n["maps"]![0]!["width"] = int.MaxValue;
                n["maps"]![0]!["height"] = int.MaxValue;
            }));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "map_dimensions"), exception.Message);
    }

    [TestMethod]
    public void content_duplicate_global_id()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["texts"]![0]!["stableId"] = "map.workshop"));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "duplicate_id"), exception.Message);
    }

    [TestMethod]
    public void content_reward_null_grant()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            ContentJson.ParseAndValidate(Mutate(n => n["rewards"]![0]!["items"]![0] = null));
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "null_grant"), exception.Message);
    }
}
