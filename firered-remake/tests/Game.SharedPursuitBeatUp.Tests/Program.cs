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
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var snapshots=new List<object>();
foreach(string name in new[]{"PURSUIT","BEAT_UP"})foreach(uint seed in new uint[]{0,31,127})foreach(bool called in new[]{false,true}){
 var actor=Mon("SNEASEL",50,called?"METRONOME":name);var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 var party=new[]{actor,Mon("CHARIZARD",40,"DEFENSE_CURL"),Mon("PIKACHU",40,"DEFENSE_CURL") with{Status=WildBattleStatus.Poison}};
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,party.ToImmutableArray(),0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 if(called){var result=WildBattleRules.ResolveCalledMoveAction(catalog,start.State!,new(WildBattleSide.Player,0,Id(name)));if(!result.Battle.Succeeded)throw new Exception("called:"+result.Battle.Fault);snapshots.Add(new{name,seed,called,result=(object)result,hash=Hash(result)});}
 else{var result=WildBattleRules.ResolveMove(catalog,start.State!,0);if(!result.Succeeded)throw new Exception("turn:"+result.Fault);snapshots.Add(new{name,seed,called,result=(object)result,hash=Hash(result)});}
}
foreach(uint seed in new uint[]{0,31,127})foreach(string state in new[]{"normal","freeze","sleep","paralysis","substitute","ko"}){
 var actor=Mon("SNEASEL",60,"PURSUIT");actor=state switch{"freeze"=>actor with{Status=WildBattleStatus.Freeze},"sleep"=>actor with{Status=3},"paralysis"=>actor with{Status=WildBattleStatus.Paralysis},_=>actor};
 var target=Mon("SNORLAX",50,"DEFENSE_CURL");if(state=="ko")target=Hp(target,1);
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[target,Mon("CHARIZARD",40,"DEFENSE_CURL")],0,actor,new(seed));if(!start.Succeeded)throw new Exception("switch-start:"+start.Fault);
 var input=start.State!;if(state=="substitute")input=input with{Player=input.Player with{CalledVolatile=new(){SubstituteHp=10}}};
 var result=WildBattleRules.SwitchSourceWildPlayer(catalog,input,1);if(!result.Succeeded)throw new Exception("switch:"+state+":"+result.Fault);snapshots.Add(new{name="switch",seed,state,result=(object)result,hash=Hash(result)});
}
if(args.Contains("capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse overwrite");
 File.WriteAllText(args[1],Json(new{baseline="bfc976e21084ba2ad0b7a2f9da0652ccdab6c7e3",snapshots})+"\n");Console.WriteLine("Captured "+snapshots.Count+" complete results");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();var turnsEvidence=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[1])!,"singles-after.json"),Json(new{snapshots})+"\n");
Test("frozen complete canonical results except source-proven freeze guard",()=>{
 using var before=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before.json")));
 var old=before.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==30,"nonempty baseline");
 for(int i=0;i<old.Length;i++) { var current=JsonSerializer.SerializeToElement(snapshots[i]);bool corrected=old[i].TryGetProperty("state",out var condition)&&condition.GetString()=="freeze";
  Check(corrected || old[i].GetProperty("hash").GetString()==current.GetProperty("hash").GetString(),"full result mismatch "+i);
 }
 Check(Environment.Version.Major==10,"actual runtime10");
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,WildBattleTransformationRules.PersistentStatus(m));
SourceRewardMetadata Facts(int index)=>new(new(BattleSide.Player,index),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,uint seed=31){
 var m=supplied??Enumerable.Range(0,4).Select(_=>Mon("SNORLAX",100,"DEFENSE_CURL")).ToArray();
 var p=new[]{Member(m[0]),Member(m[2]),Member(Mon("CHARIZARD",50,"DEFENSE_CURL"))}.ToImmutableArray();
 var o=new[]{Member(m[1]),Member(m[3]),Member(Mon("CHARIZARD",50,"DEFENSE_CURL"))}.ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,p),new(BattleSide.Opponent,o)],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
 Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,p.Length).Select(Facts).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant m){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(m))})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int? switcher=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 if(c.Slot.Value==switcher)return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Switch,Incoming:new(c.Slot.Side,2));
 var m=catalog.Moves[c.Body!.Pokemon.Moves[0].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(targets?[c.Slot.Value]??(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1)));
}).ToImmutableArray();
SourceTurnContinuation Token(SourceBattleFrame f)=>new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn!.Phase);
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f,int? switcher=null,int[]? targets=null)=>Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,switcher,targets)));
SourceFourActionResult Next(SourceFourActionResult r){var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var actual=SourceFourActionTurns.Advance(catalog,r.State,Token(r.State));var replay=SourceFourActionTurns.Advance(catalog,copy,Token(copy));Check(Json(actual)==Json(replay),"serialized continuation identical");return Ok(actual);}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Drain(SourceFourActionResult r,string label){var events=r.Events.ToList();var trace=r.Trace.ToList();int steps=0;
 while(r.State.FourTurn is not null&&r.State.Outcome==WildBattleOutcome.Ongoing&&steps++<30){
  if(r.State.Rewards?.Pending is {} learning){var candidate=learning.Moves[0];r=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(learning.Recipient,candidate.Level,candidate.Move,null)));}
  else if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var p=r.State.PendingSwitches[0];r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,Token(r.State),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0])));}
  else r=Next(r);events.AddRange(r.Events);trace.AddRange(r.Trace);
 }
 Check(r.CompletedCleanTurn||r.State.Outcome!=WildBattleOutcome.Ongoing,"complete bounded turn");turnsEvidence.Add(new{label,state=r.State,events,trace,hash=Hash(new{state=r.State,events,trace})});return(r.State,events,trace);
}
Test("Beat Up real selected target and all four actors complete turns",()=>{
 for(int actor=0;actor<4;actor++){var f=Put(Frame(),actor,Mon("SNEASEL",60,"BEAT_UP"));int target=(actor^1)^2;var targets=Enumerable.Range(0,4).ToArray();targets[actor]=target;
 var r=Drain(Begin(f,targets:targets),"Beat Up actor "+actor);
 Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Where(e=>e.Side==new BattlerSlot(actor).Side).Select(e=>e.PartyIndex).SequenceEqual(new[]{0,1,2}),"real party order");
 Check(r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id("BEAT_UP")).All(e=>e.Actor.Value==actor&&e.Target.Value==target),"real selected target");
 Check(r.Events.OfType<SourcePpCommandEvent>().Count(e=>e.CurrentMove==Id("BEAT_UP"))==1,"one PP command");
 Check(!r.Trace.Any(t=>t.Purpose.EndsWith("secondary-effect")),"no secondary RNG");
 }
});
Test("Beat Up party eligibility status faint egg and base-stat formula",()=>{
 var f=Put(Frame(),0,Mon("SNEASEL",60,"BEAT_UP"));var party=f.Party(BattleSide.Player);
 var members=party.Members.Add(Member(Mon("PIKACHU",40,"DEFENSE_CURL") with{Status=WildBattleStatus.Burn})).Add(Member(Hp(Mon("PIDGEY",40,"DEFENSE_CURL"),0))).Add(Member(Mon("RATTATA",40,"DEFENSE_CURL")) with{IsEgg=true});
 f=f with{Parties=f.Parties.SetItem(0,party with{Members=members}),Rewards=f.Rewards! with{Metadata=Enumerable.Range(0,members.Length).Select(Facts).ToImmutableArray()}};var r=Drain(Begin(f),"eligible party");
 Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Select(e=>e.PartyIndex).SequenceEqual(new[]{0,1,2}),"excluded status faint egg");
 var contributors=r.Events.OfType<SourceCalledBeatUpMemberEvent>().ToArray();var hits=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id("BEAT_UP")).ToArray();
 for(int i=0;i<hits.Length;i++){var member=members[contributors[i].PartyIndex];int basic=WildBattleSourcePartyEffects.BeatUpBaseDamage(catalog,member,f.Slots[1].Body!,10);Check(hits[i].Damage==Math.Max(1,basic*(hits[i].Critical?2:1)*hits[i].RandomPercent/100),"GenIII base stat hit "+i);}
});
Test("Beat Up Substitute per-hit items no contact and KO stop",()=>{
 var f=Put(Frame(),0,Hp(Mon("SNEASEL",60,"BEAT_UP") with{HeldItem="ITEM_SHELL_BELL"},30));f=Put(f,1,Mon("SNORLAX",100,"DEFENSE_CURL") with{CalledVolatile=new(){SubstituteHp=1}});
 var r=Drain(Begin(f),"Beat Up Substitute Shell Bell");Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Count()==3,"continued after doll");Check(r.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Move==Id("BEAT_UP"))==2,"only remaining hits reach body");
 Check(r.State.Slots[0].Body!.Pokemon.CurrentHp>30,"Shell Bell reached per-hit moveend");
 f=Put(Frame(),0,Mon("SNEASEL",60,"BEAT_UP"));f=Put(f,1,Hp(Mon("SNORLAX",100,"DEFENSE_CURL"),1));r=Drain(Begin(f),"Beat Up KO reward");Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Count()==1,"KO stops contributors");Check(r.Events.OfType<SourceOpponentRewardCompletedEvent>().Count()==1,"one reward");
 f=Put(Frame(),0,Mon("SNEASEL",60,"BEAT_UP"));f=Put(f,1,Mon("PIKACHU",100,"DEFENSE_CURL"));r=Drain(Begin(f),"Beat Up no contact");Check(!r.Trace.Any(t=>t.Purpose.EndsWith("static")),"no invented contact flag");
});
Test("Pursuit visits right then left and consumes original queued actions",()=>{
 for(int switcher=0;switcher<4;switcher++){var f=Frame();int left=(switcher^1)&1,right=left|2;f=Put(f,left,Mon("FEAROW",40,"PURSUIT"));f=Put(f,right,Mon("FEAROW",30,"PURSUIT"));var targets=Enumerable.Range(0,4).ToArray();targets[left]=targets[right]=switcher;
 var r=Drain(Begin(f,switcher,targets),"Pursuit switch "+switcher);Check(r.Events.OfType<SourcePursuitInterceptedEvent>().Select(e=>e.Actor.Value).SequenceEqual(new[]{right,left}),"original right-left order");
 Check(r.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("PURSUIT"))==2,"no replay queued Pursuit");
 Check(r.State.Slots[switcher].Member!.Value.Index==2,"actual reserved entrant");Check(r.State.Slots[left].Body!.LastUsedMove is null&&r.State.Slots[right].Body!.LastUsedMove is null,"interrupt excludes last-move update");
 Check(r.Trace.Count(t=>t.Purpose.EndsWith("critical"))==2&&!r.Trace.Any(t=>t.Purpose.EndsWith("accuracy")||t.Purpose.EndsWith("secondary-effect")),"special script RNG only");}
});
Test("Pursuit chosen target guard sleep freeze truant versus paralysis confusion flinch",()=>{
 foreach(string condition in new[]{"wrong-target","sleep","freeze","paralysis","confusion","flinch"}){
 var f=Frame();var p=Mon("FEAROW",40,"PURSUIT");p=condition switch{"sleep"=>p with{Status=3},"freeze"=>p with{Status=WildBattleStatus.Freeze},"paralysis"=>p with{Status=WildBattleStatus.Paralysis},"confusion"=>p with{ConfusionTurnsRemaining=3},"flinch"=>p with{Flinched=true},_=>p};f=Put(f,3,p);var targets=new[]{0,1,2,condition=="wrong-target"?2:0};
 var first=Next(Begin(f,0,targets));bool eligible=condition is "paralysis" or "confusion" or "flinch";Check(first.Events.OfType<SourcePursuitInterceptedEvent>().Any()==eligible,condition+" interception gate");
 if(eligible)Check(!first.Trace.Any(t=>t.Purpose.EndsWith("paralysis")||t.Purpose.EndsWith("confusion")),"no attackcanceler RNG");Drain(first,"Pursuit guard "+condition);
 }
});
Test("Pursuit KO pauses actual outgoing EXP then resumes reserved switch",()=>{
 var f=Put(Frame(),1,Hp(Mon("SNORLAX",100,"DEFENSE_CURL"),1));f=Put(f,2,Mon("FEAROW",60,"PURSUIT"));var targets=new[]{0,1,1,3};var begun=Begin(f,1,targets);var pause=Next(begun);
 Check(pause.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&pause.State.NextAction==0,"pause within current switch action");Check(pause.State.Slots[1].Member!.Value.Index==0&&pause.State.Slots[1].Body!.Pokemon.CurrentHp==0,"outgoing retained for actual EXP");
 var stale=SourceFourActionTurns.Advance(catalog,pause.State,Token(begun.State));Check(!stale.Succeeded&&ReferenceEquals(stale.State,pause.State)&&stale.Events.IsEmpty&&stale.Trace.IsEmpty,"stale boundary atomic");
 var r=Drain(pause,"Pursuit KO EXP switch resume");Check(r.State.Slots[1].Member!.Value.Index==2,"same reserved member entered after reward");Check(r.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("PURSUIT"))==1,"KO Pursuit never replayed");
 var reward=r.Events.OfType<SourceRewardAppliedEvent>().First();var entry=r.Events.OfType<SourceCalledPartySwitchedEvent>().First(e=>e.Side.Value==1);Check(reward.Sequence<entry.Sequence&&reward.Defeated.Member.Index==0,"EXP before entry for outgoing identity");
});
Test("ordinary and Mirror-called Pursuit run the normal script without switch interception",()=>{
 for(int actor=0;actor<4;actor++){
  var f=Put(Frame(),actor,Mon("FEAROW",60,"PURSUIT"));var targets=Enumerable.Range(0,4).ToArray();targets[actor]=(actor^1)^2;
  var r=Drain(Begin(f,targets:targets),"ordinary Pursuit "+actor);Check(!r.Events.OfType<SourcePursuitInterceptedEvent>().Any(),"ordinary not intercept");
  Check(r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("PURSUIT")).Target.Value==targets[actor],"normal actual target");
  Check(r.Trace.Count(t=>t.Purpose.EndsWith("accuracy"))==1&&r.Trace.Count(t=>t.Purpose.EndsWith("secondary-effect"))==1,"normal script RNG");
 }
 var call=Put(Frame(),3,Mon("FEAROW",60,"MIRROR_MOVE"));var cell=call.Slots[3];call=call with{Slots=call.Slots.SetItem(3,cell with{Registers=cell.Registers with{History=new(){LastTakenMove=Id("PURSUIT"),LastTakenFrom=[null,null,Id("PURSUIT"),null]}}})};
 var first=Next(Begin(call,0,new[]{0,1,2,0}));Check(!first.Events.OfType<SourcePursuitInterceptedEvent>().Any(),"chosen Mirror Move cannot preempt switch");var result=Drain(first,"Mirror called ordinary Pursuit after switch");
 Check(result.Events.OfType<SourceCalledMoveTransitionEvent>().Any(e=>e.Invocation.CurrentMove==Id("PURSUIT")),"actual caller transition");Check(result.State.Slots[3].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("MIRROR_MOVE")].Pp-1,"caller owns PP");
});
Test("Beat Up Helping Hand and Endure per-hit results retain real party contributors",()=>{
 var f=Put(Frame(),0,Mon("SNEASEL",60,"BEAT_UP"));f=Put(f,2,Mon("EEVEE",60,"HELPING_HAND"));var r=Drain(Begin(f,targets:new[]{1,1,0,3}),"Helping Hand Beat Up");
 var contributors=r.Events.OfType<SourceCalledBeatUpMemberEvent>().ToArray();var hits=r.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id("BEAT_UP")).ToArray();
 Check(hits.Length==3,"three Helping Hand contributions");for(int i=0;i<hits.Length;i++){int basis=WildBattleSourcePartyEffects.BeatUpBaseDamage(catalog,f.Party(BattleSide.Player).Members[contributors[i].PartyIndex],f.Slots[1].Body!,10)*15/10;Check(hits[i].Damage==Math.Max(1,basis*(hits[i].Critical?2:1)*hits[i].RandomPercent/100),"Helping Hand before critical/random");}
 f=Put(Frame(),0,Mon("SNEASEL",60,"BEAT_UP"));f=Put(f,1,Hp(Mon("SNORLAX",100,"ENDURE") with{HeldItem="ITEM_FOCUS_BAND"},1));r=Drain(Begin(f),"Beat Up all hits Endure Focus Band");
 Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Count()==3&&r.Trace.Count(t=>t.Purpose.EndsWith("focus-band"))==3,"all contributors and per-hit Focus Band draws at one HP");Check(r.State.Slots[1].Body!.Pokemon.CurrentHp==1,"Endure survives all hits");
});
Test("Pursuit Substitute contact and Shell Bell respect the short moveend slice",()=>{
 var f=Put(Frame(),3,Hp(Mon("FEAROW",60,"PURSUIT") with{HeldItem="ITEM_SHELL_BELL"},30));f=Put(f,0,Mon("PIKACHU",100,"DEFENSE_CURL") with{CalledVolatile=new(){SubstituteHp=1}});
 var first=Next(Begin(f,0,new[]{0,1,2,0}));Check(first.Events.OfType<SourcePursuitInterceptedEvent>().Count()==1,"real intercept");Check(first.State.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp==f.Slots[0].Body!.Pokemon.CurrentHp,"doll absorbs");Check(first.State.Slots[3].Body!.Pokemon.CurrentHp==30,"switch pursuit excludes Shell Bell");Check(!first.Trace.Any(t=>t.Purpose.EndsWith("static")),"doll blocks contact");Drain(first,"Pursuit short moveend doll");
 f=Put(Frame(),3,Hp(Mon("FEAROW",60,"PURSUIT") with{HeldItem="ITEM_SHELL_BELL"},30));f=Put(f,0,Mon("PIKACHU",100,"DEFENSE_CURL"));first=Next(Begin(f,0,new[]{0,1,2,0}));Check(first.Trace.Count(t=>t.Purpose.EndsWith("static"))==1,"source dark Pursuit contact reaches ability");Check(first.State.Slots[3].Body!.Pokemon.CurrentHp==30,"no Shell Bell on body hit");Drain(first,"Pursuit short moveend contact");
});
Test("original C command outputs agree with shared production formulas and guards",()=>{
 using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[1])!,"source-fixed","vectors.json")));
 var target=Mon("SNORLAX",100,"DEFENSE_CURL");var member=Member(Mon("SNEASEL",60,"BEAT_UP"));
 foreach(var row in data.RootElement.GetProperty("beatUp").EnumerateArray()){
  int attack=row.GetProperty("attack").GetInt32(),defense=row.GetProperty("defense").GetInt32(),level=row.GetProperty("level").GetInt32();
  var altered=catalog with{Species=catalog.Species.SetItem(member.Pokemon.Species,catalog.Species[member.Pokemon.Species] with{BaseStats=catalog.Species[member.Pokemon.Species].BaseStats with{Attack=attack}}).SetItem(target.Pokemon.Species,catalog.Species[target.Pokemon.Species] with{BaseStats=catalog.Species[target.Pokemon.Species].BaseStats with{Defense=defense}})};
  int damage=WildBattleSourcePartyEffects.BeatUpBaseDamage(altered,member with{Pokemon=member.Pokemon with{Level=level}},target,10);if(row.GetProperty("helping").GetInt32()!=0)damage=damage*15/10;Check(damage==row.GetProperty("damage").GetInt32(),"original C base stat damage");
 }
 var eligible=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourcePursuitRules")!.GetMethod("Eligible",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 foreach(var row in data.RootElement.GetProperty("pursuit").EnumerateArray()){
  int state=row.GetProperty("state").GetInt32();var actor=Mon("FEAROW",60,"PURSUIT") with{Status=state==1?3u:state==2?WildBattleStatus.Freeze:state==3?WildBattleStatus.Paralysis:0u,TruantLoafing=state==4};
  bool actual=(bool)eligible.Invoke(null,[actor,state==5?Hp(target,0):target])!&&row.GetProperty("matched").GetInt32()!=0;
  Check(actual==(row.GetProperty("accepted").GetInt32()!=0),"original C Pursuit guard");
 }
});
Test("Pursuit contact recoil KO cancels pursuer while outgoing switch still completes",()=>{
 var f=Put(Frame(),0,Mon("SHARPEDO",100,"FOCUS_ENERGY"));f=Put(f,3,Hp(Mon("FEAROW",60,"PURSUIT"),1));
 var first=Next(Begin(f,0,new[]{0,1,2,0}));Check(first.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"opponent contact KO reaches EXP owner");
 Check(first.State.Slots[0].Member!.Value.Index==0,"switch pending during contact reward");Check(first.Events.OfType<SourceFaintedEvent>().Single().Side.Value==3,"real pursuer fainted");
 var r=Drain(first,"Pursuit Rough Skin KO");Check(r.State.Slots[0].Member!.Value.Index==2,"outgoing reserved entry completed");Check(r.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("PURSUIT"))==1,"fainted pursuer cannot replay");
});
Test("Pursuit cannot retarget another simultaneous voluntary switch",()=>{
 var f=Put(Frame(),1,Mon("FEAROW",60,"PURSUIT"));var p=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,p with{Members=p.Members.Add(Member(Mon("PIDGEOT",50,"AGILITY")))}),Rewards=f.Rewards! with{Metadata=Enumerable.Range(0,4).Select(Facts).ToImmutableArray()}};
 var selections=Choices(f,0,new[]{0,2,2,3}).Select(c=>c.Actor.Slot.Value==2?new SourceActionSelection(c.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Player,3)):c).ToImmutableArray();
 var begun=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,selections));var first=Next(begun);Check(!first.Events.OfType<SourcePursuitInterceptedEvent>().Any(),"left outgoing is not chosen target");
 var r=Drain(first,"two voluntary switches target binding");Check(r.Events.OfType<SourcePursuitInterceptedEvent>().Single().SwitchingSide.Value==2,"only real chosen outgoing intercepted");Check(r.State.Slots[0].Member!.Value.Index==2&&r.State.Slots[2].Member!.Value.Index==3,"both reservations honored");
});
Test("first Pursuit KO leaves the other Pursuit queued for the actual incoming member",()=>{
 var f=Put(Frame(),0,Hp(Mon("SNORLAX",100,"DEFENSE_CURL"),1));f=Put(f,1,Mon("FEAROW",40,"PURSUIT"));f=Put(f,3,Mon("FEAROW",60,"PURSUIT"));
 var first=Next(Begin(f,0,new[]{0,0,2,0}));Check(first.Events.OfType<SourcePursuitInterceptedEvent>().Select(e=>e.Actor.Value).SequenceEqual(new[]{3}),"right KO stops second interruption");
 var r=Drain(first,"Pursuit right KO left normal entrant hit");Check(r.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("PURSUIT"))==2,"both original actions consumed exactly once");
 var switched=r.Events.OfType<SourceCalledPartySwitchedEvent>().First(e=>e.Side.Value==0);var leftHit=r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("PURSUIT")&&e.Actor.Value==1);Check(leftHit.Sequence>switched.Sequence&&leftHit.Target.Value==0,"left normal hit reaches actual entrant");
});
Test("Beat Up called through Mirror Move retains actual caller PP and party owner",()=>{
 var f=Put(Frame(),2,Mon("FEAROW",60,"MIRROR_MOVE"));var c=f.Slots[2];f=f with{Slots=f.Slots.SetItem(2,c with{Registers=c.Registers with{History=new(){LastTakenMove=Id("BEAT_UP")}}})};
 var r=Drain(Begin(f,targets:new[]{0,1,3,3}),"Mirror called Beat Up");Check(r.Events.OfType<SourceCalledMoveTransitionEvent>().Any(e=>e.Invocation.CurrentMove==Id("BEAT_UP")),"real caller transition");
 Check(r.Events.OfType<SourceCalledBeatUpMemberEvent>().Select(e=>e.PartyIndex).SequenceEqual(new[]{0,1,2}),"caller side party order");
 Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("MIRROR_MOVE")].Pp-1,"only caller slot PP");
});
File.WriteAllText(args[1],Json(new{suite="Game.SharedPursuitBeatUp.Tests",runtime=Environment.Version.ToString(),groups,checks,failures,results})+"\n");
File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[1])!,"turns.json"),Json(turnsEvidence)+"\n");
return failures==0?0:1;
