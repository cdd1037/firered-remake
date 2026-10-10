using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Battle;
using System.Collections.Immutable;
var tests = new List<(string, Action)>();
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Throws(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("expected InvalidDataException"); }
tests.Add(("canonical.dictionary.order", () => Check(CanonicalJson.Hash(new Dictionary<string,int>{{"b",2},{"a",1}})==CanonicalJson.Hash(new Dictionary<string,int>{{"a",1},{"b",2}}), "object order changed hash")));
tests.Add(("canonical.array.order", () => Check(CanonicalJson.Hash(new[]{1,2})!=CanonicalJson.Hash(new[]{2,1}),"array semantic order lost")));
tests.Add(("command.stale.retry.does.not.execute", () => {
 var s=new CommandSession<Counter,int>(new(0),"r1","c1");var c=new CommandEnvelope<int>("one",0,"r1","c1",5);
 var first=s.Execute(c,(x,n)=>new(CommandStatus.Accepted,x with {Value=x.Value+n}));
 var again=s.Execute(c,(_,_)=>throw new Exception("duplicate executed"));
 Check(first.State.Value==5&&again.Status==CommandStatus.Rejected&&again.Error=="stale_revision"&&ReferenceEquals(again.State,s.State)&&again.StateHash==first.StateHash&&s.Revision==1,"duplicated state change");
 Check(s.Execute(c with {Payload=6},(x,n)=>new(CommandStatus.Accepted,x)).Error=="stale_revision","old revision with changed payload accepted");
 var next=s.Execute(c with {ExpectedRevision=1,Payload=6},(x,n)=>new(CommandStatus.Accepted,x with {Value=x.Value+n}));
 Check(next.Status==CommandStatus.Accepted&&next.State.Value==11&&s.Revision==2,"trace ID incorrectly blocked a new current-revision request");
}));
tests.Add(("command.reject.atomic", () => {
 var s=new CommandSession<Counter,int>(new(2),"r1","c1");
 var a=s.Execute(new("a",0,"r1","c1",1),(x,_)=>new(CommandStatus.Rejected,new(999),"no"));
 Check(a.State.Value==2&&s.Revision==0,"rejection mutated state");
 var b=s.Execute(new("b",0,"r1","c1",1),(_,_)=>throw new InvalidOperationException("invariant"));
 Check(b.Status==CommandStatus.Fault&&s.State.Value==2&&s.Revision==0,"fault mutated state");
}));
tests.Add(("command.stale.version", () => {
 var s=new CommandSession<Counter,int>(new(2),"r1","c1");
 Check(s.Execute(new("a",1,"r1","c1",1),(x,_)=>new(CommandStatus.Accepted,x)).Error=="stale_revision","stale accepted");
 Check(s.Execute(new("a",0,"r2","c1",1),(x,_)=>new(CommandStatus.Accepted,x)).Error=="version_mismatch","version accepted");
}));
tests.Add(("save.roundtrip", () => {var b=SaveCodec.Encode(new Counter(8),4,"r1","c1");var a=SaveCodec.Decode<Counter>(b,"r1","c1");Check(a.State.Value==8&&a.Generation==4,"roundtrip mismatch");}));
tests.Add(("save.canonical.polymorphic.metadata.order", () => {
 Game.Domain.WildBattle.WildBattleEvent[] events=[new Game.Domain.WildBattle.WildBattleMedicineAppliedEvent(4,2,new("pret:ITEM_ANTIDOTE"),7,1,false,16,16,8,0)];
 var bytes=SaveCodec.Encode(events,5,"diagnostic","content");
 var text=Encoding.UTF8.GetString(bytes);
 Check(text.IndexOf("\"ActiveTarget\"",StringComparison.Ordinal)<text.IndexOf("\"kind\"",StringComparison.Ordinal),"fixture must exercise canonical metadata ordering");
 var restored=SaveCodec.Decode<Game.Domain.WildBattle.WildBattleEvent[]>(bytes,"diagnostic","content");
 Check(CanonicalJson.Hash(restored.State)==CanonicalJson.Hash(events)&&restored.State[0] is Game.Domain.WildBattle.WildBattleMedicineAppliedEvent,"typed canonical payload did not roundtrip");
 Throws(()=>SaveCodec.Decode<Game.Domain.WildBattle.WildBattleEvent[]>(bytes,"diagnostic","content",validate:_=>"save_battle_active"));
 var fs=new MemoryStorage();Throws(()=>new SaveService(fs).Save("slot",events,5,"diagnostic","content",_=>"save_battle_active"));Check(fs.Files.Count==0,"codec support must not bypass player save policy");
}));
tests.Add(("save.corrupt.future.reject", () => {
 var b=Encoding.UTF8.GetString(SaveCodec.Encode(new Counter(8),4,"r1","c1"));
 Throws(()=>SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b.Replace("\"Value\":8","\"Value\":9")),"r1","c1"));
 Throws(()=>SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b.Replace("\"SchemaVersion\":1","\"SchemaVersion\":2")),"r1","c1"));
 Throws(()=>SaveCodec.Decode<Counter>(Encoding.UTF8.GetBytes(b),"r1","different"));
}));
tests.Add(("save.backup.recover", () => {
 var fs=new MemoryStorage(); var service=new SaveService(fs); service.Save("s",new Counter(1),1,"r","c"); service.Save("s",new Counter(2),2,"r","c");
 fs.Files["s"]=Encoding.UTF8.GetBytes("corrupt"); var restored=service.Load<Counter>("s","r","c"); Check(restored.State.Value==1&&restored.Source=="s.bak","backup failed");
 service.Save("s",new Counter(3),3,"r","c"); Check(SaveCodec.Decode<Counter>(fs.Files["s.bak"],"r","c").State.Value==1,"corrupt primary overwrote backup");
}));
tests.Add(("save.stage.failure.recover", () => {
 var fs=new MemoryStorage(); var service=new SaveService(fs); service.Save("s",new Counter(1),1,"r","c"); fs.FailCommit=true;
 try {service.Save("s",new Counter(2),2,"r","c");}catch(IOException){}
 Check(service.Load<Counter>("s","r","c").State.Value==1,"commit fault lost primary");
 fs.FailCommit=false;fs.CorruptStage=true;Throws(()=>service.Save("s",new Counter(3),3,"r","c"));
 Check(service.Load<Counter>("s","r","c").State.Value==1,"stage fault lost primary");
}));
tests.Add(("save.semantic.corruption.recover", () => {
 var fs=new MemoryStorage();var service=new SaveService(fs);service.Save("s",new Counter(1),1,"r","c");service.Save("s",new Counter(2),2,"r","c");
 fs.Files["s"]=SaveCodec.Encode(new Counter(-99),3,"r","c");
 var restored=service.Load<Counter>("s","r","c",x=>x.Value<0?"negative_counter":null);
 Check(restored.State.Value==1&&restored.Source=="s.bak","semantic corruption did not recover from backup");
}));
tests.Add(("save.missing.payload.recover", () => {
 var fs=new MemoryStorage();fs.Files["s.bak"]=SaveCodec.Encode(new Counter(4),1,"r","c");
 fs.Files["s"]=Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"RulesVersion\":\"r\",\"ContentVersion\":\"c\",\"GameEdition\":\"firered\",\"Generation\":2,\"PayloadHash\":\"missing\"}");
 Check(new SaveService(fs).Load<Counter>("s","r","c").State.Value==4,"missing payload blocked backup");
}));
tests.Add(("save.invalid.id.recover", () => {
 var fs=new MemoryStorage();fs.Files["s.bak"]=SaveCodec.Encode(new IdHolder(new("valid:map")),1,"r","c");
 var payload=JsonSerializer.SerializeToElement(new{Map=new{Value="bad id"}});
 fs.Files["s"]=CanonicalJson.Encode(new SaveEnvelope(1,"r","c","firered",2,CanonicalJson.Hash(payload),payload));
 Check(new SaveService(fs).Load<IdHolder>("s","r","c").State.Map.Value=="valid:map","invalid id blocked backup");
}));
tests.Add(("command.revision.exhaustion", () => {
 var session=new CommandSession<Counter,int>(new(1),"r","c",long.MaxValue);
 var r=session.Execute(new("a",long.MaxValue,"r","c",1),(_,_)=>throw new Exception("must not execute"));
 Check(r.Status==CommandStatus.Fault&&r.Error=="revision_exhausted","overflow escaped transaction");
}));
tests.Add(("harness.command.retry", () => {
 var map=new GridMap(new("fixture:lab"),5,5,Enumerable.Repeat(new TileCell(false),25).ToImmutableArray());
 var session=new EngineeringSession(map,new EmptyBattleContent(),BattleSnapshot.Create(new("b"),EngineeringSession.Content,new(new("p"),new("s"),BattleSide.Player,5,new(20,10,10,10,10,10),Game.Domain.Rules.ElementType.Normal,null,20,[]),new(new("o"),new("s"),BattleSide.Opponent,5,new(20,10,10,10,10,10),Game.Domain.Rules.ElementType.Normal,null,20,[]),1));
 var c=new EngineeringCommand("move",Direction.East);
 var first=session.Execute("same",0,c);var second=session.Execute("same",0,c);
 Check(second.Status==CommandStatus.Rejected&&second.Error=="stale_revision"&&second.StateHash==first.StateHash&&ReferenceEquals(second.State,session.State)&&session.State.World.StepCount==1,"harness stale retry changed state");
}));
tests.Add(("save.concurrent.services.serialize", () => {
 var fs=new MemoryStorage();
 Task.WaitAll(Enumerable.Range(1,32).Select(i=>Task.Run(()=>new SaveService(fs).Save("same",new Counter(i),i,"r","c"))).ToArray());
 var loaded=new SaveService(fs).Load<Counter>("same","r","c");
 Check(loaded.State.Value is >=1 and <=32&&!fs.Files.ContainsKey("same.tmp"),"concurrent writes corrupted save");
 Check(SaveCodec.Decode<Counter>(fs.Files["same.bak"],"r","c").State.Value is >=1 and <=32,"backup corrupted");
}));
tests.Add(("harness.bad.semantic.saves.recover", () => {
 var map=new GridMap(new("fixture:lab"),5,5,Enumerable.Repeat(new TileCell(false),25).ToImmutableArray());var catalog=new IntegrationBattleContent();
 CombatantSnapshot mon(string id,BattleSide side)=>new(new(id),new("species:test"),side,5,new(20,10,10,10,10,10),Game.Domain.Rules.ElementType.Normal,null,20,[new(new("move:test"),30,30)]);
 var battle=BattleSnapshot.Create(new("battle:test"),EngineeringSession.Content,mon("actor:p",BattleSide.Player),mon("actor:o",BattleSide.Opponent),1);
 var state=new EngineeringSession(map,catalog,battle).State;
 var badStates=new[]{
  state with {World=null!},
  state with {World=state.World with{Player=state.World.Player with{Facing=(Direction)999}}},
  state with {World=state.World with{Rng=state.World.Rng with{AlgorithmId="bad-rng"}}},
  state with {Battle=battle with{Battlers=[battle.Battlers[0],battle.Battlers[1] with{Side=BattleSide.Player}]}},
  state with {Battle=battle with{Phase=BattlePhase.Ended,Outcome=BattleOutcome.PlayerWon,Battlers=[battle.Battlers[0],battle.Battlers[1] with{Hp=0}]}},
  state with {Battle=BattleRules.Apply(battle,new(new("pick"),0,battle.Battlers[0].Id,0,battle.Battlers[1].Id),catalog).State}
 };
 foreach(var bad in badStates){var fs=new MemoryStorage();fs.Files["s.bak"]=SaveCodec.Encode(state,1,EngineeringSession.Rules,EngineeringSession.Content);fs.Files["s"]=SaveCodec.Encode(bad,2,EngineeringSession.Rules,EngineeringSession.Content);
 var loaded=new SaveService(fs).Load<EngineeringState>("s",EngineeringSession.Rules,EngineeringSession.Content,x=>EngineeringSession.ValidateState(x,map,catalog));Check(loaded.Source=="s.bak","unsafe harness state loaded");}
}));
tests.Add(("dialogue.no.skipped.side.effects", () => {
 var source=new SourceRef(SourceKind.OriginalFixture,"fixture/scripts.inc","fixture");
 var imported=PretDialogueImporter.Import("Talk::\n msgbox Text, MSGBOX_SIGN\n end\nSideEffect::\n setflag FLAG_X\n msgbox Text\n end", "Text::\n .string \"Hello {PLAYER}\\pNext page$\"",source,source);
 Check(imported.Programs.Count==1&&imported.UnsupportedScripts.Length==1,"unsupported side effect was skipped");
 Check(imported.Programs["Talk"].Pages.Length==2,"pages not preserved");
}));
tests.Add(("dialogue.transaction.and.save.boundary", () => {
 var map=new GridMap(new("fixture:lab"),5,5,Enumerable.Repeat(new TileCell(false),25).ToImmutableArray());
 var dialogues=new Dictionary<string,ReadOnlyDialogue>{{"talk",new(new("script:talk"),new("text:talk"),["Hi {PLAYER}","Second"])}};
 var session=new ReferenceWorldSession(map,new(map.Id,2,2,0,Direction.North),"content",_=>"talk",dialogues);
 var opened=session.Execute("open",0,new("interact"));Check(opened.Status==CommandStatus.Accepted&&session.VisibleText=="Hi RED","dialogue open failed");
 Check(session.ValidateSave(session.State)=="save_event_active","dialogue saved as player stable state");
 var blocked=session.Execute("move",1,new("move",Direction.East));Check(blocked.Status==CommandStatus.Rejected&&session.State.World.StepCount==0,"walk advanced during dialogue");
 session.Execute("next",1,new("interact"));Check(session.VisibleText=="Second","page not advanced");
 session.Execute("close",2,new("interact"));Check(session.VisibleText is null&&session.ValidateSave(session.State) is null,"dialogue not closed");
}));
var results=tests.Select(t=>{try{t.Item2();return new Result(t.Item1,true,null);}catch(Exception e){return new Result(t.Item1,false,e.ToString());}}).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new{suite="integration",count=results.Length,passed=results.Count(r=>r.Passed),failed=results.Count(r=>!r.Passed),results},new JsonSerializerOptions{WriteIndented=true}));
return results.Length>0&&results.All(r=>r.Passed)?0:1;
record Counter(int Value);
record IdHolder(MapId Map);
record Result(string Id,bool Passed,string? Error);
sealed class MemoryStorage:ISaveStorage
{
 public readonly Dictionary<string,byte[]> Files=new();public bool FailCommit;public bool CorruptStage;
 public byte[]? Read(string slot)=>Files.GetValueOrDefault(slot);
 public void WriteStaged(string slot,byte[] bytes)=>Files[slot+".tmp"]=CorruptStage?SaveCodec.Encode(new Counter(0),0,"wrong","wrong"):bytes;
 public void Commit(string slot,bool preservePrimary){if(FailCommit)throw new IOException("injected_commit_failure");if(preservePrimary)Files[slot+".bak"]=Files[slot];Files[slot]=Files[slot+".tmp"];Files.Remove(slot+".tmp");}
}

sealed class EmptyBattleContent:IBattleContent {public MoveDefinition? FindMove(MoveId id)=>null;}

sealed class IntegrationBattleContent:IBattleContent {public MoveDefinition? FindMove(MoveId id)=>id.Value=="move:test"?new(id,Game.Domain.Rules.ElementType.Normal,40,100,30):null;}
