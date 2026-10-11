using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [--capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var snapshots=new List<object>();
foreach(string name in new[]{"ENCORE","IMPRISON"})foreach(uint seed in new uint[]{0,31,127})foreach(string kind in new[]{"valid","no-history","forbidden","empty-pp"}){
 var actor=Mon("ALAKAZAM",40,name,"DEFENSE_CURL");var target=Mon("SNORLAX",100,kind=="forbidden"?"MIRROR_MOVE":"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 target=start.State!.Opponent with{SourceMoveHistory=new(),LastUsedMove=kind=="no-history"?null:target.Pokemon.Moves[0].Move};
 if(kind=="empty-pp")target=target with{Pokemon=target.Pokemon with{Moves=target.Pokemon.Moves.SetItem(0,target.Pokemon.Moves[0] with{CurrentPp=0})}};
 var state=start.State with{Opponent=target};
 // The prepared party projection remains the exact live HP/PP identity.
 state=state with{CalledParty=state.CalledParty! with{Opponent=state.CalledParty.Opponent.SetItem(0,new(target.Pokemon,target.HeldItem,target.Status))}};
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));if(!result.Battle.Succeeded)throw new Exception("action:"+result.Battle.Fault);
 var data=Json(result);snapshots.Add(new{name,seed,kind,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasMoveRestrictionRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires frozen pre-edit Domain");File.WriteAllText(args[1],Json(new{baseline="d449a1d71e313ab5e6bac2a7ef7f8ae2fc41b3e3+frozen-dirty",snapshots})+"\n");return 0;}
int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
using(var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")))){
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==24&&old.Length==snapshots.Count,"nonempty frozen corpus");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"complete singles state/events/RNG parity "+Json(snapshots[i]));
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,uint seed=31){var m=mons??[Mon("ALAKAZAM",40,"ENCORE","IMPRISON","DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL","MINIMIZE"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int position=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,position,new(target)));Check(r.Succeeded,r.Fault??"accepted action");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=positions?[c.Slot.Value]??c.Body!.EncoreMoveSlot??0;var move=catalog.Moves[pos<0?WildBattleRules.StruggleMove:c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted turn");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid identity");return r;}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f,int[]? positions=null,int[]? targets=null){var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,positions,targets)));var events=r.Events.ToList();var trace=r.Trace.ToList();int guard=0;while(r.State.FourTurn is not null){Check(++guard<30,"bounded completion");var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var actual=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(actual)==Json(replay),"exact JSON continuation events RNG");r=Ok(actual);events.AddRange(r.Events);trace.AddRange(r.Trace);}Check(r.CompletedCleanTurn,"clean turn");return(r.State,events,trace);}
var labels=new List<string>();void Test(string label,Action run){run();labels.Add(label);Console.WriteLine("PASS "+label);}
Test("all four positions: Encore actual history and PP slot, Imprison both opposing slots",()=>{
 for(int actor=0;actor<4;actor++){
  var m=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"DEFENSE_CURL","MINIMIZE")).ToArray();m[actor]=Mon("ALAKAZAM",40,"ENCORE","IMPRISON","DEFENSE_CURL");m[actor^1]=m[actor^1] with{LastUsedMove=Id("MINIMIZE")};var f=Frame(m);var r=Act(f,actor,actor^1);
  Check(r.State.Slots[actor^1].Body!.EncoreMoveSlot==1&&r.State.Slots[actor^1].Body!.EncoredMove==Id("MINIMIZE"),"matched actual previous move at second position");Check(r.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(new[]{"accuracy","encore-duration"}),"exact draw order");
  var draw=Gen3Rng.Next(Gen3Rng.Next(f.Rng,"accuracy").State,"duration").Value;Check(r.State.Slots[actor^1].Body!.EncoreTurnsRemaining==(draw&3)+3,"source duration boundary");
  var imp=Act(f,actor,actor,1);Check(imp.State.Slots[actor].Body!.Imprison&&imp.Trace.IsEmpty,"Imprison no RNG");
 }
});
Test("Encore failure boundaries consume accuracy and PP but no duration",()=>{
 foreach(string kind in new[]{"none","unavailable","struggle","encore","mirror","unlearned","zero-pp","already"}){
  var f=Frame();var target=f.Slots[1].Body! with{LastUsedMove=kind switch{"none"=>null,"unavailable"=>Id("UNAVAILABLE"),"struggle"=>Id("STRUGGLE"),"encore"=>Id("ENCORE"),"mirror"=>Id("MIRROR_MOVE"),"unlearned"=>Id("TACKLE"),_=>Id("DEFENSE_CURL")}};
  if(kind=="zero-pp")target=target with{Pokemon=target.Pokemon with{Moves=target.Pokemon.Moves.SetItem(0,target.Pokemon.Moves[0] with{CurrentPp=0})}};
  if(kind=="already")target=target with{EncoreMoveSlot=0,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3};
  var r=Act(Put(f,1,target),0,1);Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-encore-target")&&r.Trace.Length==1,"no duration draw "+kind);Check(r.Events.OfType<SourcePpCommandEvent>().Single().Before-r.Events.OfType<SourcePpCommandEvent>().Single().After==1,"base PP "+kind);
 }
});
Test("Encore bypasses Substitute but respects Protect",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{LastUsedMove=Id("DEFENSE_CURL"),CalledVolatile=new(){SubstituteHp=20}});Check(Act(f,0,1).State.Slots[1].Body!.EncoreMoveSlot==0,"no substitute command in Encore script");
 var protectedFrame=Put(f,1,f.Slots[1].Body! with{Protected=true});var r=Act(protectedFrame,0,1);Check(r.Trace.IsEmpty&&r.State.Slots[1].Body!.EncoreMoveSlot is null&&r.Events.OfType<SourcePpCommandEvent>().Count()==1,"Protect miss before accuracy");
});
Test("Imprison scans foes only, live moves and dedicated two Pressure charges",()=>{
 var m=new[]{Mon("ALAKAZAM",40,"IMPRISON","DEFENSE_CURL"),Mon("ARTICUNO",100,"MINIMIZE"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("ZAPDOS",100,"DEFENSE_CURL")};var f=Frame(m);var r=Act(f,0,0);Check(r.State.Slots[0].Body!.Imprison&&r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==7,"base1 plus both Pressure2");Check(r.Events.OfType<SourcePpCommandEvent>().Select(e=>e.CommandCost).SequenceEqual(new[]{1,2}),"distinct dedicated pressure command");
 var repeated=Act(r.State,0,0);Check(repeated.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==6,"already-active failure only base PP");
 f=Put(f,3,Mon("ZAPDOS",100,"MINIMIZE"));var noShared=Act(f,0,0);Check(!noShared.State.Slots[0].Body!.Imprison&&noShared.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==7,"failure still charges Pressure; ally overlap insufficient");
 var sealedFrame=r.State;Check(!SourceFourActionTurns.RequiresStruggle(catalog,sealedFrame,new(1))&&SourceFourActionTurns.RequiresStruggle(catalog,sealedFrame,new(3)),"opposite flank seal");
 var transformed=Put(sealedFrame,0,Mon("DITTO",40,"TRANSFORM","DEFENSE_CURL") with{Imprison=true});var copied=Act(transformed,0,1);Check(!SourceFourActionTurns.RequiresStruggle(catalog,copied.State,new(3))&&SourceFourActionTurns.RequiresStruggle(catalog,copied.State,new(1)),"Transform updates live Imprison immediately");
});
Test("real four-action Encore redirects selected move PP position target and resumes",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{LastUsedMove=Id("DEFENSE_CURL")});var r=Turn(f,[0,1,0,0],[1,1,2,3]);var target=r.State.Slots[1];Check(target.Body!.Pokemon.Moves[0].CurrentPp==39&&target.Body.Pokemon.Moves[1].CurrentPp==20,"only forced position spends PP");Check(target.Registers.LastMoveSlot==0&&target.Body.LastUsedMove==Id("DEFENSE_CURL"),"actual slot and move history");Check(r.Events.OfType<SourceMoveUsedEvent>().Count()==4,"four actual actions");int duration=r.Events.OfType<SourceEncoreChangedEvent>().First().After;Check(target.Body.EncoreTurnsRemaining==duration-1,"same turn first countdown");
 var next=Turn(r.State,[2,0,0,0]);Check(next.State.Slots[1].Body!.EncoreTurnsRemaining==duration-2,"next full turn countdown");
});
Test("real four-action Imprison cancellation before PP then honest Struggle",()=>{
 var m=new[]{Mon("ALAKAZAM",100,"IMPRISON","MINIMIZE"),Mon("SNORLAX",20,"MINIMIZE"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")};var r=Turn(Frame(m),[0,0,0,0]);Check(r.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason=="imprisoned")==2,"both opposing queued actions blocked");Check(r.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp==20&&r.State.Slots[3].Body!.Pokemon.Moves[0].CurrentPp==20,"blocked before PP");Check(SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(1))&&SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(3)),"both owe Struggle");
 var next=Turn(r.State,[1,-1,0,-1],[0,0,2,2]);Check(next.Events.OfType<SourceAttackStringEvent>().Count(e=>e.CurrentMove==WildBattleRules.StruggleMove)==2,"two actual forced Struggles");
 var gated=Put(Frame(),1,Mon("SNORLAX",100,"METRONOME"));Check(!SourceFourActionTurns.RequiresStruggle(catalog,gated,new(1)),"positive-PP caller does not require Struggle");Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("METRONOME")]),"authenticated caller admitted");
});
Test("Encore end-turn PP exhaustion overwrite and learning identity",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{EncoreMoveSlot=0,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3,Pokemon=f.Slots[1].Body!.Pokemon with{Moves=f.Slots[1].Body!.Pokemon.Moves.SetItem(0,f.Slots[1].Body!.Pokemon.Moves[0] with{CurrentPp=1})}});var r=Turn(f,[2,0,0,0]);Check(r.State.Slots[1].Body!.EncoredMove is null&&r.Events.OfType<SourceEncoreChangedEvent>().Single().Reason=="expired","final PP expires Encore");
 var body=Frame().Slots[1].Body! with{EncoreMoveSlot=0,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3};var cell=Frame().Slots[1] with{Body=body};var learned=body.Pokemon with{Moves=body.Pokemon.Moves.SetItem(0,new(Id("TACKLE"),35,35))};
 var project=typeof(SourceBattleFrame).Assembly.GetType("Game.Domain.Battle.SourceTurnMoveRules")!.GetMethod("ProjectPokemon",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 var projected=(SourceBattleSlot)project.Invoke(null,[cell,learned])!;Check(projected.Body!.EncoredMove==Id("DEFENSE_CURL")&&projected.Body.Pokemon.Moves[0].Move==Id("TACKLE"),"learning retains stale Encore until owner boundary");
 var stale=Put(Frame(),1,projected.Body);Check(SourceFourActionTurns.RequiresStruggle(catalog,stale,new(1)),"old move identity absent means source exhaustion");var expired=Turn(stale,[2,-1,0,0],[0,0,2,3]);Check(expired.State.Slots[1].Body!.EncoredMove is null&&expired.Events.OfType<SourceEncoreChangedEvent>().Single().Reason=="move-changed","end-turn clears overwritten identity");
});
Test("actual faint replacement clears Imprison and Encore before resumed entry",()=>{
 var m=new[]{Mon("ALAKAZAM",20,"IMPRISON","DEFENSE_CURL") with{Imprison=true,EncoreMoveSlot=1,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3},Mon("SNORLAX",100,"TACKLE"),Mon("PIDGEY",20,"MINIMIZE"),Mon("RATTATA",20,"MINIMIZE")};var f=Frame(m);f=Put(f,0,f.Slots[0].Body! with{Pokemon=f.Slots[0].Body!.Pokemon with{CurrentHp=1}});var party=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("SNORLAX",100,"DEFENSE_CURL")))})};var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[1,0,0,0],[0,0,2,3])));bool replaced=false;int guard=0;
 while(r.State.FourTurn is not null){Check(++guard<30,"replacement completion");if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){Check(!r.State.Slots[0].Body!.Imprison&&r.State.Slots[0].Body!.EncoredMove is null,"faint cleared both");var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,restored,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));replaced=true;}else r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));}
 Check(replaced&&r.CompletedCleanTurn&&r.State.Slots[0].Member?.Index==2&&!r.State.Slots[0].Body!.Imprison&&r.State.Slots[0].Body!.EncoredMove is null,"clean new occupant, completed resumed turn");
});
Test("ordinary switch clears Encore and Imprison; queued Struggle keeps its earned action",()=>{
 var f=Frame();f=Put(f,0,f.Slots[0].Body! with{Imprison=true,EncoreMoveSlot=2,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3});f=Put(f,1,Mon("SNORLAX",100,"DEFENSE_CURL"));f=Put(f,2,Mon("SNORLAX",100,"MINIMIZE"));var party=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("SNORLAX",100,"MINIMIZE")))})};Check(SourceFourActionTurns.RequiresStruggle(catalog,f,new(1)),"sealed before switch");
 var choices=Choices(f,[2,-1,0,0],[0,0,2,3]).SetItem(0,new(f.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));Check(r.State.Slots[0].Member?.Index==2&&!r.State.Slots[0].Body!.Imprison&&r.State.Slots[0].Body!.EncoredMove is null,"ordinary switch starts clean");Check(!SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(1)),"seal vanished immediately");var events=r.Events.ToList();while(r.State.FourTurn is not null){r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));events.AddRange(r.Events);}Check(events.OfType<SourceAttackStringEvent>().Any(e=>e.Actor.Value==1&&e.CurrentMove==WildBattleRules.StruggleMove),"earned Struggle retained after opposing switch");
});
Test("actual Sketch changes live seal and actual Transform makes last move unavailable",()=>{
 var f=Frame([Mon("SMEARGLE",40,"SKETCH","MINIMIZE") with{Imprison=true},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("PIDGEY",20,"MINIMIZE"),Mon("RATTATA",20,"DEFENSE_CURL")]);var cell=f.Slots[1];f=f with{Slots=f.Slots.SetItem(1,cell with{Registers=cell.Registers with{History=new(){LastPrintedMove=Id("DEFENSE_CURL")}}})};Check(!SourceFourActionTurns.RequiresStruggle(catalog,f,new(3)),"unsealed before Sketch");var copied=Act(f,0,1);Check(copied.State.Slots[0].Body!.Imprison&&copied.State.Slots[0].Body!.Pokemon.Moves[0].Move==Id("DEFENSE_CURL")&&SourceFourActionTurns.RequiresStruggle(catalog,copied.State,new(3)),"permanent Sketch changes live seal immediately");
 var transformed=Act(Put(Frame(),1,Mon("DITTO",40,"TRANSFORM")),1,2);Check(transformed.State.Slots[1].Body!.LastUsedMove==Id("UNAVAILABLE"),"actual Transform history unavailable");var encore=Act(transformed.State,0,1);Check(encore.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-encore-target"),"Encore fails on actual transformed history");
});
Test("pending action executes overwritten Encore position with source target reselection",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{EncoreMoveSlot=0,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3});var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[2,0,0,0])));r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));var body=r.State.Slots[1].Body!;var replaced=body with{Pokemon=body.Pokemon with{Moves=body.Pokemon.Moves.SetItem(0,new(Id("TACKLE"),35,35))}};var changed=Put(r.State,1,replaced);r=Ok(SourceFourActionTurns.Advance(catalog,changed,r.Continuation!.Value));Check(r.Events.OfType<SourceEncoreChangedEvent>().Any(e=>e.Side.Value==1&&e.Reason=="move-changed"),"action selector clears overwritten Encore");Check(r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==1&&e.Move==Id("TACKLE")&&e.Target.Side==BattleSide.Player),"new move in retained PP position gets source target");Check(r.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp==34&&r.State.Slots[1].Body!.EncoredMove is null,"new move PP spent and old encore cleared");
});
Test("unresolved standalone Encore choices fail atomically",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{EncoreMoveSlot=0,EncoredMove=Id("DEFENSE_CURL"),EncoreTurnsRemaining=3});var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[1].Occupant!.Value,f.Revision,1,new(1)));Check(!r.Succeeded&&r.Fault=="source_kernel_encore_selection_owner_required"&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"caller must resolve Encore before kernel");
});
Check(Environment.Version.Major==10,"actual runtime 10");Console.WriteLine("PASS "+checks);File.WriteAllText(args[1],Json(new{checks,labels,snapshots})+"\n");return 0;
