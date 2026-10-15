using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Content;
[TestClass]
public sealed class PretMapImportTests : ContentFixture
{

    [TestMethod]
    public void pret_structure_only()
    {
        var result = PretMapImporter.ImportStructure(sourceShape, sourceRef);
        Assert.IsTrue(!result.IsPlayable && !result.LayoutResolved && !result.ScriptConversionAttempted && result.ReferencedScriptSymbols.Length == 3 && result.Limitations.Length == 4, "Importer overclaimed conversion.");
    }

    [TestMethod]
    public void pret_canonical_deterministic()
    {
        var one = PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef));
        var two = PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef));
        Assert.IsTrue(one == two, "Structural serialization drifted.");
    }

    [TestMethod]
    public void pret_unknown_field()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(SourceMutate(n => n["shared_events_map"] = "Other"), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void pret_unknown_event_field()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(SourceMutate(n => n["bg_events"]![0]!["item"] = "ITEM_NONE"), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "json_schema"), exception.Message);
    }

    [TestMethod]
    public void pret_unsupported_bg_union()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(SourceMutate(n => n["bg_events"]![0]!["type"] = "hidden_item"), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "unsupported_bg_type"), exception.Message);
    }

    [TestMethod]
    public void pret_dynamic_warp()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(SourceMutate(n => n["warp_events"]![0]!["dest_warp_id"] = "-1"), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "unsupported_warp_target"), exception.Message);
    }

    [TestMethod]
    public void pret_false_playability()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ToCanonicalJson(PretMapImporter.ImportStructure(sourceShape, sourceRef)with { IsPlayable = true });
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "structure_claim"), exception.Message);
    }

    [TestMethod]
    public void pret_mismatched_source_path()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(sourceShape, sourceRef with { Kind = SourceKind.Pret });
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "source_identity"), exception.Message);
    }

    [TestMethod]
    public void pret_duplicate_property()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(sourceShape.Replace("\"id\": \"MAP_ENGINEERING_ROOM\"", "\"id\": \"MAP_ENGINEERING_ROOM\", \"id\": \"MAP_ENGINEERING_ROOM\""), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "duplicate_property"), exception.Message);
    }

    [TestMethod]
    public void pret_scalar_expression()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
        {
            PretMapImporter.ImportStructure(SourceMutate(n => n["object_events"]![0]!["flag"] = "FLAG_A + 1"), sourceRef);
        });
        Assert.IsTrue(exception.Diagnostics.Any(d => d.Code == "unsupported_scalar"), exception.Message);
    }
}
