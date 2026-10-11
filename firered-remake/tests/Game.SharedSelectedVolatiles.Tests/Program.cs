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
var snapshots=new List<object>();
foreach(string name in new[]{"ATTRACT","TAUNT","TORMENT","SPITE"})foreach(string kind in new[]{"valid","substitute","failure"}){
 var actor=Mon("ALAKAZAM",40,name);var target=Mon("NIDORINO",100,"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(31));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 target=start.State!.Opponent with{LastUsedMove=kind=="failure"?null:Id("DEFENSE_CURL")};
 if(kind=="substitute")target=target with{CalledVolatile=new(){SubstituteHp=20}};
 if(kind=="failure"&&name=="ATTRACT")target=Mon("NIDORINA",100,"DEFENSE_CURL");
 if(kind=="failure"&&name=="TAUNT")target=target with{CalledVolatile=new(){TauntTurnsRemaining=2}};
 if(kind=="failure"&&name=="TORMENT")target=target with{CalledVolatile=new(){Tormented=true}};
 var state=start.State with{Opponent=target,CalledParty=start.State.CalledParty! with{Opponent=start.State.CalledParty.Opponent.SetItem(0,Member(target))}};
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));if(!result.Battle.Succeeded)throw new Exception("action:"+result.Battle.Fault);
 string data=Json(result);snapshots.Add(new{name,kind,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasSelectedVolatileRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires fixed pre-edit Domain");File.WriteAllText(args[1],Json(new{baseline="a51e8a633b36739e398e2e3ef95bab32f15d6a72",snapshots})+"\n");return 0;}
int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
using(var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")))){
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==12&&old.Length==snapshots.Count,"nonempty frozen corpus");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"complete singles state/events/RNG parity "+Json(snapshots[i]));
}

SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,uint seed=31){var m=mons??[Mon("ALAKAZAM",100,"TAUNT","TORMENT","SPITE","ATTRACT"),Mon("NIDORINO",100,"DEFENSE_CURL","TACKLE"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int position=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,position,new(target)));Check(r.Succeeded,r.Fault??"accepted action");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=positions?[c.Slot.Value]??0;var move=catalog.Moves[pos<0?WildBattleRules.StruggleMove:c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted turn");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid identity");return r;}
SourceFourActionResult Step(SourceFourActionResult r){var f=r.State;var token=r.Continuation!.Value;SourceFourActionResult Run(SourceBattleFrame x)=>x.FourTurn!.Phase switch{
 SourceFourActionPhase.RewardsPending=>x.Rewards!.Pending is {} pending?SourceFourRewards.Learn(catalog,x,token,new(pending.Recipient,pending.Moves[0].Level,pending.Moves[0].Move,null)):SourceFourRewards.Settle(catalog,x,token),
 SourceFourActionPhase.ReplacementsPending=>SourceFourActionTurns.ChooseReplacement(catalog,x,token,x.PendingSwitches[0].Slot,new(x.PendingSwitches[0].Slot.Side,2)),
 _=>SourceFourActionTurns.Advance(catalog,x,token)};
 var actual=Run(f);var replay=Run(JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!);Check(Json(actual)==Json(replay),"complete JSON continuation equality");return Ok(actual);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var events=r.Events.ToList();int guard=0;while(r.State.FourTurn is not null){Check(++guard<40,"bounded completion");r=Step(r);events.AddRange(r.Events);}Check(r.CompletedCleanTurn||r.State.Outcome!=WildBattleOutcome.Ongoing,"clean turn or ended");return(r.State,events);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Turn(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>Finish(Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,positions,targets))));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string reason)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,reason+":"+r.Fault);
var labels=new List<string>();void Test(string label,Action run){run();labels.Add(label);Console.WriteLine("PASS "+label);}
Test("all four actual slots retain selected target and source PP",()=>{
 foreach(int actor in Enumerable.Range(0,4))foreach(string effect in new[]{"ATTRACT","TAUNT","TORMENT","SPITE"}){
  var m=Enumerable.Range(0,4).Select(i=>Mon("NIDORINO",100,"DEFENSE_CURL")).ToArray();m[actor]=Mon("NIDORINA",100,effect);m[actor^1]=m[actor^1] with{LastUsedMove=Id("DEFENSE_CURL"),CalledVolatile=new(){SubstituteHp=20}};
  var f=Frame(m);var r=Act(f,actor,actor^1);var target=r.State.Slots[actor^1];Check(r.Events.OfType<SourcePpCommandEvent>().Single().After==catalog.Moves[Id(effect)].Pp-1,"base source PP");Check(target.Body!.CalledVolatile!.SubstituteHp==20,"bypasses substitute");
  Check(effect switch{"ATTRACT"=>target.Registers.InfatuationSource==new BattlerSlot(actor),"TAUNT"=>target.Body.CalledVolatile.TauntTurnsRemaining==2,"TORMENT"=>target.Body.CalledVolatile.Tormented,_=>r.Events.OfType<SourceSpiteEvent>().Single().Slot==0},"applied actual effect");
 }
});
Test("Attract gender and Oblivious precede gender checks; Protect and Pressure",()=>{
 foreach(string species in new[]{"NIDORINA","DITTO","SLOWPOKE"}){
  var f=Frame();f=Put(f,0,Mon("NIDORINA",100,"ATTRACT"));f=Put(f,1,Mon(species,100,"DEFENSE_CURL"));var r=Act(f,0,1);Check(r.State.Slots[1].Registers.InfatuationSource is null,"invalid target gender or ability");
  Check(species=="SLOWPOKE"?r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_OBLIVIOUS"):r.Events.OfType<SourceMoveFailedEvent>().Any(),"source failure reason");
 }
 var oblivious=Act(Put(Frame(),1,Mon("SLOWPOKE",100,"DEFENSE_CURL")),0,1);Check(oblivious.State.Slots[1].Body!.CalledVolatile?.TauntTurnsRemaining==2,"Gen III Oblivious does not block Taunt");
 foreach(string move in new[]{"ATTRACT","TAUNT","TORMENT","SPITE"}){
  var f=Put(Frame(),0,Mon("NIDORINA",100,move));f=Put(f,1,Mon("ARTICUNO",100,"DEFENSE_CURL") with{Protected=true,LastUsedMove=Id("DEFENSE_CURL")});var r=Act(f,0,1);
  Check(r.Trace.IsEmpty&&r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"Protect consumes Pressure PP before no accuracy draw");Check(r.State.Slots[1].Body!.CalledVolatile is null&&r.State.Slots[1].Registers.InfatuationSource is null&&!r.Events.OfType<SourceSpiteEvent>().Any(),"protected effect not applied");
 }
});
Test("Transform uses permanent attraction identity in both directions",()=>{
 var f=Frame([Mon("NIDORINO",100,"ATTRACT"),Mon("NIDORINA",100,"TRANSFORM"),Mon("DITTO",100,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);
 var transformed=Act(f,1,2).State;Check(transformed.Slots[1].Body!.Pokemon.Gender==PokemonGender.Genderless,"genderless live copied view");var r=Act(transformed,0,1);Check(r.State.Slots[1].Registers.InfatuationSource==new BattlerSlot(0),"permanent female target still attracted");
 f=Frame([Mon("NIDORINO",100,"ATTRACT"),Mon("DITTO",100,"TRANSFORM"),Mon("NIDORINA",100,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);transformed=Act(f,1,2).State;r=Act(transformed,0,1);Check(r.State.Slots[1].Registers.InfatuationSource is null&&r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="genderless"),"original Ditto cannot become attracted");
 // Canonical creation takes permanent Pokemon; use a natural two-action Transform sequence instead.
 var pair=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("NIDORINA",100,"TRANSFORM")],0,Mon("DITTO",100,"ATTRACT"),new(31)).State!;
 var copy=WildBattleRules.ResolveSourceMoveAction(catalog,pair,new(WildBattleSide.Player,0));Check(copy.Succeeded,"singles transform");
 var male=Mon("NIDORINO",100,"ATTRACT");var active=copy.State! with{Opponent=male,CalledParty=copy.State!.CalledParty! with{Opponent=copy.State.CalledParty!.Opponent.SetItem(0,Member(male))}};
 var attraction=WildBattleRules.ResolveSourceMoveAction(catalog,active,new(WildBattleSide.Opponent,0));Check(attraction.Succeeded&&attraction.State!.Player.InfatuationSource==WildBattleSide.Opponent,"canonical singles correct party lookup accepts genderless transformed view");Check(WildBattleRules.ResolveSourceMoveAction(catalog,attraction.State!,new(WildBattleSide.Player,0)).Succeeded,"next singles transaction validates permanent identity");
});
Test("Taunt cancels queued status before PP then two end-turn decrements and earned Struggle",()=>{
 var f=Put(Frame(),1,Mon("SNORLAX",100,"DEFENSE_CURL"));var r=Turn(f);Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==1&&e.Reason=="taunted"),"mid-turn action cancelled");Check(r.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp==40&&r.State.Slots[1].Body!.CalledVolatile?.TauntTurnsRemaining==1,"no PP and first decrement");Check(SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(1)),"all zero-power moves limited");
 Atomic(r.State,SourceFourActionTurns.BeginActions(catalog,r.State,r.State.Revision,Choices(r.State)),"taunt invalid command is atomic");var next=Turn(r.State,[1,-1,0,0],[1,0,2,3]);Check(next.Events.OfType<SourceAttackStringEvent>().Any(e=>e.Actor.Value==1&&e.CurrentMove==WildBattleRules.StruggleMove),"actual selected Struggle");Check(next.State.Slots[1].Body!.CalledVolatile?.TauntTurnsRemaining is null or 0,"exact expiry after next turn");
 var slower=Frame([Mon("SNORLAX",100,"TAUNT","TACKLE"),Mon("ALAKAZAM",100,"DEFENSE_CURL"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);var late=Turn(slower);Check(late.State.Slots[1].Body!.DefenseStage==7&&late.State.Slots[1].Body!.CalledVolatile?.TauntTurnsRemaining==1,"slower Taunt still starts two-count timer and counts down that turn");
});
Test("Torment only limits selection, preserves queued moves and chosen caller history",()=>{
 var f=Put(Frame(),1,Mon("NIDORINO",100,"DEFENSE_CURL") with{LastUsedMove=Id("DEFENSE_CURL")});var r=Turn(f,[1,0,0,0]);Check(r.State.Slots[1].Body!.DefenseStage==7&&r.State.Slots[1].Body!.CalledVolatile?.Tormented==true,"same move already selected still executes");Check(SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(1)),"torment exhausts only learned move");Atomic(r.State,SourceFourActionTurns.BeginActions(catalog,r.State,r.State.Revision,Choices(r.State)),"torment selected repeat atomic");
 var pair=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("ALAKAZAM",100,"METRONOME")],0,Mon("NIDORINO",100,"DEFENSE_CURL"),new(31)).State!;
 var called=WildBattleRules.ResolveCalledMoveAction(catalog,pair,new(WildBattleSide.Player,0,Id("TORMENT")));Check(called.Succeeded&&called.State!.Player.LastUsedMove==Id("METRONOME")&&called.State.Opponent.CalledVolatile?.Tormented==true,"called recipe keeps selected Metronome last move");
 var limited=called.State! with{Player=called.State!.Player with{CalledVolatile=new(){Tormented=true}}};Check(WildBattleRules.ValidateSourceMoveSelection(catalog,limited,WildBattleSide.Player,0)=="called_move_struggle_required"&&WildBattleRules.ValidateSourceMoveSelection(catalog,limited,WildBattleSide.Player,-1)is null,"chosen caller identity limits next selection");
});
Test("Spite first matching live position and <=1 PP failure avoid extra RNG",()=>{
 foreach(int pp in new[]{0,1,2,8}){var f=Frame();var mon=Mon("NIDORINO",100,"TACKLE","DEFENSE_CURL") with{LastUsedMove=Id("DEFENSE_CURL")};mon=mon with{Pokemon=mon.Pokemon with{Moves=mon.Pokemon.Moves.SetItem(1,mon.Pokemon.Moves[1] with{CurrentPp=pp})}};var r=Act(Put(f,1,mon),0,1,2);Check(r.Trace.Length==(pp<=1?1:2),"accuracy then optional spite draw");Check(pp<=1?!r.Events.OfType<SourceSpiteEvent>().Any():r.Events.OfType<SourceSpiteEvent>().Single().Slot==1,"actual last move position");}
 var none=Act(Frame(),0,1,2);Check(!none.Events.OfType<SourceSpiteEvent>().Any()&&none.Trace.Length==1,"no history no PP draw");
});
Test("Spite empties queued PP without free move or restoration",()=>{
 var f=Frame();var mon=Mon("SNORLAX",100,"DEFENSE_CURL") with{LastUsedMove=Id("DEFENSE_CURL")};mon=mon with{Pokemon=mon.Pokemon with{Moves=mon.Pokemon.Moves.SetItem(0,mon.Pokemon.Moves[0] with{CurrentPp=2})}};var r=Turn(Put(f,1,mon),[2,0,0,0]);Check(r.Events.OfType<SourceSpiteEvent>().Single().PpAfter==0&&r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==1&&e.Reason=="no-pp"),"already selected move loses all PP before execution");Check(r.State.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0].CurrentPp==0&&SourceFourActionTurns.RequiresStruggle(catalog,r.State,new(1)),"permanent PP zero requires next-turn Struggle");
});
Test("Spite zero PP cancels an actual multi-turn lock",()=>{
 var f=Frame([Mon("ALAKAZAM",100,"SPITE"),Mon("NIDORINO",100,"ROLLOUT"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("RATTATA",100,"MINIMIZE")]);var rolled=Act(f,1,2).State;Check(rolled.Slots[1].Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Rollout,"actual retained Rollout lock");var target=rolled.Slots[1].Body!;rolled=Put(rolled,1,target with{Pokemon=target.Pokemon with{Moves=target.Pokemon.Moves.SetItem(0,target.Pokemon.Moves[0] with{CurrentPp=2})}});var r=Act(rolled,0,1);Check(r.State.Slots[1].Registers.Turn?.Lock is null&&r.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Reason=="spite-empty-pp"),"zero PP source cancellation clears actual lock");
});
Test("Spite temporary copy PP never leaks into the restored party slot",()=>{
 var f=Frame([Mon("ALAKAZAM",100,"SPITE"),Mon("NIDORINO",100,"MIMIC"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);f=Put(f,2,f.Slots[2].Body! with{LastUsedMove=Id("DEFENSE_CURL")});var copied=Act(f,1,2).State;copied=Put(copied,1,copied.Slots[1].Body! with{LastUsedMove=Id("DEFENSE_CURL")});var r=Act(copied,0,1);Check(r.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp<5,"copied live PP drained");var original=r.State.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0];Check(original.Move==Id("MIMIC")&&original.CurrentPp==9,"real Mimic PP retained");Check(WildBattleTransformationRules.Restore(r.State.Slots[1].Body!).Pokemon.Moves[0]==original,"restoration uses exact persistent slot");
});
Test("real switch clears attraction by occupant identity and own restrictions",()=>{
 var f=Put(Frame(),0,Mon("NIDORINA",100,"ATTRACT","DEFENSE_CURL"));f=Act(f,0,1).State;f=Put(f,0,f.Slots[0].Body! with{CalledVolatile=new(){Tormented=true,TauntTurnsRemaining=1}});var party=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("NIDORINA",100,"DEFENSE_CURL")))})};
 f=f with{Rewards=f.Rewards! with{Metadata=f.Rewards!.Metadata.Add(new(new(BattleSide.Player,2),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
 var choices=Choices(f,[0,1,0,0]).SetItem(0,new(f.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));var begin=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));var moved=Step(begin);Check(moved.State.Slots[0].Member?.Index==2&&moved.State.Slots[0].Body!.CalledVolatile is null&&moved.State.Slots[1].Registers.InfatuationSource is null,"same species new occupant starts clean and loses source attraction");Finish(moved);
});
Test("faint reward and replacement resume clear restrictions and attraction",()=>{
 var f=Frame([Mon("ALAKAZAM",100,"TACKLE"),Mon("NIDORINO",20,"TACKLE"),Mon("PIDGEY",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);var target=f.Slots[1].Body!;f=Put(f,1,target with{Pokemon=target.Pokemon with{CurrentHp=1},CalledVolatile=new(){Tormented=true,TauntTurnsRemaining=1}});var c=f.Slots[0];f=f with{Slots=f.Slots.SetItem(0,c with{Registers=c.Registers with{InfatuationSource=new(1)}}),Parties=f.Parties.SetItem(1,f.Parties[1] with{Members=f.Parties[1].Members.Add(Member(Mon("NIDORINO",20,"DEFENSE_CURL")))})};
 // A fixed source seed permits the attracted attacker to act and KO.
 var attempt=f with{Rng=new(3)};var r=Step(Ok(SourceFourActionTurns.BeginActions(catalog,attempt,attempt.Revision,Choices(attempt))));Check(r.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending,"actual reward boundary");Check(r.State.Slots[1].Body!.CalledVolatile is null&&r.State.Slots[0].Registers.InfatuationSource is null,"faint clears own volatile and source relationship before EXP");var done=Finish(r);Check(done.State.Rewards!.CompletedOpponentMask==1&&done.State.Slots[1].Member?.Index==2&&done.State.Slots[1].Body!.CalledVolatile is null,"actual EXP once and clean replacement");
});
Test("queued move replacement is checked against active Taunt and repeated Taunt cannot refresh",()=>{
 var f=Put(Frame(),1,Mon("SNORLAX",100,"TACKLE","DEFENSE_CURL") with{CalledVolatile=new(){TauntTurnsRemaining=1}});
 var repeat=Act(f,0,1);Check(repeat.State.Slots[1].Body!.CalledVolatile!.TauntTurnsRemaining==1&&repeat.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="already-taunted"),"taunt stays at one");
 var begun=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[1,0,0,0])));var first=Step(begun);var b=first.State.Slots[1].Body!;var changed=Put(first.State,1,b with{Pokemon=b.Pokemon with{Moves=b.Pokemon.Moves.SetItem(0,new(Id("MINIMIZE"),20,20))}});first=first with{State=changed};
 while(first.State.FourTurn is not null&&first.State.NextAction<first.State.Queue.Length&&first.State.Queue[first.State.NextAction].Slot.Value!=1)first=Step(first);
 var blocked=Step(first);Check(blocked.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==1&&e.Move==Id("MINIMIZE")&&e.Reason=="taunted"),"current overwritten move blocked");Check(blocked.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp==20,"no new move PP spent");Finish(blocked);
});
Test("malformed counter and stale commands reject all mutations",()=>{
 var empty=Put(Frame(),1,Frame().Slots[1].Body! with{CalledVolatile=new()});Atomic(empty,SourceFourActionTurns.BeginActions(catalog,empty,empty.Revision,Choices(empty)),"empty unowned volatile rejected");
 var f=Put(Frame(),1,Frame().Slots[1].Body! with{CalledVolatile=new(){TauntTurnsRemaining=3}});var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1)));Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"malformed taunt atomic");f=Frame();Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision+1,Choices(f)),"stale full turn atomic");var family=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasSelectedVolatileRecipe",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;Check(!(bool)family.Invoke(null,[catalog.Moves[Id("METRONOME")].Effect])!,"caller not classified as a selected-volatile recipe");
});
Check(Environment.Version.Major==10,"actual runtime 10");
File.WriteAllText(args[1],Json(new{checks,labels,snapshots})+"\n");Console.WriteLine("PASS "+checks);return 0;
