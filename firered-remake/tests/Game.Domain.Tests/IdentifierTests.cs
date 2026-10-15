using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Battle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Domain;
[TestClass, TestCategory("Game")]
public sealed class IdentifierTests : DomainFixture
{
    [TestMethod]
    public void ids_stable_json_roundtrip()
    {
        var id = new SpeciesId("species/bulbasaur");
        Assert.AreEqual(id, JsonSerializer.Deserialize<SpeciesId>(Json(id)));
        Assert.AreEqual("species/bulbasaur", id.ToString());
    }

    [TestMethod]
    public void ids_reject_invalid()
    {
        foreach (var v in new[]
        {
            "",
            " ",
            "hello world",
            "皮卡丘",
            "x?y"
        }

        )
            Assert.Throws<ArgumentException>(() => new MapId(v));
        Assert.IsTrue(!StableId.IsValid(default(MapId).Value));
    }
}
