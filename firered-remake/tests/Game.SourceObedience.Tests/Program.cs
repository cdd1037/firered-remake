using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 2) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
int checks=0, failures=0, vectors=0, intentionalDeviations=0, fullTurns=0; var results = new List<object>(); var selectedTurns = new List<object>();
void Check(bool ok,string why){checks++;if(!ok)throw new InvalidDataException(why);}
void Test(string name,Action run){try{run();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;results.Add(new{name,passed=false,error=ex.ToString()});Console.WriteLine("FAIL "+name+" "+ex);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species="RATTATA",int level=50,uint owner=43,params string[] names)
{
 var mon=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,owner,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(1)).Pokemon;
 if(names.Length==0)names=["TACKLE","GROWL","SPLASH","TAIL_WHIP"];
 return new(mon with{Moves=names.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()});
}
WildBattleCalledPartyMember Member(WildBattleCombatant mon)=>new(WildBattleTransformationRules.PersistentPokemon(mon),mon.HeldItem,WildBattleTransformationRules.PersistentStatus(mon));
SourceObedienceContext Context(int count=1,byte badges=0,string ot="BLUE")=>new(42,"RED",badges,
 Enumerable.Range(0,count).Select(i=>new SourceObedienceMember(new(BattleSide.Player,i),i==0?ot:"RED")).ToImmutableArray());
WildBattleState Single(WildBattleCombatant? mon=null,uint seed=1)
{
 var p=mon??Mon();var o=Mon("SLOWBRO",100,99,"AMNESIA");
 return new(p,o,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=WildBattleTerrain.Plain,CalledParty=new([Member(p)],[Member(o)],0,0),Obedience=Context()};
}
SourceBattleFrame Four(uint seed=1,WildBattleCombatant? actor=null)
{
 var mons=new[]{actor??Mon(),Mon("SLOWBRO",100,99,"AMNESIA"),Mon("RATTATA",50,42,"SPLASH"),Mon("SLOWBRO",100,98,"AMNESIA")};
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Obedience=Context(2),
  Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray()};
}
SourceKernelResult Act(SourceBattleFrame f,int slot=0)=>SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[slot].Occupant!.Value,f.Revision,0,catalog.Moves[f.Slots[slot].Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?new(slot):new(slot^1)));
WildBattleCalledMoveResult ActSingle(WildBattleState s)=>WildBattleRules.ResolveSourceMoveAction(catalog,s,new(WildBattleSide.Player,0));
uint Seed(SourceObedienceOutcome outcome,WildBattleCombatant? mon=null)
{
 var s=Single(mon);
 for(uint seed=0;seed<2000;seed++)
 {
  var r=ActSingle(s with{Rng=new(seed)});Check(r.Succeeded,"seed action "+r.Fault);
  if(r.Battle.Events.OfType<WildBattleObedienceEvent>().FirstOrDefault()?.Decision.Outcome==outcome)return seed;
 }
 throw new InvalidDataException("No seed for "+outcome);
}
Test("pinned C boundaries and intentional event-origin deviations",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1],"source-obedience-vectors.json")));
 Check(doc.RootElement.GetProperty("sourceCommit").GetString()==WildBattleRules.SourceCommit,"source pin");
 var decide=typeof(SourceObedienceRules).GetMethod("Decide",BindingFlags.NonPublic|BindingFlags.Static)!;
 foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
 {
  var x=row.GetProperty("input");var y=row.GetProperty("output");string name=row.GetProperty("name").GetString()!;
  if(x.GetProperty("battleTypeFlags").GetInt32()!=0)continue; // link/tutorial transports are deliberately not this API.
  string Decode(JsonElement bytes)=>new(bytes.EnumerateArray().Select(b=>b.GetInt32()).TakeWhile(b=>b!=255).Select(b=>(char)b).ToArray());
  bool foreign=SourceObedienceRules.IsOtherTrainer(x.GetProperty("otId").GetUInt32(),Decode(x.GetProperty("otName")),x.GetProperty("playerId").GetUInt32(),Decode(x.GetProperty("playerName")));
  Check(foreign==(y.GetProperty("foreign").GetInt32()!=0),"full identity "+name);
  int species=x.GetProperty("species").GetInt32();bool sourceSpecialGate=species is 151 or 410 && x.GetProperty("modernFateful").GetInt32()==0;
  int ability=x.GetProperty("ability").GetInt32();string abilityName=ability==15?"ABILITY_INSOMNIA":ability==72?"ABILITY_VITAL_SPIRIT":"ABILITY_NONE";
  var move=catalog.Moves.Values.Single(m=>m.SourceMoveId==x.GetProperty("currentMove").GetInt32()).Id;
  int mask=x.GetProperty("limitedMask").GetInt32()|(1<<x.GetProperty("currMovePos").GetInt32());
  bool uproar=((x.GetProperty("status2").GetInt32()|x.GetProperty("otherStatus2").EnumerateArray().Take(x.GetProperty("battlersCount").GetInt32()-1).Aggregate(0,(a,b)=>a|b.GetInt32()))&0x70)!=0;
  var input=new SourceObedienceInput(x.GetProperty("level").GetInt32(),x.GetProperty("attackerSide").GetInt32()==0,foreign,(byte)x.GetProperty("badgeFlags").GetInt32(),x.GetProperty("status1").GetUInt32(),abilityName,move,(byte)mask,uproar);
  ushort[] draws=x.GetProperty("draws").EnumerateArray().Select(d=>d.GetUInt16()).ToArray();int cursor=0;
  Func<string,int,ushort> draw=(_,_)=>draws[cursor++];
  var actual=(SourceObedienceDecision)decide.Invoke(null,[input,draw])!;
  if(sourceSpecialGate){Check(actual.Outcome==SourceObedienceOutcome.Obey&&cursor==0&&y.GetProperty("result").GetInt32()!=0,"intentional event-origin gate removal "+name);intentionalDeviations++;continue;}
  string script=actual.Outcome switch{SourceObedienceOutcome.Obey=>"unchanged",SourceObedienceOutcome.IgnoreAsleep=>"ignore_asleep",SourceObedienceOutcome.Loaf=>"loaf",SourceObedienceOutcome.Nap=>"nap",SourceObedienceOutcome.SelfHit=>"self_hit",_=>"alternate"};
  Check(actual.SourceReturn==y.GetProperty("result").GetInt32()&&cursor==y.GetProperty("drawCount").GetInt32(),"branch/draws "+name);
  Check(script==y.GetProperty("script").GetString(),"script "+name+" actual "+script);
  if(actual.MoveSlot is{} position)Check(position==y.GetProperty("currMovePos").GetInt32(),"alternate slot "+name);
  if(actual.LoafString is{} loaf)Check(loaf==y.GetProperty("multiString").GetInt32(),"loaf string "+name);
  vectors++;
 }
 Check(vectors==60&&intentionalDeviations==2,"60 source-equivalent vectors and two deliberate event-origin deviations");
});
Test("explicit owned identity and badge gates preserve no-RNG obedience",()=>{
 foreach(byte badge in new byte[]{0,2,8,32,128,255})
 {
  var s=Single(Mon(owner:42)) with{Obedience=Context(badges:badge,ot:"RED")};var r=ActSingle(s);Check(r.Succeeded,r.Fault??"owned");
  var baseline=ActSingle(s with{Obedience=null});Check(baseline.Succeeded,"baseline");
  Check(!r.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"owned no obedience draw");
  Check(Json(r.Battle.Events)==Json(baseline.Battle.Events)&&Json(r.State! with{Obedience=null})==Json(baseline.State),"owned matches established default");
 }
 foreach((byte badge,int cap) in new[]{((byte)0,10),((byte)2,30),((byte)8,50),((byte)32,70),((byte)128,100)})
 {
  var s=Single(Mon(level:cap)) with{Obedience=Context(badges:badge)};var r=ActSingle(s);
  Check(r.Succeeded&&!r.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"at badge cap");
 }
});
Test("nap loaf self-hit and alternate preserve PP history and deterministic replay",()=>{
 foreach(var outcome in new[]{SourceObedienceOutcome.Nap,SourceObedienceOutcome.Loaf,SourceObedienceOutcome.SelfHit,SourceObedienceOutcome.AlternateMove})
 {
  uint seed=Seed(outcome);var s=Single(seed:seed);var r=ActSingle(s);var replay=ActSingle(s);Check(Json(r)==Json(replay),"action replay "+outcome);
  var p=r.State!.Player;var e=r.Battle.Events.OfType<WildBattleObedienceEvent>().Single();Check(e.Decision.Outcome==outcome,"selected branch");
  if(outcome==SourceObedienceOutcome.AlternateMove)
  {
   int pos=e.Decision.MoveSlot!.Value;var id=p.Pokemon.Moves[pos].Move;
   Check(r.Invocation!.ChosenSlot==pos&&r.Invocation.PpSlot==pos&&r.Invocation.OriginalChosenMove==Id("TACKLE")&&r.Invocation.ChosenMove==id&&r.Invocation.CurrentMove==id,"alternate registers");
   Check(p.Pokemon.Moves[0].CurrentPp==s.Player.Pokemon.Moves[0].CurrentPp&&p.Pokemon.Moves[pos].CurrentPp==s.Player.Pokemon.Moves[pos].CurrentPp-1,"alternate real PP owner");
   Check(p.LastUsedMove==id&&p.SourceMoveHistory!.LastResultingMove==id,"alternate history");
   Check(r.Battle.Trace.Count(t=>t.Purpose.EndsWith("obedience-check"))==1,"one obedience check only");
  }
  else
  {
   Check(!r.Battle.Events.OfType<WildBattlePpCommandEvent>().Any()&&!r.Battle.Events.OfType<WildBattleAttackStringEvent>().Any(),"cancel no PP/attack string");
   Check(p.Pokemon.Moves.SequenceEqual(s.Player.Pokemon.Moves),"cancel retains all PP");
   Check(p.LastUsedMove==(outcome==SourceObedienceOutcome.SelfHit?Id("TACKLE"):new MoveId("pret:MOVE_UNAVAILABLE")),"source OBEYS history");
   if(outcome==SourceObedienceOutcome.Nap)Check(p.Status is >=2 and <=5,"source nap duration");
   if(outcome==SourceObedienceOutcome.SelfHit)Check(p.Pokemon.CurrentHp<s.Player.Pokemon.CurrentHp&&r.Invocation!.Target==WildBattleSide.Player,"self-target actual body");
  }
 }
});
Test("earlier cancellers ability and PP checks prevent obedience draws",()=>{
 var cases=new[]{Single(Mon() with{Status=3}),Single(Mon() with{Flinched=true}),Single(Mon("SLAKOTH") with{TruantLoafing=true})};
 foreach(var s in cases){var r=ActSingle(s);Check(r.Succeeded,r.Fault??"blocked");Check(!r.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"blocked before obedience");}
 var empty=Mon();empty=empty with{Pokemon=empty.Pokemon with{Moves=empty.Pokemon.Moves.SetItem(0,empty.Pokemon.Moves[0] with{CurrentPp=0})}};
 var noPp=ActSingle(Single(empty));Check(noPp.Succeeded&&!noPp.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"no PP before obedience");
 var growl=Single(Mon(names:["GROWL"]));var sound=Mon("VOLTORB",50,99,"TACKLE");growl=growl with{Opponent=sound,CalledParty=growl.CalledParty! with{Opponent=[Member(sound)]}};
 var muted=ActSingle(growl);Check(muted.Succeeded,muted.Fault??"soundproof");Check(muted.Battle.Events.OfType<WildBattleAbilityPreventedEvent>().Any()&&!muted.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"soundproof precedes obedience");
});
Test("sleeping Snore ignores orders without called move or PP",()=>{
 var mon=Mon(names:["SNORE","TACKLE"]) with{Status=4};uint seed=Seed(SourceObedienceOutcome.IgnoreAsleep,mon);var r=ActSingle(Single(mon,seed));
 Check(r.Succeeded&&r.State!.Player.Status==3,"sleep countdown occurs once");Check(r.Battle.Trace.Length==1&&!r.Battle.Events.OfType<WildBattlePpCommandEvent>().Any(),"asleep branch only first obedience draw");
});
Test("Mew Deoxys share ordinary ownership badges and atomic context rules",()=>{
 foreach(string species in new[]{"MEW","DEOXYS"})
 {
  var owned=Single(Mon(species,50,42)) with{Obedience=Context(ot:"RED")};var own=ActSingle(owned);
  Check(own.Succeeded&&!own.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"owned legendary obeys without event metadata");
  var foreign=Single(Mon(species,50,43));var traded=ActSingle(foreign);
  Check(traded.Succeeded&&traded.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"foreign legendary uses ordinary level/badge decision");
  var badge=ActSingle(foreign with{Obedience=Context(badges:128)});
  Check(badge.Succeeded&&!badge.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"foreign legendary badge eight exemption");
 }
 var normal=Single();foreach(var s in new[]{normal with{Obedience=normal.Obedience! with{PlayerParty=[]}},normal with{SourceMechanicsRulesVersion=null},normal with{Rng=new(1,ulong.MaxValue)},normal with{Revision=long.MaxValue}})
 {var r=ActSingle(s);Check(!r.Succeeded&&ReferenceEquals(r.State,s)&&r.Battle.Events.IsEmpty&&r.Battle.Trace.IsEmpty,"invalid/exhausted action atomic "+r.Fault);}
 var frame=Four() with{Obedience=Context()};var f=Act(frame);Check(!f.Succeeded&&ReferenceEquals(f.State,frame)&&f.Trace.IsEmpty&&f.Events.IsEmpty,"four invalid context atomic");
});
Test("singles frame shares outcomes PP history and RNG values",()=>{
 foreach(var outcome in new[]{SourceObedienceOutcome.Nap,SourceObedienceOutcome.Loaf,SourceObedienceOutcome.SelfHit,SourceObedienceOutcome.AlternateMove})
 {
  var s=Single(seed:Seed(outcome));var expected=ActSingle(s);var imported=SinglesBattleAdapter.Import(catalog,s);Check(imported.Succeeded,imported.Fault??"import");
  var actual=Act(imported.Frame!);Check(actual.Succeeded,actual.Fault??"frame");var exported=SinglesBattleAdapter.Export(actual.State);Check(exported.Succeeded,exported.Fault??"export");
  Check(actual.State.Rng==expected.State!.Rng,"shared RNG state "+outcome);
  Check(Json(expected.State)==Json(exported.State),"shared state "+outcome+"\n"+Json(expected.State)+"\n"+Json(exported.State));
  Check(Json(expected.Battle.Events)==Json(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray()),"shared events "+outcome);
 }
});
Test("real public four-action turns consume disobedient action exactly once",()=>{
 var wanted=new HashSet<SourceObedienceOutcome>{SourceObedienceOutcome.Nap,SourceObedienceOutcome.SelfHit,SourceObedienceOutcome.AlternateMove};
 for(uint seed=0;seed<300&&wanted.Count>0;seed++)
 {
  var start=Four(seed);var choices=start.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Body!.Pokemon.Moves[0].Move==Id("SPLASH")||c.Body.Pokemon.Moves[0].Move==Id("AMNESIA")?c.Slot:new(c.Slot.Value^1))).ToImmutableArray();
  var r=SourceFourActionTurns.BeginActions(catalog,start,start.Revision,choices);Check(r.Succeeded,r.Fault??"begin");var events=r.Events.ToList();int steps=0;
  while(r.State.FourTurn is not null&&steps++<10){var prior=r;r=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);Check(r.Succeeded,r.Fault??"advance");events.AddRange(r.Events);if(steps==1){var stale=SourceFourActionTurns.Advance(catalog,r.State,prior.Continuation!.Value);Check(!stale.Succeeded&&ReferenceEquals(stale.State,r.State)&&stale.Events.IsEmpty&&stale.Trace.IsEmpty,"stale turn token");}}
  Check(r.CompletedCleanTurn&&r.State.Turn==1,"four complete turn");
  var obedience=events.OfType<SourceObedienceEvent>().Where(e=>e.Actor.Value==0).ToArray();Check(obedience.Length<=1,"one attempted disobedience");
  if(obedience.Length==1&&wanted.Remove(obedience[0].Decision.Outcome)){fullTurns++;selectedTurns.Add(new{layout="four",seed,outcome=obedience[0].Decision.Outcome.ToString(),steps,state=r.State,events});}
 }
 Check(wanted.Count==0,"four demonstrated nap/self/alternate");
});
Test("real public source-wild singles turns finish after nap self-hit and alternate",()=>{
 var wanted=new HashSet<SourceObedienceOutcome>{SourceObedienceOutcome.Nap,SourceObedienceOutcome.SelfHit,SourceObedienceOutcome.AlternateMove};
 for(uint seed=0;seed<300&&wanted.Count>0;seed++)
 {
  var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon()],0,Mon("SLOWBRO",100,99,"AMNESIA"),new(seed));Check(entry.Succeeded,entry.Fault??"source entry");
  var state=entry.State! with{Obedience=Context()};var r=WildBattleRules.ResolveMove(catalog,state,0);Check(r.Succeeded,r.Fault??"full source turn");
  Check(r.State!.SourceWild!.Turn is null&&r.State.Turn==1,"singles turn completes");
  var events=r.Events.OfType<WildBattleObedienceEvent>().ToArray();Check(events.Length<=1,"singles only one obedience branch");
  if(events.Length==1&&wanted.Remove(events[0].Decision.Outcome)){fullTurns++;selectedTurns.Add(new{layout="singles",seed,outcome=events[0].Decision.Outcome.ToString(),state=r.State,events=r.Events});}
 }
 Check(wanted.Count==0,"singles demonstrated nap/self/alternate");
});
Test("caller replacement and forced Struggle retain one cancellation owner",()=>{
 var caller=Mon(names:["METRONOME","TACKLE"]);var s=Single(caller,Seed(SourceObedienceOutcome.AlternateMove,caller));
 var called=WildBattleRules.ResolveCalledMoveAction(catalog,s,new(WildBattleSide.Player,0,Id("SURF")));
 Check(called.Succeeded,called.Fault??"supplied caller");Check(called.Invocation!.CurrentMove==Id("TACKLE")&&called.Invocation.PpSlot==1,"disobedient alternate replaces caller and supplied result");
 Check(!called.Battle.Events.OfType<WildBattleMoveUsedEvent>().Any(e=>e.Move==Id("SURF")),"old caller result never executes");
 Check(called.Battle.Trace.Count(t=>t.Purpose.EndsWith("obedience-check"))==1,"caller checked once");
 var reentry=Mon(names:["TACKLE","SLEEP_TALK"]);var redirected=ActSingle(Single(reentry,Seed(SourceObedienceOutcome.AlternateMove,reentry)));
 Check(redirected.Succeeded&&redirected.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="user-awake"),"alternate caller actual awake script");
 Check(redirected.Battle.Trace.Count(t=>t.Purpose.EndsWith("obedience-check"))==1&&redirected.State!.Player.Pokemon.Moves[1].CurrentPp==9,"alternate caller no repeated canceller and own PP");
 var exhausted=Mon();exhausted=exhausted with{Pokemon=exhausted.Pokemon with{Moves=exhausted.Pokemon.Moves.Select(m=>m with{CurrentPp=0}).ToImmutableArray()}};
 bool disobeyed=false;
 for(uint seed=0;seed<30&&!disobeyed;seed++){
  var entry=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[exhausted],0,Mon("SLOWBRO",100,99,"AMNESIA"),new(seed));Check(entry.Succeeded,"exhausted entry");
  var r=WildBattleRules.ResolveMove(catalog,entry.State! with{Obedience=Context()},WildBattleRules.StruggleSlot);Check(r.Succeeded,r.Fault??"forced Struggle");
  Check(r.State!.Player.Pokemon.Moves.All(m=>m.CurrentPp==0),"forced Struggle does not charge real slot");
  if(r.Events.OfType<WildBattleObedienceEvent>().FirstOrDefault() is{} e){disobeyed=true;Check(e.AttemptedMove==WildBattleRules.StruggleMove&&e.Decision.Outcome!=SourceObedienceOutcome.AlternateMove,"Struggle checks obedience, cannot choose unusable alternate");}
 }
 Check(disobeyed,"Struggle disobedience branch found");
 var locked=Mon(names:["ROLLOUT"]) with{CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Rollout,Id("ROLLOUT"),0,Id("ROLLOUT"),WildBattleSide.Opponent,3)}};
 var continuation=WildBattleRules.ResolveCalledMoveContinuation(catalog,Single(locked),WildBattleSide.Player);
 Check(continuation.Succeeded,continuation.Fault??"locked continuation");Check(!continuation.Battle.Trace.Any(t=>t.Purpose.Contains("obedience")),"multi-turn bypass exactly source");
});
Test("diagnostic serialization and alternate targeting preserve authoritative facts",()=>{
 var mon=Mon(names:["TACKLE","GROWL"]);var original=Single(mon,Seed(SourceObedienceOutcome.AlternateMove,mon));
 var copy=JsonSerializer.Deserialize<WildBattleState>(Json(original))!;Check(Json(ActSingle(original))==Json(ActSingle(copy)),"snapshot replay");
 var f=Four(Seed(SourceObedienceOutcome.AlternateMove,mon),mon);var r=Act(f);Check(r.Succeeded,r.Fault??"four alternate selected");
 Check(r.Events.OfType<SourceObedienceEvent>().Single().Decision.Outcome==SourceObedienceOutcome.AlternateMove,"four alternate");
 Check(r.Events.OfType<SourcePpCommandEvent>().Count()==2&&r.State.Get(new(0)).Body!.Pokemon.Moves[1].CurrentPp==catalog.Moves[Id("GROWL")].Pp-1,"alternate spread exactly two passes and one PP");
 Check(r.Events.OfType<SourceStatChangedEvent>().Select(e=>e.Target.Value).Order().SequenceEqual(new[]{1,3}),"both actual opposing targets");
});
Test("alternate Baton Pass retains original action ownership across choice",()=>{
 var mon=Mon(names:["TACKLE","BATON_PASS"]);uint seed=Seed(SourceObedienceOutcome.AlternateMove,mon);var reserve=Mon(owner:42,names:["SPLASH"]);
 var s=Single(mon,seed);s=s with{CalledParty=s.CalledParty! with{Player=s.CalledParty.Player.Add(Member(reserve))},Obedience=Context(2)};
 var pass=ActSingle(s);Check(pass.Succeeded,pass.Fault??"alternate Baton Pass");
 Check(pass.State!.CalledParty!.PendingSwitch?.Invocation?.PpSlot==1,"alternate Baton PP position");
 Check(WildBattleRules.ValidateCalledMoveActionState(catalog,pass.State) is null,"pending singles context valid");
 var switched=WildBattleRules.ResolveCalledPartySwitch(catalog,pass.State,new(WildBattleSide.Player,1));Check(switched.Succeeded,switched.Fault??"resume singles Baton");
 Check(switched.State!.CalledParty!.PlayerActiveIndex==1&&switched.State.Obedience==s.Obedience,"replacement keeps contextual identity facts");
 bool found=false;
 for(uint candidate=0;candidate<100&&!found;candidate++){
  var f=Four(candidate,mon);f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(Member(reserve))}),Obedience=Context(3)};
  var choices=f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value==0?new(1):c.Slot)).ToImmutableArray();
  var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices);Check(r.Succeeded,r.Fault??"Baton begin");int steps=0;
  while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions&&steps++<4){r=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);Check(r.Succeeded,r.Fault??"Baton advance");}
  if(r.State.FourTurn?.Phase!=SourceFourActionPhase.ActionSwitchPending)continue;
  found=true;Check(r.State.PendingSwitches.Single().Invocation!.PpSlot==1,"four actual alternate PP owner");
  var resumed=SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2));Check(resumed.Succeeded,resumed.Fault??"four resume");
  Check(resumed.State.Get(new(0)).Member!.Value.Index==2,"actual reserve enters");
  var prior=r.State.Get(new(0)).Occupant;Check(resumed.State.Get(new(0)).Occupant!=prior,"replacement occupant cannot inherit old action");
 }
 Check(found,"four alternate Baton boundary reached");
});
Test("alternate chosen move owns Mirror Move eligibility",()=>{
 var mon=Mon(names:["SPLASH","TACKLE"]);bool found=false;
 for(uint seed=0;seed<100&&!found;seed++){
  var r=ActSingle(Single(mon,seed));Check(r.Succeeded,r.Fault??"mirror alternate");
  if(r.Battle.Events.OfType<WildBattleObedienceEvent>().FirstOrDefault()?.Decision.Outcome!=SourceObedienceOutcome.AlternateMove||!r.Battle.Events.OfType<WildBattleHpChangedEvent>().Any())continue;
  found=true;Check(r.Invocation!.OriginalChosenMove==Id("SPLASH")&&r.Invocation.ChosenMove==Id("TACKLE"),"original input remains diagnostic");
  Check(r.State!.Opponent.SourceMoveHistory!.LastTakenMove==Id("TACKLE")&&r.State.Opponent.SourceMoveHistory.LastTakenFromPlayer==Id("TACKLE"),"actual alternate owns mirror history");
 }
 Check(found,"mirrorable alternate hit reached");
});
Test("alternate non-Ghost Curse uses raw target draws and pre-retarget Pressure",()=>{
 foreach(bool capped in new[]{false,true}){
  var mon=Mon(names:["SPLASH","CURSE"]);uint seed=Seed(SourceObedienceOutcome.AlternateMove,mon);
  if(capped)mon=mon with{SpeedStage=0,AttackStage=12,DefenseStage=12};
  var s=Single(mon,seed);var target=Mon("DUSCLOPS",100,99,"AMNESIA");s=s with{Opponent=target,CalledParty=s.CalledParty! with{Opponent=[Member(target)]}};
  var r=ActSingle(s);Check(r.Succeeded,r.Fault??"alternate Curse");Check(r.Battle.Trace.Any(t=>t.Purpose.EndsWith("obedience-target")),"raw selected target rejection RNG");
  Check(r.State!.Player.Pokemon.Moves[1].CurrentPp==catalog.Moves[Id("CURSE")].Pp-2,"Pressure before Curse retarget");
  Check(r.Invocation!.Target==(capped?WildBattleSide.Opponent:WildBattleSide.Player),"source retarget after stat-cap guard");
  var imported=SinglesBattleAdapter.Import(catalog,s);Check(imported.Succeeded,"Curse import");var actual=Act(imported.Frame!);Check(actual.Succeeded,actual.Fault??"Curse frame");
  Check(actual.State.Rng==r.State.Rng&&Json(SinglesBattleAdapter.Export(actual.State).State)==Json(r.State),"Curse exact shared frame state/RNG");
 }
});
Test("Marvel Scale defense participates in canonical obedience self-damage",()=>{
 var mon=Mon("MILOTIC") with{Status=WildBattleStatus.Poison,DefenseStage=7};var r=ActSingle(Single(mon,Seed(SourceObedienceOutcome.SelfHit,mon)));Check(r.Succeeded,r.Fault??"Milotic self-hit");
 var hit=r.Battle.Events.OfType<WildBattleConfusionSelfHitEvent>().Single();
 int scaledDefense=(mon.Pokemon.Stats.Defense*150/100)*3/2;
 int unscaledDefense=mon.Pokemon.Stats.Defense*3/2;
 int Expected(int defense)=>Math.Max(1,(Math.Max(1,mon.Pokemon.Stats.Attack*40*(2*mon.Pokemon.Level/5+2)/defense/50)+2)*hit.RandomPercent/100);
 Check(hit.Damage==Expected(scaledDefense)&&hit.Damage<Expected(unscaledDefense),"source defense modifier precedes stat stage and variance");
 var imported=SinglesBattleAdapter.Import(catalog,Single(mon,Seed(SourceObedienceOutcome.SelfHit,mon)));var shared=Act(imported.Frame!);Check(shared.Succeeded,shared.Fault??"Milotic frame");
 Check(shared.Events.OfType<SourceConfusionSelfHitEvent>().Single().Damage==hit.Damage,"same canonical self-damage owner");
});
Test("fateful acquisition metadata round-trips without influencing obedience",()=>{
 foreach(string species in new[]{"MEW","DEOXYS"})foreach(uint trainer in new uint[]{42,43}){
  var mon=Mon(species,50,trainer);string ot=trainer==42?"RED":"BLUE";
  var owned=new PartyPokemon(1,mon.Pokemon,species,ot,0,2,"MAPSEC_ROUTE_1",50,4,"ITEM_POKE_BALL","ITEM_NONE");
  string legacy=Json(owned);Check(!legacy.Contains("ModernFatefulEncounter",StringComparison.Ordinal),"default metadata omitted from old JSON shape");
  var legacyRoundTrip=JsonSerializer.Deserialize<PartyPokemon>(legacy)!;Check(!legacyRoundTrip.ModernFatefulEncounter&&Json(legacyRoundTrip)==legacy,"old JSON remains exact and defaults false");
  var marked=owned with{ModernFatefulEncounter=true};string markedJson=Json(marked);var restored=JsonSerializer.Deserialize<PartyPokemon>(markedJson)!;
  Check(restored.ModernFatefulEncounter&&Json(restored)==markedJson,"true metadata retained in JSON");
  Check((restored with{Nickname="TEST"}).ModernFatefulEncounter,"ordinary with copy retains acquisition metadata");
  WildBattleCalledMoveResult FromOwned(PartyPokemon value)=>ActSingle(Single(new(value.Pokemon,HeldItem:value.HeldItem,Status:value.Status),11) with{Obedience=Context(ot:value.OriginalTrainerName)});
  var ordinary=FromOwned(legacyRoundTrip);var fateful=FromOwned(restored);Check(ordinary.Succeeded&&fateful.Succeeded,"metadata comparison admitted");
  Check(Json(ordinary)==Json(fateful),species+" owner "+trainer+" metadata ignored by all obedience state/events/RNG");
 }
});
Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"public-turns.json"),Json(selectedTurns));
var summary=new{suite="source-obedience",checks,failures,vectors,intentionalDeviations,fullTurns,runtime=Environment.Version.ToString(),results};File.WriteAllText(Path.Combine(args[1],"result.json"),Json(summary));Console.WriteLine(Json(summary));
return failures==0&&vectors==60&&intentionalDeviations==2&&fullTurns==6?0:1;
