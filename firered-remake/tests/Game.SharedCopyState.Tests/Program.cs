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
WildBattleCombatant Mon(string species,int level,params string[] moves)=>MonIdentity(species,level,42,0,moves);
WildBattleCombatant MonIdentity(string species,int level,uint ot,uint pid,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,ot,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var snapshots=new List<object>();
foreach(string name in new[]{"TRANSFORM","MIMIC","SKETCH"})foreach(uint seed in new uint[]{0,31,127}){
 var actor=Mon("DITTO",5,name);var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor,Mon("PIDGEY",10,"GROWL")],0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var turns=new List<object>();var state=start.State!;
 for(int turn=0;turn<3;turn++){
  var result=WildBattleRules.ResolveMove(catalog,state,0);if(!result.Succeeded)throw new Exception("turn:"+name+":"+result.Fault);
  turns.Add(result);state=result.State!;
 }
 var switched=WildBattleRules.SwitchSourceWildPlayer(catalog,state,1);if(!switched.Succeeded)throw new Exception("switch:"+switched.Fault);turns.Add(switched);
 var data=Json(turns);snapshots.Add(new{name,seed,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("refuse baseline overwrite");
 File.WriteAllText(args[1],Json(new{baseline="242275c plus frozen dynamic/freeze/recharge working tree",snapshots})+"\n");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("frozen canonical singles complete state events RNG and departure",()=>{
 Check(Environment.Version.Major==10,"runtime10");using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==9&&old.Length==snapshots.Count,"nonempty frozen suite");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"state/event/RNG parity "+Json(snapshots[i]));
});
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(WildBattleTransformationRules.PersistentPokemon(m), m.HeldItem, WildBattleTransformationRules.PersistentStatus(m));
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,uint seed=31) {
 var m=supplied??[Mon("DITTO",20,"TRANSFORM"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("DITTO",10,"MIMIC"),Mon("SNORLAX",90,"MINIMIZE")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
 Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2]),Member(Mon("PIDGEY",20,"GROWL"))]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3]),Member(Mon("SPEAROW",20,"GROWL"))])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant m){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(m))})};}
SourceBattleFrame History(SourceBattleFrame f,int target,string? used,string? printed=null){var c=f.Slots[target];return f with{Slots=f.Slots.SetItem(target,c with{Body=c.Body! with{LastUsedMove=used is null?null:Id(used)},Registers=c.Registers with{History=new(){LastPrintedMove=printed is null?used is null?null:Id(used):Id(printed)}}})};}
SourceKernelResult Act(SourceBattleFrame f,int a,int t,int pos=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[a].Occupant!.Value,f.Revision,pos,new(t)));Check(r.Succeeded,r.Fault??"action");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid action output");return r;}
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid turn output");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 int pos=positions?[c.Slot.Value]??c.Registers.Turn?.Lock?.PpSlot??0;var move=catalog.Moves[c.Registers.Turn?.Lock?.Move??c.Body!.Pokemon.Moves[pos].Move];
 var target=c.Registers.Turn?.Lock?.Target??new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1));
 return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var events=r.Events.ToList();int steps=0;while(r.State.FourTurn is not null){Check(++steps<40,"bounded full turn");
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;SourceFourActionResult next,replay;
 if(r.State.FourTurn.Phase==SourceFourActionPhase.ActionSwitchPending){var pending=r.State.PendingSwitches.Single();var incoming=new PartyMemberKey(pending.Slot.Side,pending.LegalPartyIndices[0]);next=SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,pending.Slot,incoming);replay=SourceFourActionTurns.ChooseActionSwitch(catalog,restored,r.Continuation!.Value,pending.Slot,incoming);}
 else if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var pending=r.State.PendingSwitches[0];var incoming=new PartyMemberKey(pending.Slot.Side,pending.LegalPartyIndices[0]);next=SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,pending.Slot,incoming);replay=SourceFourActionTurns.ChooseReplacement(catalog,restored,r.Continuation!.Value,pending.Slot,incoming);}
 else {Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"bounded phase");next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);}
 Check(Json(next)==Json(replay),"JSON state/events/RNG replay at every boundary");r=Ok(next);events.AddRange(r.Events);
 }Check(r.CompletedCleanTurn,"clean turn");return(r.State,events);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null)=>Finish(Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t))));
WildBattleCombatant Bonus(WildBattleCombatant m,int slot,int ups){var old=m.Pokemon.Moves[slot];int max=catalog.Moves[old.Move].Pp*(100+20*ups)/100;return m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(slot,old with{PpUps=ups,MaximumPp=max,CurrentPp=max})}};}
Test("all three shared recipes use every real actor and ally/opponent target",()=>{
 foreach(var move in new[]{"TRANSFORM","MIMIC","SKETCH"})for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2}){
  var f=Frame(Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",50,i==actor?move:"DEFENSE_CURL")).ToArray());f=History(f,target,"DEFENSE_CURL");
  var r=Act(f,actor,target);var live=r.State.Slots[actor].Body!;Check(live.Pokemon.Moves[0].Move==Id("DEFENSE_CURL"),"copied actual recipient");
  Check(r.Invocation!.ChosenMove==Id("UNAVAILABLE")&&live.LastUsedMove==Id("UNAVAILABLE")&&r.State.Slots[actor].Registers.History!.LastPrintedMove==Id("UNAVAILABLE"),"chosen and printed history");
  Check(r.State.Slots[actor].Registers.History!.LastResultingMove==Id(move)&&r.State.Slots[target].Body!.LastLandedMove==Id("UNAVAILABLE"),"resulting and landed history");
  Check(r.Trace.IsEmpty,"copy commands consume no RNG");
  Check(r.Events.Any(e=>e is SourceTransformedEvent x&&x.Actor.Value==actor&&x.Target.Value==target||e is SourceMoveCopiedEvent y&&y.Actor.Value==actor&&y.Target.Value==target),"canonical positions");
  foreach(int other in Enumerable.Range(0,4).Where(i=>i!=actor&&i!=target))Check(Json(r.State.Slots[other])==Json(f.Slots[other]),"unrelated actual position preserved");
 }
});
Test("Transform retains owner identity HP item level PP Ups and donor effective state",()=>{
 var actor=Bonus(Mon("DITTO",20,"TRANSFORM"),0,3);actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=17},Status=WildBattleStatus.Poison,HeldItem="ITEM_FOCUS_BAND"};
 var donor=MonIdentity("GENGAR",80,9182,146,"TACKLE","GROWL","SKETCH","TRANSFORM") with{AttackStage=10,AccuracyStage=2,SpecialAttackStageDelta=3,CalledOverlay=new(){Type1="TYPE_WATER",Type2="TYPE_WATER"}};
 var f=Frame([actor,donor,Mon("DITTO",10,"DEFENSE_CURL"),Mon("SNORLAX",10,"DEFENSE_CURL")]);var r=Act(f,0,1);var m=r.State.Slots[0].Body!;
 Check(m.Transform!.OriginalPokemon.Species==actor.Pokemon.Species&&m.Pokemon.Species==donor.Pokemon.Species,"original/copied species");
 Check(m.Pokemon.Personality==actor.Pokemon.Personality&&m.Pokemon.OriginalTrainerId==actor.Pokemon.OriginalTrainerId&&m.Pokemon.Level==20&&m.Pokemon.CurrentHp==17&&m.Pokemon.Stats.MaxHp==actor.Pokemon.Stats.MaxHp,"identity level and HP retained");
 Check(m.HeldItem=="ITEM_FOCUS_BAND"&&r.Events.OfType<SourceTransformedEvent>().Single().AppearancePersonality==146&&r.Events.OfType<SourceTransformedEvent>().Single().AppearanceOriginalTrainerId==9182,"held item and donor appearance metadata");
 Check(m.Pokemon.AbilityId=="ABILITY_LEVITATE"&&m.CalledOverlay==donor.CalledOverlay&&m.AttackStage==10&&m.AccuracyStage==2&&m.SpecialAttackStageDelta==3,"ability stages and effective types copied");
 Check(m.Pokemon.Moves.Select(s=>s.CurrentPp).SequenceEqual(new[]{5,5,1,5})&&m.Pokemon.Moves[0].PpUps==3&&m.Pokemon.Moves.Skip(1).All(s=>s.PpUps==0),"source minimum PP and owner bonuses");
 Check(r.State.Parties[0].Members[0].Pokemon.Moves[0].Move==Id("TRANSFORM")&&r.State.Parties[0].Members[0].Pokemon.Moves[0].CurrentPp==15,"permanent original PP after command");
 var hit=Act(r.State,0,3);Check(hit.State.Parties[0].Members[0].Pokemon.Moves[0].CurrentPp==15&&hit.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==4,"copied PP suppression");
});
Test("Mimic and Sketch exact histories exclusions PP masks and all-live Sketch write",()=>{
 foreach(string move in new[]{"MIMIC","SKETCH"})foreach(string? prior in new string?[]{null,"NONE","UNAVAILABLE","STRUGGLE","SKETCH","MIMIC","METRONOME","TACKLE"}){
  var f=Frame([Mon("DITTO",20,move),Mon("SNORLAX",20,"GROWL"),Mon("DITTO",20,"GROWL"),Mon("SNORLAX",20,"GROWL")]);var r=Act(History(f,1,prior),0,1);
  bool fail=prior is null or "NONE" or "UNAVAILABLE" or "STRUGGLE" or "SKETCH"||move=="MIMIC"&&prior is "MIMIC" or "METRONOME";
  Check(r.Events.OfType<SourceMoveCopiedEvent>().Any()==!fail,move+":"+prior);
 }
 var baseFrame=Frame([Bonus(Mon("DITTO",20,"MIMIC","SKETCH"),0,3),Mon("SNORLAX",20,"GROWL"),Mon("DITTO",20,"GROWL"),Mon("SNORLAX",20,"GROWL")]);
 var copied=Act(History(baseFrame,1,"TACKLE","GROWL"),0,1).State;Check(copied.Slots[0].Body!.Pokemon.Moves[0]==new PokemonMoveSlot(Id("TACKLE"),5,56,3),"Mimic last-used and retained bonuses");
 var sketched=Act(History(copied,1,"TACKLE","GROWL"),0,1,1).State;Check(sketched.Parties[0].Members[0].Pokemon.Moves[0].Move==Id("TACKLE")&&sketched.Parties[0].Members[0].Pokemon.Moves[1].Move==Id("GROWL"),"Sketch writes every live move");
 var after=Act(sketched,0,1).State;Check(after.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==4&&after.Parties[0].Members[0].Pokemon.Moves[0].CurrentPp==5,"old mimic flag survives Sketch party transmission");
});
Test("copy script guards preserve their command and history boundaries",()=>{
 foreach(string move in new[]{"TRANSFORM","MIMIC","SKETCH"}){
  var f=History(Frame([Mon("DITTO",20,move),Mon("SNORLAX",20,"GROWL"),Mon("DITTO",20,"GROWL"),Mon("SNORLAX",20,"GROWL")]),1,"GROWL");
  var target=f.Slots[1].Body!;var sub=Act(Put(f,1,target with{CalledVolatile=new(){SubstituteHp=10}}),0,1);
  Check(sub.Events.OfType<SourceMoveFailedEvent>().Any()==(move!="TRANSFORM"),"substitute "+move);
  Check(sub.Invocation!.ChosenMove==Id(move=="TRANSFORM"?"UNAVAILABLE":move),"substitute occurs before unavailable for copy");
  var protect=Act(Put(f,1,target with{Protected=true,ProtectConsecutiveUses=1}),0,1);
  Check(protect.Events.OfType<SourceMoveFailedEvent>().Any()==(move=="MIMIC"),"Protect guard "+move);
  var c=f.Slots[1];var charged=f with{Slots=f.Slots.SetItem(1,c with{Body=Mon("SNORLAX",20,"DIG"),Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id("DIG"),0,Id("DIG"),new(0),1,0,null,false),0,null)}})};charged=Put(charged,1,charged.Slots[1].Body!);charged=History(charged,1,"GROWL");
  var underground=Act(charged,0,1);Check(underground.Events.OfType<SourceMoveFailedEvent>().Any()==(move!="SKETCH"),"slot-native semi-invulnerability "+move);
 }
});
Test("real four-action turns use copied moves on next selection and deterministic serialization",()=>{
 foreach(var name in new[]{"TRANSFORM","MIMIC","SKETCH"}){
  var f=Frame([Mon("DITTO",5,name),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("PIDGEY",10,"MINIMIZE"),Mon("SPEAROW",10,"DEFENSE_CURL")]);
  var first=Turn(f);Check(first.Events.OfType<SourceMoveUsedEvent>().Count()==4,"four distinct selected actions");
  Check(first.State.Slots[0].Body!.Pokemon.Moves[0].Move==Id("DEFENSE_CURL"),"actual earlier donor action copied");
  var second=Turn(first.State);Check(second.Events.OfType<SourceMoveUsedEvent>().Single(e=>e.Actor.Value==0).Target.Value==0,"new self move resolves current target mode");
  Check(second.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==(name=="SKETCH"?39:4),"next turn PP");
 }
});
Test("voluntary forced and Baton departure restore only intended permanent state",()=>{
 foreach(string name in new[]{"TRANSFORM","MIMIC","SKETCH"})foreach(string departure in new[]{"voluntary","forced","baton"}){
  var f=History(Frame([Mon("DITTO",40,name,"BATON_PASS"),Mon("SNORLAX",90,"DEFENSE_CURL","BATON_PASS"),Mon("PIDGEY",20,"MINIMIZE"),Mon("SPEAROW",20,"DEFENSE_CURL")]),1,"DEFENSE_CURL");f=Act(f,0,1).State;
  if(departure=="forced")f=Put(f,1,Mon("SNORLAX",90,"ROAR"));
  var choices=Choices(f,departure=="baton"?new[]{1,0,0,0}:null).ToArray();
  if(departure=="voluntary")choices[0]=new(f.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2));
  var r=Finish(Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices.ToImmutableArray())));
  Check(r.State.Slots[0].Member!.Value.Index==2&&r.State.Slots[0].Body!.Transform is null&&r.State.Slots[0].Body!.CopyMoves is null,"fresh entrant "+name+departure);
  var stored=r.State.Parties[0].Members[0].Pokemon;Check(stored.Species==f.Parties[0].Members[0].Pokemon.Species&&stored.Moves[0].Move==Id(name=="SKETCH"?"DEFENSE_CURL":name),"departure exact permanent data");
  Check(r.Events.OfType<SourceCalledPartySwitchedEvent>().Any(e=>e.Side.Value==0),"real slot switch event");
 }
});
Test("Mimic into Transform clears copied flags without leaking temporary move",()=>{
 var f=Frame([Mon("DITTO",20,"MIMIC"),Mon("SNORLAX",20,"DEFENSE_CURL"),Mon("DITTO",20,"GROWL"),Mon("SNORLAX",20,"GROWL")]);f=Act(History(f,1,"TRANSFORM"),0,1).State;
 var transformed=Act(f,0,1).State.Slots[0].Body!;Check(transformed.CopyMoves is null&&transformed.Transform!.OriginalPokemon.Moves[0].Move==Id("MIMIC"),"single permanent origin after mimic-transform");
});
Test("malformed copy and transform states and unimplemented caller selections reject atomically",()=>{
 var start=History(Frame(),1,"DEFENSE_CURL");var transformed=Act(start,0,1).State;var t=transformed.Slots[0].Body!;
 var bad=new[]{t with{Transform=t.Transform! with{OriginalPokemon=null!}},t with{Transform=t.Transform! with{CopiedPokemon=null!}},t with{Transform=t.Transform! with{OriginalPpUps=default}},t with{Pokemon=t.Pokemon with{Moves=default}},t with{Transform=t.Transform! with{OriginalPpUps=[4,0,0,0]}},t with{Pokemon=t.Pokemon with{AbilityId="ABILITY_PRESSURE"}},t with{CopyMoves=new([])},t with{Transform=null,CopyMoves=new([new(9,t.Pokemon.Moves[0])])},t with{Transform=null,CopyMoves=new([null!])}};
 foreach(var body in bad){var f=transformed with{Slots=transformed.Slots.SetItem(0,transformed.Slots[0] with{Body=body})};
  var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1)));Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic malformed action");
  var turn=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,[]);Check(!turn.Succeeded&&ReferenceEquals(turn.State,f)&&turn.Events.IsEmpty&&turn.Trace.IsEmpty,"atomic malformed turn");}
 var caller=Act(History(Put(Frame(),0,Mon("DITTO",20,"SKETCH")),1,"METRONOME"),0,1).State;
 var reject=SourceFourActionTurns.BeginActions(catalog,caller,caller.Revision,Choices(caller));Check(!reject.Succeeded&&ReferenceEquals(reject.State,caller)&&reject.Events.IsEmpty&&reject.Trace.IsEmpty,"copied caller has explicit unsupported selection");
});

Test("unchanged compiled source command outcomes match every copy actor",()=>{
 Check(args.Length>=3,"required freshly compiled source oracle");using var oracle=JsonDocument.Parse(File.ReadAllText(args[2]));var vectors=oracle.RootElement.GetProperty("copy").EnumerateArray().ToArray();Check(vectors.Length==112,"112 extracted-source cases");
 foreach(var v in vectors){int a=v.GetProperty("actor").GetInt32();bool sketch=v.GetProperty("mode").GetInt32()==1,transformed=v.GetProperty("transformed").GetInt32()==1;int prior=v.GetProperty("prior").GetInt32();string name=sketch?"SKETCH":"MIMIC";
  string priorName=prior==0?"NONE":prior==65535?"UNAVAILABLE":catalog.Moves.Values.Single(m=>m.SourceMoveId==prior).Id.Value[10..];
  var f=Frame(Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",30,i==a?transformed?"TRANSFORM":name:i==(a^1)&&transformed?name:"GROWL")).ToArray());
  if(transformed)f=Act(f,a,a^1).State;f=History(f,a^1,priorName);var r=Act(f,a,a^1);var mon=r.State.Slots[a].Body!;
  Check(r.Events.OfType<SourceMoveCopiedEvent>().Any()==v.GetProperty("success").GetBoolean(),"oracle success");
  Check(catalog.Moves[mon.Pokemon.Moves[0].Move].SourceMoveId==v.GetProperty("move").GetInt32()&&mon.Pokemon.Moves[0].CurrentPp==v.GetProperty("pp").GetInt32(),"oracle move and PP "+Json(v)+" actual "+Json(mon.Pokemon.Moves[0]));
  Check((mon.CopyMoves?.Slots.Aggregate(0,(mask,c)=>mask|1<<c.Slot)??0)==v.GetProperty("mask").GetInt32()&&r.Invocation!.ChosenMove==Id("UNAVAILABLE"),"oracle mimic mask and chosen register");
 }
 var transforms=oracle.RootElement.GetProperty("transform").EnumerateArray().ToArray();Check(transforms.Length==12,"12 source Transform outcomes");
 foreach(var v in transforms){int a=v.GetProperty("actor").GetInt32(),blocked=v.GetProperty("blocked").GetInt32();var f=Frame(Enumerable.Range(0,4).Select(i=>Mon(i==a?"DITTO":"GENGAR",30,i==a||blocked==1&&i==(a^1)?"TRANSFORM":"TACKLE")).ToArray());
  if(blocked==1)f=Act(f,a^1,a^3).State;
  if(blocked==2){int t=a^1;var donor=Mon("GENGAR",30,"DIG");f=Put(f,t,donor);var c=f.Slots[t];f=f with{Slots=f.Slots.SetItem(t,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id("DIG"),0,Id("DIG"),new(a),1,0,null,false),0,null)}})};}
  var r=Act(f,a,a^1);Check(r.Events.OfType<SourceTransformedEvent>().Any()==v.GetProperty("success").GetBoolean(),"oracle transform guard");
  Check(r.State.Slots[a].Body!.Pokemon.SourceSpeciesId==v.GetProperty("species").GetInt32(),"oracle transform species");
 }
});
Test("real Follow Me and voluntary donor replacement resolve current identities",()=>{
 var f=Frame([Mon("DITTO",10,"TRANSFORM"),Mon("SNORLAX",30,"DEFENSE_CURL"),Mon("PIDGEY",10,"MINIMIZE"),Mon("TOGETIC",30,"FOLLOW_ME")]);
 var redirected=Turn(f);var copied=redirected.Events.OfType<SourceTransformedEvent>().Single();Check(copied.Target.Value==3&&copied.AppearanceSpecies==f.Slots[3].Body!.Pokemon.Species,"Follow Me copied actual redirected donor");
 f=Frame();var selections=Choices(f).ToArray();selections[1]=new(f.Slots[1].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,2));
 var switched=Finish(Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,selections.ToImmutableArray())));copied=switched.Events.OfType<SourceTransformedEvent>().Single();Check(copied.Target.Value==1&&copied.AppearanceSpecies==IdSpecies("SPEAROW"),"selected position observes new occupant");
 Check(!switched.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==1),"incoming donor cannot steal departed action");
});
SpeciesId IdSpecies(string name)=>new("pret:SPECIES_"+name);
Test("Transform failure retransform and copied Pressure use source lifecycle",()=>{
 var actor=Bonus(Mon("DITTO",20,"TRANSFORM"),0,3);var f=Frame([actor,Mon("DUSCLOPS",30,"TRANSFORM","GROWL"),Mon("GENGAR",30,"TACKLE"),Mon("SNORLAX",30,"DEFENSE_CURL")]);
 var first=Act(f,0,1);Check(first.State.Parties[0].Members[0].Pokemon.Moves[0].CurrentPp==14,"Pressure charges two original PP");
 var again=Act(first.State,0,2);Check(again.State.Slots[0].Body!.Pokemon.Species==IdSpecies("GENGAR")&&again.State.Parties[0].Members[0].Pokemon.Moves[0].CurrentPp==14,"retransform uses original identity and suppressed temporary PP");
 var failed=Act(first.State,1,0);Check(failed.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="target-transformed"),"transformed target fails");
});

Test("copied Trace and Forecast overlays keep their effective ability and type",()=>{
 foreach(string donor in new[]{"PORYGON","CASTFORM"}){
  var f=Frame([Mon("DITTO",10,"TRANSFORM"),Mon(donor,30,"DEFENSE_CURL"),Mon(donor=="CASTFORM"?"GROUDON":"PIDGEY",30,"MINIMIZE"),Mon("SNORLAX",30,"DEFENSE_CURL")]);
  f=Ok(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;var expected=f.Slots[1].Body!;
  var transformed=Turn(f).State;var actual=transformed.Slots[0].Body!;
  Check(actual.Pokemon.Species==expected.Pokemon.Species&&actual.CalledOverlay?.Ability==expected.CalledOverlay?.Ability&&actual.CalledOverlay?.Type1==expected.CalledOverlay?.Type1,"effective Trace/Forecast state copied");
  Check(transformed.Parties[0].Members[0].Pokemon.AbilityId=="ABILITY_LIMBER","permanent ability remains Ditto");
  Check(Turn(transformed).State.Slots[0].Body!.Transform is not null,"effective state survives full next turn");
 }
});
Test("fainted copied occupants use deferred KO entry and retain permanent party identity",()=>{
 foreach(string name in new[]{"TRANSFORM","MIMIC","SKETCH"}){
  var f=History(Frame([Mon("DITTO",10,name),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("PIDGEY",20,"MINIMIZE"),Mon("SPEAROW",20,"DEFENSE_CURL")]),1,"DEFENSE_CURL");
  f=Act(f,0,1).State;f=Put(f,0,f.Slots[0].Body! with{Pokemon=f.Slots[0].Body!.Pokemon with{CurrentHp=1}});f=Put(f,1,Mon("SNORLAX",80,"QUICK_ATTACK"));
  var r=Turn(f);Check(r.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==0)&&r.Events.OfType<SourceCalledPartySwitchedEvent>().Any(e=>e.Side.Value==0&&e.Kind==WildBattleCalledSwitchKind.FaintedReplacement),"actual KO plus deferred replacement");
  Check(r.State.Parties[0].Members[0].Pokemon.Species==IdSpecies("DITTO")&&r.State.Parties[0].Members[0].Pokemon.CurrentHp==0&&r.State.Parties[0].Members[0].Pokemon.Moves[0].Move==Id(name=="SKETCH"?"DEFENSE_CURL":name),"original fainted party data");
  Check(r.State.Slots[0].Body!.Transform is null&&r.State.Slots[0].Body!.CopyMoves is null,"new entrant has no copied state");
 }
});
File.WriteAllText(args[1],Json(new{passed=failures==0,groups,checks,failures,results,snapshots})+"\n");return failures==0?0:1;
