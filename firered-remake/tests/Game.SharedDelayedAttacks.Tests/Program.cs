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
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,m.Status);
var snapshots=new List<object>();var serializedSnapshots=new List<object>();
foreach(string name in new[]{"WISH","FUTURE_SIGHT","DOOM_DESIRE"})foreach(string kind in new[]{"valid","substitute","occupied","screen"}){
 var actor=Mon("ALAKAZAM",40,name);var target=Mon("NIDORINO",100,"DEFENSE_CURL");
 actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=1}};
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(31));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var state=start.State!;
 if(kind=="substitute")target=target with{CalledVolatile=new(){SubstituteHp=20},Protected=true};
 state=state with{Opponent=target,CalledParty=state.CalledParty! with{Opponent=state.CalledParty.Opponent.SetItem(0,Member(target))}};
 if(kind=="screen")state=state with{OpponentReflectTurnsRemaining=5,CalledField=(state.CalledField??new()) with{OpponentLightScreenTurnsRemaining=5}};
 var stages=new List<object>();
 if(kind=="occupied"){var prior=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));if(!prior.Succeeded)throw new Exception(prior.Battle.Fault);state=prior.State!;stages.Add(prior);}
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));if(!result.Succeeded)throw new Exception("action:"+result.Battle.Fault);stages.Add(result);state=result.State!;
 for(int turn=0;turn<3;turn++){var residual=WildBattleRules.ResolveCalledVolatileEndTurn(catalog,state);if(!residual.Succeeded)throw new Exception("end:"+residual.Fault);stages.Add(residual);state=residual.State!;}
 string data=Json(stages);serializedSnapshots.Add(new{name,kind,stages});snapshots.Add(new{name,kind,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasDelayedRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires fixed pre-edit Domain");File.WriteAllText(args[1]+".serialized.json",Json(serializedSnapshots)+"\n");File.WriteAllText(args[1],Json(new{baseline="294eb6c9c7d4b41d8733acc26cbe89a74d2325d7",snapshots})+"\n");return 0;}
var plusMinusSnapshots=new List<object>();
foreach(string species in new[]{"PLUSLE","MINUN"})foreach(string move in new[]{"FUTURE_SIGHT","DOOM_DESIRE"}){
 var actor=Mon(species,50,move);var target=Mon(species=="PLUSLE"?"MINUN":"PLUSLE",100,"DEFENSE_CURL");
 var initial=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(31)).State!;
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,initial,new(WildBattleSide.Player,0));if(!result.Succeeded)throw new Exception(result.Fault);
 plusMinusSnapshots.Add(new{species,move,result});
}
if(args.Contains("--capture-plus-minus")){
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasDelayedRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires pre-edit Domain");
 File.WriteAllText(args[1],Json(plusMinusSnapshots)+"\n");return 0;
}
int checks=0,cleanTurns=0,replays=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
using(var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")))){
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==12&&old.Length==snapshots.Count,"nonempty frozen corpus");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"complete singles state/events/RNG parity "+Json(snapshots[i]));
}
using(var serialized=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-serialized.json"))))
{
 var full=serialized.RootElement.EnumerateArray().ToArray();Check(full.Length==12,"full immutable serialization present");
 for(int i=0;i<full.Length;i++)Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full[i].GetProperty("stages").GetRawText()))).ToLowerInvariant()==JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString(),"raw serialized full before state/events/RNG hash");
}
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,uint seed=31){var m=mons??[Mon("ALAKAZAM",50,"WISH","FUTURE_SIGHT","DOOM_DESIRE","DEFENSE_CURL"),Mon("NIDORINO",100,"DEFENSE_CURL","TACKLE"),Mon("PIDGEY",100,"DEFENSE_CURL"),Mon("RATTATA",100,"MINIMIZE")];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int position=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,position,new(target)));Check(r.Succeeded,r.Fault??"accepted action");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=positions?[c.Slot.Value]??0;var move=catalog.Moves[pos<0?WildBattleRules.StruggleMove:c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted turn");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid identity");return r;}
SourceFourActionResult Step(SourceFourActionResult r){var f=r.State;var token=r.Continuation!.Value;SourceFourActionResult Run(SourceBattleFrame x)=>x.FourTurn!.Phase switch{
 SourceFourActionPhase.RewardsPending=>x.Rewards!.Pending is {} pending?SourceFourRewards.Learn(catalog,x,token,new(pending.Recipient,pending.Moves[0].Level,pending.Moves[0].Move,null)):SourceFourRewards.Settle(catalog,x,token),
 SourceFourActionPhase.ReplacementsPending=>SourceFourActionTurns.ChooseReplacement(catalog,x,token,x.PendingSwitches[0].Slot,new(x.PendingSwitches[0].Slot.Side,2)),
 _=>SourceFourActionTurns.Advance(catalog,x,token)};
 var actual=Run(f);replays++;var replay=Run(JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!);Check(Json(actual)==Json(replay),"complete JSON continuation equality");return Ok(actual);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var events=r.Events.ToList();int guard=0;while(r.State.FourTurn is not null && r.State.Outcome==WildBattleOutcome.Ongoing){Check(++guard<40,"bounded completion");r=Step(r);events.AddRange(r.Events);}Check(r.CompletedCleanTurn||r.State.Outcome!=WildBattleOutcome.Ongoing,"clean turn or ended");cleanTurns++;return(r.State,events);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Turn(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>Finish(Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,positions,targets))));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string reason)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,reason+":"+r.Fault);
var labels=new List<string>();void Test(string label,Action run){run();labels.Add(label);Console.WriteLine("PASS "+label);}

SourceBattleFrame Reserve(SourceBattleFrame f, BattleSide side, WildBattleCombatant body)
{
 var party=f.Party(side);int index=party.Members.Length;
 f=f with{Parties=f.Parties.SetItem((int)side,party with{Members=party.Members.Add(Member(body))})};
 if(side==BattleSide.Player)f=f with{Rewards=f.Rewards! with{Metadata=f.Rewards!.Metadata.Add(new(new(side,index),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
 return f;
}
if(args.Length!=3||!File.Exists(args[2]))throw new ArgumentException("Third argument must be the compiled pinned source-base-damage executable");
using var oracle=new System.Diagnostics.Process{StartInfo=new(args[2]){RedirectStandardInput=true,RedirectStandardOutput=true,UseShellExecute=false}};
oracle.Start();var oracleInputs=new List<string>();
int SourceBase(WildBattleCombatant actor,WildBattleCombatant target,string move,WildBattleCombatant[] field){
 var definition=catalog.Moves[Id(move)];bool physical=definition.Type=="TYPE_STEEL";
 int Ability(WildBattleCombatant m)=>WildBattleEffective.Ability(m) switch{"ABILITY_PLUS"=>57,"ABILITY_MINUS"=>58,_=>0};
 int attack=physical?actor.Pokemon.Stats.Attack:actor.Pokemon.Stats.SpecialAttack,defense=physical?target.Pokemon.Stats.Defense:target.Pokemon.Stats.SpecialDefense;
 var abilities=field.Select(Ability).Concat(Enumerable.Repeat(0,4)).Take(4);
 string input=$"{attack} {defense} {actor.Pokemon.Level} {Ability(actor)} {Ability(target)} {string.Join(' ',abilities)} {(physical?8:14)} {definition.Power} 0";
 oracleInputs.Add(input);oracle.StandardInput.WriteLine(input);oracle.StandardInput.Flush();return int.Parse(oracle.StandardOutput.ReadLine()!);
}
Test("canonical Plus Minus Future Sight corrects the omitted source base boost with bounded delta",()=>{
 using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-plus-minus-before.json")));
 var rows=frozen.RootElement.EnumerateArray().ToArray();Check(rows.Length==4,"four frozen Plus Minus entries");
 for(int i=0;i<rows.Length;i++){
  var current=JsonSerializer.SerializeToElement(plusMinusSnapshots[i]);string species=current.GetProperty("species").GetString()!,move=current.GetProperty("move").GetString()!;
  var a=Mon(species,50,move);var b=Mon(species=="PLUSLE"?"MINUN":"PLUSLE",100,"DEFENSE_CURL");
  var result=JsonSerializer.Deserialize<WildBattleCalledMoveResult>(current.GetProperty("result").GetRawText())!;
  int expected=SourceBase(a,b,move,[a,b]);Check(result.State!.CalledDelayed!.OpponentFutureAttack!.Damage==expected,"unchanged full source base function");
  var old=System.Text.Json.Nodes.JsonNode.Parse(rows[i].GetProperty("result").GetRawText())!;var next=System.Text.Json.Nodes.JsonNode.Parse(current.GetProperty("result").GetRawText())!;
  foreach(string root in new[]{"State","Battle.State"}){
   var oldState=root=="State"?old["State"]!:old["Battle"]!["State"]!;var newState=root=="State"?next["State"]!:next["Battle"]!["State"]!;
   var before=oldState["CalledDelayed"]!["OpponentFutureAttack"]!["Damage"]!.GetValue<int>();
   Check(move=="FUTURE_SIGHT"?expected>before:expected==before,"only special Future Sight increases");oldState["CalledDelayed"]!["OpponentFutureAttack"]!["Damage"]=expected;
  }
  Check(System.Text.Json.Nodes.JsonNode.DeepEquals(old,next),"all other canonical state/events/RNG values unchanged");
 }
});
Test("all four positions schedule Wish Future Sight and Doom Desire without setup RNG",()=>{
 foreach(int actor in Enumerable.Range(0,4))foreach(string move in new[]{"WISH","FUTURE_SIGHT","DOOM_DESIRE"}){
  var f=Frame();f=Put(f,actor,Mon("ALAKAZAM",50,move));var r=Act(f,actor,move=="WISH"?actor:actor^1);
  Check(r.Trace.IsEmpty,"setup consumes no accuracy critical or damage draw");
  int target=move=="WISH"?actor:actor^1;
  Check(r.Events.OfType<SourcePpCommandEvent>().Single().After==catalog.Moves[Id(move)].Pp-1,"PP charged at setup");
  Check(move=="WISH"?r.State.Slots[target].Position.Wish is{TurnsRemaining:2}:r.State.Slots[target].Position.FutureAttack is{TurnsRemaining:3}future&&future.Attacker.Value==actor&&future.Move==Id(move),"true position and source actor retained");
  Check(r.State.Slots.Where(c=>c.Slot.Value!=target).All(c=>c.Position.Wish is null&&c.Position.FutureAttack is null),"other positions untouched");
  if(move!="WISH")Check(r.State.Slots[target].Position.FutureAttack!.Damage==SourceBase(f.Slots[actor].Body!,f.Slots[target].Body!,move,f.Slots.Select(c=>c.Body!).ToArray()),"all actual positions match unchanged source base damage");
 }
});
Test("occupied slot fails after PP; delayed moves share slot and bypass setup Protect Substitute immunity",()=>{
 var f=Put(Frame(),1,Mon("POOCHYENA",100,"DEFENSE_CURL") with{Protected=true,CalledVolatile=new(){SubstituteHp=20}});
 var first=Act(f,0,1,1);Check(first.State.Slots[1].Position.FutureAttack is not null&&first.Trace.IsEmpty,"dark Protect Substitute target scheduled");
 var second=Act(first.State,0,1,2);Check(second.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="future-slot-occupied"&&second.State.Slots[1].Position.FutureAttack==first.State.Slots[1].Position.FutureAttack,"Future/Doom share occupied position");Check(second.Trace.IsEmpty&&second.Events.OfType<SourcePpCommandEvent>().Single().After==4,"failure spends PP without new damage roll");
 var wish=Act(Frame(),0,0);var repeat=Act(wish.State,0,0);Check(repeat.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="wish-slot-occupied"&&repeat.Trace.IsEmpty,"Wish cannot refresh");
});
Test("three real turns snapshot damage before later stat screen and type changes",()=>{
 foreach(string move in new[]{"FUTURE_SIGHT","DOOM_DESIRE"}){
  var f=Put(Frame(),0,Mon("ALAKAZAM",50,move,"DEFENSE_CURL"));f=Put(f,1,Mon("POOCHYENA",100,"DEFENSE_CURL"));
  var first=Turn(f);var snapshot=first.State.Slots[1].Position.FutureAttack!;Check(snapshot.TurnsRemaining==2,"setting turn counts down");
  var changed=Put(first.State,0,first.State.Slots[0].Body! with{AttackStage=0,SpecialAttackStageDelta=-6});changed=Put(changed,1,changed.Slots[1].Body! with{DefenseStage=12,SpecialDefenseStageDelta=6});changed=changed with{Sides=changed.Sides.SetItem(1,changed.Sides[1] with{ReflectTurnsRemaining=5,LightScreenTurnsRemaining=5})};
  var second=Turn(changed,[1,0,0,0]);Check(second.State.Slots[1].Position.FutureAttack is{TurnsRemaining:1,Damage:var damage}&&damage==snapshot.Damage,"stored base unaffected by subsequent stats/screens");
  var third=Turn(second.State,[1,0,0,0]);var hit=third.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id(move));
  Check(hit.Damage==Math.Max(1,snapshot.Damage*hit.RandomPercent/100)&&!hit.Critical,"impact only accuracy then random; no STAB type or critical");Check(third.State.Slots[1].Position.FutureAttack is null,"consumed once");
 }
});
Test("Helping Hand multiplies stored source base and screens use the real defender count",()=>{
 foreach(string move in new[]{"FUTURE_SIGHT","DOOM_DESIRE"}){
  var f=Put(Frame(),0,Mon("ALAKAZAM",50,move,"DEFENSE_CURL"));var plain=Act(f,0,1).State.Slots[1].Position.FutureAttack!.Damage;
  var cell=f.Slots[0];f=f with{Slots=f.Slots.SetItem(0,cell with{Registers=cell.Registers with{HelpingHand=true}})};
  Check(Act(f,0,1).State.Slots[1].Position.FutureAttack!.Damage==plain*15/10,"source Helping Hand after base");
  var screen=f with{Slots=f.Slots.SetItem(0,cell),Sides=f.Sides.SetItem(1,f.Sides[1] with{ReflectTurnsRemaining=5,LightScreenTurnsRemaining=5})};
  int doubled=Act(screen,0,1).State.Slots[1].Position.FutureAttack!.Damage;
  var absent=screen with{SourceAbsentMask=8,TurnStartAbsentMask=8};int single=Act(absent,0,1).State.Slots[1].Position.FutureAttack!.Damage;
  Check(doubled<plain&&single<doubled,"two present defenders reduce screen to source two-thirds; one halves");
 }
});
Test("Wish heals actual replacement maximum HP and retains original source identity",()=>{
 var f=Frame();var old=f.Slots[0].Occupant;var replacement=Mon("SNORLAX",100,"DEFENSE_CURL");replacement=replacement with{Pokemon=replacement.Pokemon with{CurrentHp=1}};f=Reserve(f,BattleSide.Player,replacement);
 var first=Turn(f);Check(first.State.Slots[0].Position.Wish is{TurnsRemaining:1},"Wish waits one further end turn");
 var choices=Choices(first.State,[3,0,0,0]).SetItem(0,new(first.State.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));
 var second=Finish(Ok(SourceFourActionTurns.BeginActions(catalog,first.State,first.State.Revision,choices)));
 Check(second.State.Slots[0].Occupant!=old&&second.State.Slots[0].Member!.Value.Index==2,"real switch replacement");
 var heal=second.Events.OfType<SourceRecoveredEvent>().Single(e=>e.Move==Id("WISH"));Check(heal.Recovered==replacement.Pokemon.Stats.MaxHp/2&&heal.HpAfter==1+heal.Recovered,"uses incoming Snorlax maximum HP");
 Check(second.Events.OfType<SourceCalledDelayedEvent>().Single(e=>e.Move==Id("WISH")).SourcePartyIndex==0&&second.State.Slots[0].Position.Wish is null,"original attribution consumed with position timer");
});
Test("both participants can switch before Future impact while position damage persists",()=>{
 var f=Frame();f=Reserve(f,BattleSide.Player,Mon("RATTATA",30,"DEFENSE_CURL"));f=Reserve(f,BattleSide.Opponent,Mon("SNORLAX",100,"DEFENSE_CURL"));
 var first=Turn(f,[1,0,0,0]);var damage=first.State.Slots[1].Position.FutureAttack!.Damage;
 var choices=Choices(first.State,[3,0,0,0]).SetItem(0,new(first.State.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2))).SetItem(1,new(first.State.Slots[1].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,2)));
 var second=Finish(Ok(SourceFourActionTurns.BeginActions(catalog,first.State,first.State.Revision,choices)));Check(second.State.Slots[1].Position.FutureAttack is{TurnsRemaining:1}attack&&attack.Attacker.Value==0&&attack.Damage==damage,"actor position and damage survive both switches");
 var third=Turn(second.State);var hit=third.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id("FUTURE_SIGHT"));Check(hit.Actor.Value==0&&hit.Target.Value==1&&hit.Damage==damage*hit.RandomPercent/100,"hits actual incoming target, attributed to retained position");
 Check(third.State.Party(BattleSide.Opponent).Members[0].Pokemon.CurrentHp==first.State.Party(BattleSide.Opponent).Members[0].Pokemon.CurrentHp,"departed target untouched");
});
Test("Wish survives caster faint reward replacement and consumes once",()=>{
 var f=Frame([Mon("SNORLAX",50,"DEFENSE_CURL"),Mon("ALAKAZAM",50,"WISH","DEFENSE_CURL"),Mon("PIDGEY",100,"DEFENSE_CURL"),Mon("RATTATA",100,"DEFENSE_CURL")]);f=Put(f,1,f.Slots[1].Body! with{Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=1},Status=WildBattleStatus.Poison});var replacement=Mon("NIDORINO",100,"DEFENSE_CURL");replacement=replacement with{Pokemon=replacement.Pokemon with{CurrentHp=1}};f=Reserve(f,BattleSide.Opponent,replacement);
 var first=Turn(f);Check(first.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==1)&&first.Events.OfType<SourceRewardAppliedEvent>().Any(),"actual caster faint and reward boundary");Check(first.State.Slots[1].Member!.Value.Index==2&&first.State.Slots[1].Position.Wish is{TurnsRemaining:1},"replacement inherits pending position Wish");
 var second=Turn(first.State);Check(second.Events.OfType<SourceRecoveredEvent>().Single(e=>e.Move==Id("WISH")).Recovered==replacement.Pokemon.Stats.MaxHp/2,"heals real replacement after reward resume");
});
Test("Future lethal impact follows actual reward replacement continuation without duplicate hit",()=>{
 var f=Frame();f=Put(f,1,f.Slots[1].Body! with{Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=1}});f=Reserve(f,BattleSide.Opponent,Mon("NIDORINO",100,"DEFENSE_CURL"));
 var first=Turn(f,[1,0,0,0]);var second=Turn(first.State,[3,0,0,0]);var third=Turn(second.State,[3,0,0,0]);
 Check(third.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Move==Id("FUTURE_SIGHT"))==1&&third.Events.OfType<SourceFaintedEvent>().Single().Side.Value==1,"one delayed KO");Check(third.State.Slots[1].Member!.Value.Index==2&&third.State.Slots[1].Position.FutureAttack is null,"actual replacement does not inherit resolved attack");
 var fourth=Turn(third.State,[3,0,0,0]);Check(!fourth.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("FUTURE_SIGHT")),"no duplicate on next turn");
});
Test("absent position freezes Future but Wish resolves; full HP consumes Wish without healing",()=>{
 var f=Frame();
 f=Act(f,0,1,1).State;f=Act(f,0,0).State;f=f with{SourceAbsentMask=3,TurnStartAbsentMask=3};
 var one=Turn(f);Check(one.State.Slots[1].Position.FutureAttack is{TurnsRemaining:3}&&one.State.Slots[0].Position.Wish is{TurnsRemaining:1},"different source absence policies");var two=Turn(one.State);Check(two.State.Slots[1].Position.FutureAttack is{TurnsRemaining:3}&&two.State.Slots[0].Position.Wish is null,"Wish consumed even when absent");Check(two.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Move==Id("WISH")&&e.Reason=="already-full-hp"),"full HP result without healing event");
});
Test("real Helping Hand applies before delayed snapshot and Wish heals before weather",()=>{
 var f=Frame();f=Put(f,2,Mon("PIDGEY",100,"HELPING_HAND","DEFENSE_CURL"));
 int plain=Act(f,0,1,1).State.Slots[1].Position.FutureAttack!.Damage;
 var helped=Turn(f,[1,0,0,0],[1,1,0,3]);Check(helped.State.Slots[1].Position.FutureAttack!.Damage==plain*15/10,"real Helping Hand target executes before Future Sight");
 f=Frame();var first=Turn(f);f=Put(first.State,0,first.State.Slots[0].Body! with{Pokemon=first.State.Slots[0].Body!.Pokemon with{CurrentHp=1}});f=f with{Field=new(){HailTurnsRemaining=3}};
 var second=Turn(f,[3,0,0,0]);var heal=second.Events.OfType<SourceRecoveredEvent>().Single(e=>e.Move==Id("WISH"));var weather=second.Events.OfType<SourceWeatherDamageEvent>().Single(e=>e.Side.Value==0);Check(heal.Sequence<weather.Sequence&&weather.HpBefore==heal.HpAfter&&second.State.Slots[0].Body!.Pokemon.CurrentHp>0,"Wish prevents fatal weather by healing in field order first");
});
Test("impact misses use accuracy only; hits use accuracy and random once; Substitute receives damage",()=>{
 bool missed=false,hit=false;
 for(uint seed=0;seed<24&&(!missed||!hit);seed++){
  var f=Frame(seed:seed);f=Put(f,1,f.Slots[1].Body! with{CalledVolatile=new(){SubstituteHp=255},EvasionStage=12});
  var first=Turn(f,[1,0,0,0]);var second=Turn(first.State,[3,0,0,0]);
  var begin=Ok(SourceFourActionTurns.BeginActions(catalog,second.State,second.State.Revision,Choices(second.State,[3,0,0,0])));var events=begin.Events.ToList();var traces=begin.Trace.ToList();int guard=0;while(begin.State.FourTurn is not null){Check(++guard<32,"bounded impact turn");begin=Step(begin);events.AddRange(begin.Events);traces.AddRange(begin.Trace);}cleanTurns++;
  var impact=traces.Where(t=>t.Purpose.Contains("future-attack")).ToArray();bool failure=events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="future-accuracy");
  Check(impact.Length==(failure?1:2),"impact draw count independent of setup");Check(begin.State.Slots[1].Position.FutureAttack is null,"hit or miss consumes timer");
  if(failure){missed=true;Check(!events.OfType<SourceSubstituteDamagedEvent>().Any(),"miss does not damage Substitute");}
  else{hit=true;Check(events.OfType<SourceSubstituteDamagedEvent>().Single().Target.Value==1&&!events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("FUTURE_SIGHT")),"successful delayed attack damages Substitute, not real HP");}
 }
 Check(missed&&hit,"both bounded source accuracy branches observed");
});
Test("invalid Wish identity and stale commands are rejected atomically",()=>{
 var f=Act(Frame(),0,0).State;var cell=f.Slots[0];var bad=f with{Slots=f.Slots.SetItem(0,cell with{Position=cell.Position with{Wish=cell.Position.Wish! with{SourcePersonality=1}}})};Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"corrupt original attribution");Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision+1,Choices(f)),"stale command");
});
oracle.StandardInput.Close();oracle.WaitForExit();Check(oracle.ExitCode==0&&oracleInputs.Count==12,"nonempty successful full source damage proof");
Test("real Future scheduling resolves before same-turn Perish knockouts",()=>{
 var f=Frame([Mon("ALAKAZAM",50,"FUTURE_SIGHT","DEFENSE_CURL"),Mon("NIDORINO",100,"DEFENSE_CURL"),Mon("ELECTRODE",100,"PERISH_SONG","DEFENSE_CURL"),Mon("RATTATA",100,"DEFENSE_CURL")],0);
 Check(f.Slots[2].Body!.Pokemon.AbilityId=="ABILITY_SOUNDPROOF","authentic surviving Soundproof partner");
 var first=Turn(f,[1,0,0,0]);var second=Turn(first.State,[0,0,1,0]);var third=Turn(second.State,[1,0,1,0]);var fourth=Turn(third.State,[1,0,1,0]);
 File.WriteAllText(args[1]+".perish-future.json",Json(new{fourth.State,fourth.Events})+"\n");
 var future=fourth.Events.OfType<SourceCalledDelayedEvent>().Single(e=>e.Move==Id("FUTURE_SIGHT")&&e.Reason=="resolved");
 var perish=fourth.Events.OfType<SourceHpChangedEvent>().Where(e=>e.Move==Id("PERISH_SONG")).ToArray();
 Check(perish.Length==2&&perish.Select(e=>e.Target.Value).SequenceEqual(new[]{3,1})&&perish.All(e=>e.Sequence>future.Sequence),"real scheduled impact precedes both source-speed-order opponent Perish deaths before win ends residuals");
 Check(fourth.State.Outcome==WildBattleOutcome.Won&&fourth.State.Slots[2].Body!.Pokemon.CurrentHp>0,"ordinary win owner settles with surviving actual partner");
});
File.WriteAllText(args[1],Json(new{checks,cleanTurns,replays,labels,snapshots,plusMinusSnapshots,oracleInputs})+"\n");return 0;
