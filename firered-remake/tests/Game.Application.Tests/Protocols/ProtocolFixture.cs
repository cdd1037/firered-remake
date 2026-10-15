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
[TestClass]
public abstract class ProtocolFixture
{
    private protected void Throws(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new Exception("expected InvalidDataException");
    }
}
