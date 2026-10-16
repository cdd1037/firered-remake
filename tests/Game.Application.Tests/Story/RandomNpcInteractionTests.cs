using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Application.Story;

[TestClass, TestCategory("Game")]
public sealed class RandomNpcInteractionTests : WildBattleFixture
{
    [TestMethod]
    [DataRow("CeruleanCity_EventScript_Lass", 3)]
    [DataRow("CeruleanCity_EventScript_Slowbro", 4)]
    [DataRow("SSAnne_Kitchen_EventScript_Chef4", 3)]
    public void PublicInteractionCompletesSourceNpcWithOneAuthoritativeDraw(string program, int maximum)
    {
        var state = FieldFixture();
        var map = content.Maps.Values.Single(m => content.MapDefinition(m.Id).Actors.Any(a => a.ScriptId == program));
        var definition = content.MapDefinition(map.Id);
        var actor = definition.Actors.Single(a => a.ScriptId == program);
        var spot = new[] { (X:actor.X,Y:actor.Y+1,Facing:Direction.North),
            (X:actor.X,Y:actor.Y-1,Facing:Direction.South),
            (X:actor.X-1,Y:actor.Y,Facing:Direction.East),
            (X:actor.X+1,Y:actor.Y,Facing:Direction.West) }
            .First(p => map.GetTile(p.X,p.Y) is { Blocked:false } && !definition.Actors.Any(a => a.X==p.X && a.Y==p.Y));
        state = state with {
            World = state.World with { Player = new(map.Id, spot.X, spot.Y, map.GetTile(spot.X,spot.Y)!.Elevation,spot.Facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,Direction.South,
                    a.MovementType,a.ScriptId,a.HideFlag,!state.Script.Flags.GetValueOrDefault(a.HideFlag),false,a.X,a.Y),StringComparer.Ordinal),
            Script = StoryState.Empty with { Flags=state.Script.Flags,Variables=state.Script.Variables,EffectSequence=state.Script.EffectSequence },
            ObjectSlots=null, LastTalked=null, SelectedObjectSlot=0, MapLayout=null, MapEdits=null,
            EntryPrograms=[], Dialogue=null, Motions=[], IsSurfing=false, IsCycling=false, FlashLevel=0
        };
        // The representative checkpoint is after departure. Place this controlled fixture
        // before departure while retaining its actual HM/rival ownership and history.
        if (map.Id.Value.StartsWith("map.ssanne-", StringComparison.Ordinal))
            state = state with { Script = state.Script with {
                Flags = state.Script.Flags.Remove("FLAG_HIDE_SS_ANNE").SetItem("FLAG_GOT_SS_TICKET",true),
                Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_VERMILION_CITY",
                    state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM01") ? 1 : 0) } };
        state = CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var session = CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);
        var expected = Gen3Rng.Next(state.World.Rng,"story.random",maximum);
        Send(session,new("interact"));
        int acknowledgements=0;
        while (!session.State.Script.Completed || session.State.Dialogue is not null)
        {
            Assert.IsTrue(++acknowledgements<100,"NPC event must finish");
            Send(session,new("ack"));
        }
        Assert.AreEqual(expected.State,session.State.World.Rng);
        Assert.AreEqual(expected.Trace.Result,session.State.Script.Variables["VAR_0x8008"]);
        Assert.IsFalse(session.State.Script.Locked);
    }
}
