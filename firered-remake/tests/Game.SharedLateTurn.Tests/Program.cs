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

List<WildBattleCalledMoveResult> PressureCases(){
 var records=new List<WildBattleCalledMoveResult>();
 foreach(bool canonical in new[]{true})foreach(bool active in new[]{false,true}){
  var actor=Mon("SNORLAX",100,"DEFENSE_CURL","PERISH_SONG");var foe=Mon("DUSCLOPS",100,"DEFENSE_CURL");
  if(active){actor=actor with{CalledVolatile=new(){PerishSongTimer=1}};foe=foe with{CalledVolatile=new(){PerishSongTimer=1}};}
  var state=new WildBattleState(actor,foe,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
  var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,1));if(!r.Succeeded)throw new Exception("pressure fixture:"+r.Fault);records.Add(r);
 }return records;
}
if(args.Contains("--capture-pressure")){
 if(File.Exists(args[1]))throw new Exception("Immutable pressure baseline already exists");File.WriteAllText(args[1],Json(PressureCases())+"\n");return 0;
}

List<WildBattleCalledMoveResult> BideCases(){
 var records=new List<WildBattleCalledMoveResult>();
 foreach(bool paralyzed in new[]{false,true})foreach(int remaining in new[]{2,1}){
  var actor=Mon("SNORLAX",100,"BIDE");var foe=Mon("SNORLAX",100,"DEFENSE_CURL");
  actor=actor with{Status=paralyzed?WildBattleStatus.Paralysis:0,CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Bide,Id("BIDE"),0,Id("BIDE"),WildBattleSide.Player,remaining){StoredDamage=40,StoredDamageSource=WildBattleSide.Opponent}}};
  var state=new WildBattleState(actor,foe,0,0,0,new(0),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
  var r=WildBattleRules.ResolveCalledMoveContinuation(catalog,state,WildBattleSide.Player);if(!r.Succeeded)throw new Exception("Bide fixture:"+r.Fault);records.Add(r);
 }return records;
}
if(args.Contains("--capture-bide")){
 if(File.Exists(args[1]))throw new Exception("Immutable Bide baseline already exists");File.WriteAllText(args[1],Json(BideCases())+"\n");return 0;
}
var snapshots=new List<object>();
foreach(string move in new[]{"BIDE","PERISH_SONG","DESTINY_BOND","GRUDGE"})foreach(uint seed in new uint[]{0,31,127})foreach(bool lethal in new[]{false,true}){
 var actor=Mon("CHARIZARD",60,move,"DEFENSE_CURL");if(lethal)actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=1}};
 var target=Mon("SNORLAX",100,lethal?"DRAGON_RAGE":"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var state=start.State!;var records=new List<object>();
 for(int i=0;i<4&&state.Outcome==WildBattleOutcome.Ongoing;i++){
  var r=WildBattleRules.ResolveMove(catalog,state,i==0||move=="BIDE"&&i<3?0:1);if(!r.Succeeded)throw new Exception(move+":"+r.Fault);records.Add(r);state=r.State!;
 }
 var data=Json(records);snapshots.Add(new{move,seed,lethal,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Immutable baseline already exists");
 File.WriteAllText(args[1],Json(new{baseline="a51e8a633b36739e398e2e3ef95bab32f15d6a72",snapshots})+"\n");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("immutable complete singles baseline",()=>{
 Check(Environment.Version.Major==10,"hosted runtime10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==24&&old.Length==snapshots.Count,"nonempty suite");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"full state/event/RNG parity "+Json(snapshots[i]));
});



Test("canonical Bide storage/release exact; paralysis only clears the source multi-turn lock",()=>{
 var before=JsonSerializer.Deserialize<List<WildBattleCalledMoveResult>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"bide-before.json")))!;var after=BideCases();Check(before.Count==4&&after.Count==4,"nonempty immutable Bide cases");
 for(int i=0;i<4;i++){
  if(i<2){Check(Json(before[i])==Json(after[i]),"Bide storage/release full exact baseline");continue;}
  var actual=after[i];var added=actual.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-bide"&&e.Reason=="paralysis");
  Check(actual.State!.Player.CalledTurnState is null&&before[i].State!.Player.CalledTurnState is not null,"paralysis cancels original lock");
  var restoredState=actual.State with{EventSequence=actual.State.EventSequence-1,Player=actual.State.Player with{CalledTurnState=before[i].State!.Player.CalledTurnState}};
  var events=actual.Battle.Events.Where(e=>e.Sequence!=added.Sequence).Select(e=>e.Sequence>added.Sequence?e with{Sequence=e.Sequence-1}:e).ToImmutableArray();
  var restored=actual with{Battle=actual.Battle with{State=restoredState,Events=events}};Check(Json(before[i])==Json(restored),"only source lock clear and its event differ; PP/history/RNG identical");
 }
});
Test("canonical Perish Pressure corrects only the extra PP command",()=>{
 var before=JsonSerializer.Deserialize<List<WildBattleCalledMoveResult>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"pressure-before.json")))!;
 var after=PressureCases();Check(before.Count==2&&after.Count==2,"nonempty fixed pressure vectors");
 for(int i=0;i<2;i++){
  var actual=after[i];var commands=actual.Battle.Events.OfType<WildBattlePpCommandEvent>().ToArray();Check(commands.Length==2,"exactly one added PP command");var added=commands[1];
  Check(added.PpSlot==1&&added.Before==4&&added.After==3&&added.CommandCost==1,"actual learned Perish PP source command");
  Check(Json(before[i].Battle.Trace)==Json(actual.Battle.Trace)&&Json(before[i].Invocation)==Json(actual.Invocation),"RNG and invocation unchanged");
  var mon=actual.State!.Player;var moves=mon.Pokemon.Moves;var restoredState=actual.State with{EventSequence=actual.State.EventSequence-1,Player=mon with{Pokemon=mon.Pokemon with{Moves=moves.SetItem(1,moves[1] with{CurrentPp=moves[1].CurrentPp+1})}}};
  var events=actual.Battle.Events.Where(e=>e.Sequence!=added.Sequence).Select(e=>e.Sequence>added.Sequence?e with{Sequence=e.Sequence-1}:e).ToImmutableArray();
  var restored=actual with{Battle=actual.Battle with{State=restoredState,Events=events}};Check(Json(before[i])==Json(restored),"all other state, PP, events and histories identical after removing documented correction");
 }
});
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,int reserves=0,uint seed=31){
 var m=supplied??[Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")];
 var player=new[]{Member(m[0]),Member(m[2])}.Concat(Enumerable.Range(0,reserves).Select(_=>Member(Mon("SNORLAX",100,"DEFENSE_CURL")))).ToImmutableArray();
 var foe=new[]{Member(m[1]),Member(m[3])}.Concat(Enumerable.Range(0,reserves).Select(_=>Member(Mon("SNORLAX",100,"DEFENSE_CURL")))).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,player),new(BattleSide.Opponent,foe)],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,player.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant m){var c=f.Slots[i];var key=c.Member!.Value;var p=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(m))})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 int pos=positions?[c.Slot.Value]??c.Registers.Turn?.Lock?.PpSlot??0;var move=catalog.Moves[c.Registers.Turn?.Lock?.Move??c.Body!.Pokemon.Moves[pos].Move];
 var target=c.Registers.Turn?.Lock?.Target??new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1));
 return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output:"+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Next(SourceFourActionResult prior){var f=prior.State;var token=prior.Continuation!.Value;
 SourceFourActionResult Resume(SourceBattleFrame current)=>current.FourTurn!.Phase switch{
  SourceFourActionPhase.RewardsPending when current.Rewards!.Pending is{} pending=>SourceFourRewards.Learn(catalog,current,token,new(pending.Recipient,pending.Moves[0].Level,pending.Moves[0].Move,null)),
  SourceFourActionPhase.RewardsPending=>SourceFourRewards.Settle(catalog,current,token),
  SourceFourActionPhase.ReplacementsPending=>SourceFourActionTurns.ChooseReplacement(catalog,current,token,current.PendingSwitches[0].Slot,new(current.PendingSwitches[0].Slot.Side,current.PendingSwitches[0].LegalPartyIndices[0])),
  _=>SourceFourActionTurns.Advance(catalog,current,token)};
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;var actual=Resume(f);var replay=Resume(restored);Check(Json(actual)==Json(replay),"exact JSON-resume state/events/RNG");return Ok(actual);
}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace,List<SourceFourActionPhase> Pauses) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null){
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));var events=r.Events.ToList();var trace=r.Trace.ToList();var phases=new List<SourceFourActionPhase>();int steps=0;
 while(r.State.FourTurn is not null){Check(++steps<80,"bounded turn progress");phases.Add(r.State.FourTurn.Phase);r=Next(r);events.AddRange(r.Events);trace.AddRange(r.Trace);}
 Check(r.CompletedCleanTurn,"complete turn or terminal battle");return(r.State,events,trace,phases);
}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int position=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,position,new(target)));Check(r.Succeeded,r.Fault??"action");return r;}
void Rejected(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Trace.IsEmpty&&r.Events.IsEmpty,why+" atomic:"+r.Fault);
Test("Bide complete storage release and ordinary next turn with final PP",()=>{
 var bider=Mon("SNORLAX",100,"BIDE","DEFENSE_CURL") with{SpeedStage=12};bider=bider with{Pokemon=bider.Pokemon with{Moves=bider.Pokemon.Moves.SetItem(0,bider.Pokemon.Moves[0] with{CurrentPp=1})}};
 var f=Frame([bider,Mon("SNORLAX",100,"DRAGON_RAGE","DEFENSE_CURL") with{SpeedStage=4},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DRAGON_RAGE","DEFENSE_CURL") with{SpeedStage=0}]);
 var first=Turn(f,null,[0,0,2,0]);var l=first.State.Slots[0].Registers.Turn!.Lock!;
 Check(l.Kind==WildBattleCalledLockKind.Bide&&l.TurnsRemaining==2&&l.StoredDamage==80&&l.StoredDamageSource==new BattlerSlot(3),"two damages stored at real latest source");
 var second=Turn(first.State,null,[0,0,2,0]);Check(second.State.Slots[0].Registers.Turn!.Lock is{TurnsRemaining:1,StoredDamage:160},"counts action not end-turn");
 var third=Turn(second.State,null,[0,0,2,0]);var hit=third.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("BIDE"));
 Check(hit.Actor.Value==0&&hit.Target.Value==3&&hit.Damage==320,"double accumulated damage at latest source");
 Check(third.State.Slots[0].Registers.Turn is null&&third.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==0,"release clears lock without PP");
 Check(third.Events.OfType<SourcePpCommandEvent>().All(e=>e.Actor.Value!=0)&&third.Trace.Where(x=>x.Purpose.StartsWith("source-battle.slot-0.")).All(x=>x.Purpose.EndsWith(".accuracy")),"release no attack PP, crit, random damage or secondary roll");
 var fourth=Turn(third.State,[1,1,0,1]);Check(fourth.State.Turn==4&&fourth.Events.OfType<SourceMoveUsedEvent>().Count()==4,"four real ordinary next actions");
});
Test("Bide no energy, Protect, immunity, substitute and incapacities",()=>{
 SourceBattleFrame Base()=>Frame([Mon("SNORLAX",100,"BIDE","DEFENSE_CURL") with{SpeedStage=12},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
 var first=Turn(Base());var second=Turn(first.State);var third=Turn(second.State);Check(third.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="bide-no-energy"),"no-energy branch");
 foreach(string kind in new[]{"protect","ghost","substitute","flinch","disabled","paralysis","sleep"}){
  var f=Base();var c=f.Slots[0];f=f with{Slots=f.Slots.SetItem(0,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Bide,Id("BIDE"),0,Id("BIDE"),new(0),1,80,new(1),false),0,null)}})};
  if(kind=="protect")f=Put(f,1,f.Slots[1].Body! with{Protected=true});
  if(kind=="ghost")f=Put(f,1,Mon("GENGAR",100,"DEFENSE_CURL"));
  if(kind=="substitute")f=Put(f,1,f.Slots[1].Body! with{CalledVolatile=new(){SubstituteHp=20}});
  if(kind is "flinch" or "disabled" or "paralysis" or "sleep"){var b=f.Slots[0].Body!;f=Put(f,0,kind switch{"flinch"=>b with{Flinched=true},"disabled"=>b with{DisabledMove=Id("BIDE"),DisableTurnsRemaining=2},"paralysis"=>b with{Status=WildBattleStatus.Paralysis},_=>b with{Status=3}}) with{Rng=new(0)};}
  var r=kind=="sleep"?Turn(f).Events:Act(f,0,0).Events.ToList();
  Check(!r.OfType<SourcePpCommandEvent>().Any(e=>e.Actor.Value==0),kind+" no PP command");
  if(kind=="protect")Check(r.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"),"Protect release");
  else if(kind=="ghost")Check(r.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect),"Gen3 Normal immunity");
  else if(kind=="substitute")Check(r.OfType<SourceSubstituteDamagedEvent>().Single().Damage==160,"release hits substitute");
  else Check(!r.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("BIDE")),kind+" prevents release");
 }
});
Test("Perish Song numeric field setup, Soundproof, pressure and duplicate no refresh",()=>{
 var f=Frame([Mon("SNORLAX",100,"DEFENSE_CURL","PERISH_SONG"),Mon("DUSCLOPS",100,"DEFENSE_CURL"),Mon("DUSCLOPS",100,"DEFENSE_CURL"),Mon("ELECTRODE",100,"DEFENSE_CURL")]);
 Check(f.Slots[3].Body!.Pokemon.AbilityId=="ABILITY_SOUNDPROOF","Soundproof fixture");
 f=f with{SourceAbsentMask=4};var r=Act(f,0,0,1);
 Check(new[]{0,1,2}.All(i=>r.State.Slots[i].Body!.CalledVolatile?.PerishSongTimer==3)&&r.State.Slots[3].Body!.CalledVolatile is null,"includes absent ally, excludes soundproof");
 Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("DEFENSE_CURL")].Pp&&r.State.Slots[0].Body!.Pokemon.Moves[1].CurrentPp==2,"extra pressure drains first learned slot, even ally/absent");
 Check(r.Events.OfType<SourceAbilityActivatedEvent>().Count(e=>e.Ability=="ABILITY_SOUNDPROOF")==1,"soundproof reported after success");
 var duplicate=Act(r.State,0,0,1);Check(duplicate.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="perish-song-already-active")&&!duplicate.Events.OfType<SourceAbilityActivatedEvent>().Any(),"duplicate fails without second soundproof report");
 Check(duplicate.State.Slots[0].Body!.Pokemon.Moves[1].CurrentPp==0,"failed Perish still pays first-slot pressure");
});
Test("Perish complete four turns serializes reward and replacement boundaries",()=>{
 var f=Frame([Mon("SNORLAX",100,"PERISH_SONG","DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("ELECTRODE",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")],2);
 var first=Turn(f);Check(new[]{0,1,3}.All(i=>first.State.Slots[i].Body!.CalledVolatile?.PerishSongTimer==2),"first countdown occurs same turn");
 var second=Turn(first.State,[1,0,0,0]);var third=Turn(second.State,[1,0,0,0]);Check(new[]{0,1,3}.All(i=>third.State.Slots[i].Body!.CalledVolatile?.PerishSongTimer==0),"zero counter lives until next callback");
 var old=third.State.Slots.Select(c=>c.Occupant).ToArray();var fourth=Turn(third.State,[1,0,0,0]);
 Check(fourth.Pauses.Contains(SourceFourActionPhase.RewardsPending)&&fourth.Pauses.Contains(SourceFourActionPhase.ReplacementsPending),"actual EXP then KO entry pauses");
 Check(fourth.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Move==Id("PERISH_SONG"))==3&&fourth.Events.OfType<SourceFaintedEvent>().Count()==3,"each countdown kills exactly once");
 Check(new[]{0,1,3}.All(i=>fourth.State.Slots[i].Occupant!=old[i]&&fourth.State.Slots[i].Body!.CalledVolatile?.PerishSongTimer is null),"new occupants do not inherit timer or replay callback");
 Check(fourth.Events.OfType<SourceTurnReadyEvent>().Count()==1&&fourth.State.Turn==4,"one clean turn boundary");
 var fifth=Turn(fourth.State);Check(!fifth.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("PERISH_SONG")),"normal next turn");
});
Test("Destiny Bond and Grudge clear on an unsuccessful next action",()=>{
 foreach(string effect in new[]{"DESTINY_BOND","GRUDGE"}){
  var f=Frame([Mon("SNORLAX",100,effect,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
  var first=Turn(f);Check(effect=="GRUDGE"?first.State.Slots[0].Body!.CalledVolatile?.Grudge==true:first.State.Slots[0].Body!.DestinyBond,"flag survives end-turn");
  var next=Turn(Put(first.State,0,first.State.Slots[0].Body! with{Flinched=true}),[1,0,0,0]);
  Check(!next.State.Slots[0].Body!.DestinyBond&&next.State.Slots[0].Body!.CalledVolatile?.Grudge!=true,"clear before flinch");
  Check(!next.Events.OfType<SourcePpCommandEvent>().Any(e=>e.Actor.Value==0),"blocked action spends no PP");
 }
});
Test("direct knockout applies Grudge before Destiny Bond, then rewards and deferred entries",()=>{
 var target=Mon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=0,DestinyBond=true,CalledVolatile=new(){Grudge=true}};target=target with{Pokemon=target.Pokemon with{CurrentHp=1}};
 var f=Frame([Mon("CHARIZARD",100,"DRAGON_RAGE","DEFENSE_CURL") with{SpeedStage=12},target,Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")],1);
 var old0=f.Slots[0].Occupant;var old1=f.Slots[1].Occupant;var turn=Turn(f);
 var grudge=turn.Events.OfType<SourceGrudgePpEvent>().Single();var bond=turn.Events.OfType<SourceTowerHpEvent>().Single(e=>e.Effect=="destiny-bond");
 Check(grudge.Attacker.Value==0&&grudge.Fainted.Value==1&&grudge.ChosenSlot==0&&grudge.Sequence<bond.Sequence,"Grudge chosen PP before Destiny Bond");
 Check(turn.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{0,1}),"source stacked attacker then target faint scripts");
 Check(turn.Pauses.Contains(SourceFourActionPhase.RewardsPending)&&turn.Pauses.Contains(SourceFourActionPhase.ReplacementsPending),"real reward and entry boundaries");
 Check(turn.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[0].CurrentPp==0&&turn.State.Slots[0].Occupant!=old0&&turn.State.Slots[1].Occupant!=old1,"original member drained, exact occupants replaced");
 Check(turn.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Actor.Value==1)==0,"fainted queued opponent never acts");
});
Test("ally knockout and passive Perish never trigger Destiny Bond or Grudge",()=>{
 var protectedBody=Mon("SNORLAX",100,"DEFENSE_CURL") with{DestinyBond=true,CalledVolatile=new(){Grudge=true}};protectedBody=protectedBody with{Pokemon=protectedBody.Pokemon with{CurrentHp=1}};
 var f=Frame([Mon("CHARIZARD",100,"DRAGON_RAGE"),Mon("SNORLAX",100,"DEFENSE_CURL"),protectedBody,Mon("SNORLAX",100,"DEFENSE_CURL")]);
 var hit=Act(f,0,2);Check(hit.State.Slots[0].Body!.Pokemon.CurrentHp>0&&!hit.Events.OfType<SourceGrudgePpEvent>().Any()&&!hit.Events.OfType<SourceTowerHpEvent>().Any(),"same-side direct KO");
 f=Frame(reserves:1);f=Put(f,1,f.Slots[1].Body! with{DestinyBond=true,CalledVolatile=new(){Grudge=true,PerishSongTimer=0},Status=3});
 var passive=Turn(f);Check(!passive.Events.OfType<SourceGrudgePpEvent>().Any()&&!passive.Events.OfType<SourceTowerHpEvent>().Any(),"passive Perish independent of active invocation");
});

Test("Bide retains source position across entry and retargets absent or deferred vacancies",()=>{
 SourceBattleFrame Ready(int source){var f=Frame([Mon("SNORLAX",100,"BIDE"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);var c=f.Slots[0];return f with{Slots=f.Slots.SetItem(0,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Bide,Id("BIDE"),0,Id("BIDE"),new(0),1,20,new(source),false),0,null)}})};}
 var zero=Ready(1) with{Rng=new(0)};zero=Put(zero,2,Mon("ABRA",1,"PSYWAVE"));var refreshed=Act(zero,2,0);
 Check(refreshed.Events.OfType<SourceHpChangedEvent>().Single().Damage==0&&refreshed.State.Slots[0].Registers.Turn!.Lock is{StoredDamage:20,StoredDamageSource.Value:2},"effective zero damage refreshes latest source without adding energy");
 var immune=Put(zero,0,Mon("UMBREON",100,"BIDE"));var ignored=Act(immune,2,0);Check(ignored.State.Slots[0].Registers.Turn!.Lock!.StoredDamageSource==new BattlerSlot(1),"zero from immunity does not replace the remembered source");
 var ally=Act(Ready(2),0,0);Check(ally.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==2,"same-side last damage source remains legal");
 var absent=Ready(1) with{SourceAbsentMask=2};var redirected=Act(absent,0,0);Check(redirected.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3&&redirected.Trace.Any(x=>x.Purpose.Contains("selected-rejection")),"GetMoveTarget only when remembered source absent");
 var dead=Ready(1);dead=Put(dead,1,dead.Slots[1].Body! with{Pokemon=dead.Slots[1].Body!.Pokemon with{CurrentHp=0}}) with{DeferredKoVacancyMask=2};var replacement=Act(dead,0,0);Check(replacement.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3,"approved deferred vacancy also retargets");
 var changed=Ready(1);var old=changed.Slots[1];changed=Put(changed,1,Mon("BLASTOISE",100,"DEFENSE_CURL"));changed=changed with{Slots=changed.Slots.SetItem(1,changed.Slots[1] with{EntrySerial=old.EntrySerial+1})};var occupant=Act(changed,0,0);Check(occupant.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==1&&!occupant.Trace.Any(x=>x.Purpose.Contains("selected-rejection")),"source is battler position rather than old occupant token");
});
Test("delayed Future precedes Perish and ordinary switching clears Perish",()=>{
 var f=Frame(reserves:1);f=Put(f,0,f.Slots[0].Body! with{CalledVolatile=new(){PerishSongTimer=0}});var cell=f.Slots[0];f=f with{Slots=f.Slots.SetItem(0,cell with{Position=cell.Position with{FutureAttack=new(1,Id("FUTURE_SIGHT"),new(1),20)}})};
 var t=Turn(f);var future=t.Events.OfType<SourceCalledDelayedEvent>().Single(e=>e.Move==Id("FUTURE_SIGHT")&&e.Reason=="resolved");var perish=t.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("PERISH_SONG"));Check(future.Sequence<perish.Sequence,"numeric Future stage before retained-order Perish");
 var start=Frame(reserves:1);start=Put(start,0,start.Slots[0].Body! with{CalledVolatile=new(){PerishSongTimer=0,Grudge=true},DestinyBond=true});
 var choices=Choices(start).Select(x=>x.Actor.Slot.Value==0?new SourceActionSelection(x.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)):x).ToImmutableArray();
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,start,start.Revision,choices));var events=r.Events.ToList();while(r.State.FourTurn is not null){r=Next(r);events.AddRange(r.Events);}
 Check(r.State.Slots[0].Body!.CalledVolatile is null&&!r.State.Slots[0].Body!.DestinyBond&&!events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("PERISH_SONG")),"voluntary new occupant starts clear");
});
Test("malformed counters reject atomically",()=>{
 foreach(int timer in new[]{-1,4}){var f=Frame();f=Put(f,0,f.Slots[0].Body! with{CalledVolatile=new(){PerishSongTimer=timer}});Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"bad perish timer");}
 var b=Frame([Mon("SNORLAX",100,"BIDE"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);var c=b.Slots[0];b=b with{Slots=b.Slots.SetItem(0,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Bide,Id("BIDE"),0,Id("BIDE"),new(0),3,0,null,false),0,null)}})};Rejected(b,SourceFourActionTurns.BeginActions(catalog,b,b.Revision,Choices(b)),"bad Bide counter");
});
File.WriteAllText(args[1],Json(new{groups,checks,failures,results})+"\n");return failures==0?0:1;
