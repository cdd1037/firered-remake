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
sealed class MemoryStorage : ISaveStorage
{
    public readonly Dictionary<string, byte[]> Files = new();
    public bool FailCommit;
    public bool CorruptStage;
    public byte[]? Read(string slot) => Files.GetValueOrDefault(slot);
    public void WriteStaged(string slot, byte[] bytes) => Files[slot + ".tmp"] = CorruptStage ? SaveCodec.Encode(new Counter(0), 0, "wrong", "wrong") : bytes;
    public void Commit(string slot, bool preservePrimary)
    {
        if (FailCommit)
            throw new IOException("injected_commit_failure");
        if (preservePrimary)
            Files[slot + ".bak"] = Files[slot];
        Files[slot] = Files[slot + ".tmp"];
        Files.Remove(slot + ".tmp");
    }
}

sealed class EmptyBattleContent : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => null;
}

sealed class IntegrationBattleContent : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => id.Value == "move:test" ? new(id, Game.Domain.Rules.ElementType.Normal, 40, 100, 30) : null;
}

sealed record Counter(int Value);
sealed record IdHolder(MapId Map);
