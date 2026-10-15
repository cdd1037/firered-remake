using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Battle;
using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.Protocols;
[TestClass, TestCategory("Game")]
public sealed class CanonicalSerializationTests : ProtocolFixture
{

    [TestMethod]
    public void canonical_dictionary_order()
    {
        Assert.IsTrue(CanonicalJson.Hash(new Dictionary<string, int> { { "b", 2 }, { "a", 1 } }) == CanonicalJson.Hash(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }), "object order changed hash");
    }

    [TestMethod]
    public void canonical_array_order()
    {
        Assert.IsTrue(CanonicalJson.Hash(new[] { 1, 2 }) != CanonicalJson.Hash(new[] { 2, 1 }), "array semantic order lost");
    }
}
