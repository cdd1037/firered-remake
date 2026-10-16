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

sealed class Catalog(params MoveDefinition[] moves) : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => moves.FirstOrDefault(x => x.Id == id);
}

sealed class CaveWarpMap(bool destination, int floorElevation, int doorElevation) : IFieldMap
{
    public MapId Id => new(destination ? "fixture/arrival" : "fixture/map");
    public int Width => 3;
    public int Height => 3;
    public ImmutableArray<FieldWarp> FieldWarps => [new(0, 1, 0, 0, new(destination ? "fixture/map" : "fixture/arrival"), 0)];

    public int? SourceBehaviorAt(int x, int y) => !destination && x == 1 && y == 0 ? 0x60 : 0;
    public TileCell? GetTile(int x, int y) => x is < 0 or >= 3 || y is < 0 or >= 3 ? null : !destination && x == 1 && y == 0 ? new(false, doorElevation, TileBehavior.Warp) : new(false, floorElevation);
}
