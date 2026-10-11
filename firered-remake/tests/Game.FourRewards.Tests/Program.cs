using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Growth;
using Game.Domain.WildBattle;

if (args.Length != 1) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
int groups = 0, checks = 0, failures = 0;
void Check(bool value, string label) { checks++; if (!value) throw new InvalidDataException(label); }
void Test(string label, Action body) { groups++; try { body(); Console.WriteLine("PASS " + label); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + label + " " + ex); } }
string Json<T>(T value) => JsonSerializer.Serialize(value);
MoveId Id(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species="PIDGEY", int level=50, string move="TACKLE", string item="ITEM_NONE", int? hp=null) {
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with { CurrentHp=hp??p.CurrentHp, Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)] }, HeldItem:item);
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,WildBattleTransformationRules.PersistentStatus(m),KnockedOffItem:m.KnockedOffItem);
SourceRewardMetadata Facts(int index)=>new(new(BattleSide.Player,index),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,int playerReserve=0,int opponentReserve=0) {
 var m=supplied??[Mon("RATTATA",50,"QUICK_ATTACK"),Mon("PIDGEY",5,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")];
 var p=new[]{Member(m[0]),Member(m[2])}.Concat(Enumerable.Range(0,playerReserve).Select(_=>Member(Mon(move:"BARRIER")))).ToImmutableArray();
 var o=new[]{Member(m[1]),Member(m[3])}.Concat(Enumerable.Range(0,opponentReserve).Select(_=>Member(Mon("RATTATA",50,"BARRIER")))).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,p),new(BattleSide.Opponent,o)],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,p.Length).Select(Facts).ToImmutableArray()}};
}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant body) {var c=f.Get(new(slot));var k=c.Member!.Value;var p=f.Party(k.Side);return f with {Slots=f.Slots.SetItem(slot,c with {Body=body}),Parties=f.Parties.SetItem((int)k.Side,p with {Members=p.Members.SetItem(k.Index,Member(body))})};}
SourceTurnContinuation Token(SourceBattleFrame f)=>new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn!.Phase);
SourceFourActionResult Ok(SourceFourActionResult r) {Check(r.Succeeded,"accepted "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f,int[]? targets=null)=>Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(targets?[c.Slot.Value]??(c.Slot.Value^1)))).ToImmutableArray()));
SourceFourActionResult Next(SourceFourActionResult r)=>Ok(SourceFourActionTurns.Advance(catalog,r.State,Token(r.State)));
SourceFourActionResult Settle(SourceFourActionResult r)=>Ok(SourceFourRewards.Settle(catalog,r.State,Token(r.State)));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string label)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,label+" atomic "+r.Fault);
SourceFourActionResult ToReward(SourceBattleFrame f) {var r=Begin(f);for(int i=0;i<6&&r.State.FourTurn?.Phase!=SourceFourActionPhase.RewardsPending;i++)r=Next(r);Check(r.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending,"actual reward pause");return r;}
SourceFourActionResult Finish(SourceFourActionResult r) {for(int i=0;i<20&&r.State.FourTurn is not null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++) {
 if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending) {var p=r.State.PendingSwitches[0];r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,Token(r.State),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0])));} else r=Next(r);
 } return r;}
Test("replaced target composition retains intermediate Lightning Rod and source random-target draw order",()=>{
 var f=Frame([Mon(move:"MEDITATE"),Mon("RHYDON",50,"SCARY_FACE"),Mon("RATTATA",50,"TACKLE"),Mon("PIDGEY",50,"TACKLE")]);
 Check(f.Slots[1].Body!.Pokemon.AbilityId=="ABILITY_LIGHTNING_ROD","actual source Rod identity");f=f with{Rng=new(75120883)};
 var electric=catalog.Moves[Id("THUNDERBOLT")];var living=SourceMoveTargeting.ResolveReplaced(f,new(0),electric);
 Check(living.Succeeded&&living.Target==new BattlerSlot(1)&&living.LightningRodRedirected&&living.Trace.Select(t=>t.Raw).SequenceEqual(new ushort[]{5834,35451}),"GetMoveTarget redirects once and normal stage does not redraw or lose its flag");
 var dead=Body(f,1,f.Slots[1].Body! with{Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=0}});dead=dead with{DeferredKoVacancyMask=2};
 var fallback=SourceMoveTargeting.ResolveReplaced(dead,new(0),electric);
 Check(fallback.Succeeded&&fallback.Target==new BattlerSlot(3)&&!fallback.LightningRodRedirected&&fallback.Trace.Select(t=>t.Raw).SequenceEqual(new ushort[]{5834,35451}),"retained fainted Rod is an intermediate source target; normal deferred fallback chooses living partner without fake Rod flag");
 var none=Body(dead,3,dead.Slots[3].Body! with{Pokemon=dead.Slots[3].Body!.Pokemon with{CurrentHp=0}});none=none with{DeferredKoVacancyMask=10};var failed=SourceMoveTargeting.ResolveReplaced(none,new(0),electric);
 Check(failed.Succeeded&&failed.Target is null&&failed.Trace.IsEmpty&&failed.Rng==none.Rng,"approved both-vacant boundary avoids impossible source rejection loop");
 var random=SourceMoveTargeting.ResolveReplaced(f,new(0),catalog.Moves[Id("THRASH")]);
 Check(random.Succeeded&&random.Trace.Length==2&&random.Trace[0].Purpose.EndsWith(".called.random-flank")&&random.Trace[1].Purpose.EndsWith(".learned.random-flank"),"random target consumes each of the original two distinct source stages");
 var over=f with{Rng=f.Rng with{Cursor=ulong.MaxValue-1}};var rejected=SourceMoveTargeting.ResolveReplaced(over,new(0),catalog.Moves[Id("THRASH")]);
 Check(!rejected.Succeeded&&rejected.Trace.IsEmpty&&rejected.Rng==over.Rng,"second targeting stage overflow rejects both stages atomically");
});
Test("learning over the queued move retargets with original GetMoveTarget RNG then finishes the actual turn",()=>{
 foreach(int learnerSlot in new[]{0,2}) {
  var d=Mon("DROWZEE",30,"MEDITATE");d=d with{Pokemon=d.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[d.Pokemon.Species].GrowthRate,31)-1,
   Moves=new[]{"MEDITATE","POUND","HYPNOSIS","DISABLE"}.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()}};
  var a=Mon("RATTATA",70,"QUICK_ATTACK");var f=Frame([learnerSlot==0?d:a,Mon("PIDGEY",20,"TACKLE",hp:1),learnerSlot==2?d:a,Mon("SLOWPOKE",50,"AMNESIA",hp:1)],opponentReserve:2);
  f=f with{Field=new(){SandstormTurnsRemaining=3}};var targets=learnerSlot==0?new[]{0,0,1,3}:new[]{1,0,2,3};
  var r=Next(Begin(f,targets));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"real partner KO pauses original queued learner");r=Settle(r);
  var pending=r.State.Rewards!.Pending!;Check(pending.Recipient.Index==learnerSlot/2&&pending.Moves[0].Move==Id("PSYCHIC"),"actual level31 Psychic decision");
  foreach(int? replace in new int?[]{null,1,0}) {
   var learned=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,31,Id("PSYCHIC"),replace)));
   Check(learned.State.Queue.Single(q=>q.Slot.Value==learnerSlot).OriginalChosenMove==Id("MEDITATE"),"original selected identity survives all learning choices");
   while(learned.State.Queue[learned.State.NextAction].Slot.Value!=learnerSlot)learned=Next(learned);
   var before=learned.State;var clone=JsonSerializer.Deserialize<SourceBattleFrame>(Json(before))!;var action=Next(learned);
   Check(Json(action)==Json(SourceFourActionTurns.Advance(catalog,clone,Token(clone))),"serialized queued identity yields exact events and RNG");
   var used=action.Events.OfType<SourceMoveUsedEvent>().Single();
   if(replace==0) {
    Check(used.Move==Id("PSYCHIC")&&used.Actor.Value==learnerSlot&&used.Target.Value==3,"replaced self-target becomes surviving opponent target");
    // Compiled unmodified GetMoveTarget, initial seed75120883, source/deferred absent mask2:
    // 5834%4 rejects same-side slot2; 35451%4 accepts slot3. No second selected-target draw.
    var draws=action.Trace.Where(t=>t.Purpose.Contains(".target.")).ToArray();
    Check(before.Rng.Seed==75120883&&draws.Select(t=>t.Raw).SequenceEqual(new ushort[]{5834,35451})&&draws.All(t=>t.Purpose.EndsWith(".called.selected-rejection")),"exact independently compiled original C rejection-loop draws");
    Check(action.Events.OfType<SourcePpCommandEvent>().Single().PpSlot==0&&action.State.Get(new(learnerSlot)).Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("PSYCHIC")].Pp-1,"new selected move spends its actual PP once");
    Atomic(before,SourceFourActionTurns.Advance(catalog,before,Token(before) with{EventSequence=before.EventSequence-1}),"retarget stale continuation");
    var overflow=before with{Rng=before.Rng with{Cursor=ulong.MaxValue}};Atomic(overflow,SourceFourActionTurns.Advance(catalog,overflow,Token(overflow)),"retarget RNG overflow");
   } else Check(used.Move==Id("MEDITATE")&&used.Target.Value==learnerSlot&&!action.Trace.Any(t=>t.Purpose.Contains(".target.")),"decline and other-slot replacement retain chosen target without retarget RNG");
   var done=Finish(action);Check(done.CompletedCleanTurn&&done.State.Field.SandstormTurnsRemaining==2&&done.State.Rewards!.CompletedOpponentMask==3,"remaining actions rewards residual and both reserve entries finish exactly once");
   Check(done.State.Slots[1].Member!.Value.Index==2&&done.State.Slots[3].Member!.Value.Index==3,"distinct original-party reserve entries");
  }
  var old=r.State with{FourTurn=r.State.FourTurn! with{Version="firered-four-action-reward-continuation-v3"}};Atomic(old,SourceFourRewards.Settle(catalog,old,Token(old)),"v3 lacks selected identity contract");
  var missing=r.State with{Queue=r.State.Queue.Select(q=>q.Slot.Value==learnerSlot?q with{OriginalChosenMove=null}:q).ToImmutableArray()};Atomic(missing,SourceFourRewards.Settle(catalog,missing,Token(missing)),"v4 requires original chosen identity");
 }
});
Test("real KO applies party-ordered shared rewards then resumes remaining three actions and residuals",()=>{
 var f=Frame();var r=ToReward(f);Check(r.State.NextAction==1,"first action KO");var before=r.State;var expected=SourceVictoryRewards.Calculate(proof,before.Party(BattleSide.Player),before.Rewards!.Metadata,new(before.FourTurn!.Defeats[0].Occupant,before.Get(new(1)).Body!,[new(BattleSide.Player,0),new(BattleSide.Player,1)]),before.Rewards.Context!);
 r=Settle(r);Check(r.Events.OfType<SourceRewardAppliedEvent>().Select(e=>e.Recipient.Index).SequenceEqual(new[]{0,1}),"party order");
 foreach(var x in expected.Members)Check(Json(r.State.Party(BattleSide.Player).Members[x.Key.Index].Pokemon)==Json(x.Reward.Pokemon),"real shared calculation");
 Check(r.State.Rewards!.CompletedOpponentMask==1&&r.State.FourTurn!.Defeats.IsEmpty,"bounded completion retired token");
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions&&r.State.NextAction==1,"remaining cursor");
 var events=new List<SourceBattleEvent>();while(r.State.FourTurn is not null){r=Next(r);events.AddRange(r.Events);}Check(r.State.Turn==1&&events.OfType<SourceMoveUsedEvent>().Count()==2,"two surviving original actors and no inherited fainted action");
 Check(r.State.SourceAbsentMask==2&&r.State.DeferredKoVacancyMask==0,"unfilled defeated position becomes absent");
});
Test("poison KO retains normal residual cursor and weather ticks only once",()=>{
 var f=Frame([Mon(move:"BARRIER"),Mon("RATTATA",50,"BARRIER",hp:1) with{Status=WildBattleStatus.Poison},Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);f=f with {Field=new(){RainTurnsRemaining=3}};
 var r=ToReward(f);Check(r.State.NextAction==4&&r.State.FourTurn!.EndTurn?.NextBattler==0&&r.State.Field.RainTurnsRemaining==2,"normal residual boundary");
 var token=r.State.FourTurn!.EndTurn;var rng=r.State.Rng;r=Settle(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.EndTurnResume&&r.State.FourTurn.EndTurn==token,"same residual cursor");
 r=Next(r);Check(r.CompletedCleanTurn&&r.State.Field.RainTurnsRemaining==2&&!r.Events.OfType<SourceStatusDamageEvent>().Any(),"no residual replay");Check(r.State.Rng.Cursor==rng.Cursor+1,"only ready random draw");
});
Test("due delayed KO pauses with numeric target cursor and never reruns earlier residuals",()=>{
 var f=Frame([Mon(move:"BARRIER"),Mon("RATTATA",50,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);
 f=f with{Field=new(){RainTurnsRemaining=3},Slots=f.Slots.SetItem(1,f.Slots[1] with {Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(2),10000)}})};
 var r=ToReward(f);Check(r.State.FourTurn!.EndTurn is {Stage:WildBattleSourceEndTurnStage.FutureAttacks,NextBattler:2},"resume after actual target slot1");
 Check(r.State.Get(new(1)).Position.FutureAttack is null&&r.State.Field.RainTurnsRemaining==2,"timer consumed once");var before=r.State.Rng;r=Settle(r);r=Next(r);
 Check(r.CompletedCleanTurn&&r.State.Field.RainTurnsRemaining==2&&!r.Events.OfType<SourceHpChangedEvent>().Any()&&r.State.Rng.Cursor==before.Cursor+1,"delayed hit not replayed");
});
Test("every delayed resume position comes from a real callback and cannot replay ordinary residuals",()=>{
 foreach(int target in new[]{0,1,2,3}){
  var f=Frame([Mon(move:"BARRIER"),Mon("PIDGEY",50,"BARRIER"),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);f=f with{Field=new(){RainTurnsRemaining=3},Slots=f.Slots.SetItem(target,f.Slots[target] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(target^1),3)}})};
  var r=Begin(f);for(int i=0;i<4;i++)r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.EndTurn&&r.State.FourTurn.EndTurn is null,"ordinary phase has no resume cursor");r=Next(r);
  Check(r.State.FourTurn!.Phase==SourceFourActionPhase.EndTurnResume&&r.State.FourTurn.EndTurn!.NextBattler==target+1&&r.Events.OfType<SourceCalledDelayedEvent>().Single().Target.Value==target,"actual delayed callback establishes exact numeric resume");
  var missing=r.State with{FourTurn=r.State.FourTurn with{EndTurn=null,EndTurnOrder=[]}};Atomic(missing,SourceFourActionTurns.Advance(catalog,missing,Token(missing)),"resume cannot discard cursor and replay ordinary work");
  var replay=r.State with{FourTurn=r.State.FourTurn with{Phase=SourceFourActionPhase.EndTurn}};Atomic(replay,SourceFourActionTurns.Advance(catalog,replay,Token(replay)),"resume cursor incompatible with fresh ordinary phase");
  var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var resumed=Next(r);Check(Json(resumed)==Json(SourceFourActionTurns.Advance(catalog,copy,Token(copy))),"actual delayed checkpoint JSON exact");
  Check(resumed.CompletedCleanTurn&&resumed.State.Field.RainTurnsRemaining==2&&!resumed.Events.OfType<SourceCalledDelayedEvent>().Any()&&!resumed.Events.OfType<SourceMtMoonVolatileEvent>().Any(),"neither normal timers nor earlier callback repeats");
 }
});
Test("due attacks skip absent positions and consume fainted-present timer without a fabricated reward",()=>{
 foreach(bool absent in new[]{false,true}){
  var f=Frame([Mon(move:"BARRIER"),Mon("RATTATA",50,"BARRIER",hp:0),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);
  f=f with {SourceAbsentMask=absent?(byte)2:(byte)0,TurnStartAbsentMask=absent?(byte)2:(byte)0,Slots=f.Slots.SetItem(1,f.Slots[1] with{Position=new(){FutureAttack=new(1,Id("DOOM_DESIRE"),new(2),1000)}})};
  var r=Finish(Begin(f));Check(r.CompletedCleanTurn&&r.State.Rewards!.CompletedOpponentMask==0,"no fake faint/award");Check((r.State.Get(new(1)).Position.FutureAttack is not null)==absent,"source absence checked before timer");
 }
});
Test("two opposing occupants settle independently and terminal outcome waits for both",()=>{
 var f=Frame([Mon("RATTATA",50,"QUICK_ATTACK"),Mon("PIDGEY",1,"BARRIER",hp:1),Mon("SPEAROW",50,"PECK"),Mon("DODUO",1,"BARRIER",hp:1)]);
 var r=ToReward(f);r=Settle(r);Check(r.State.Rewards!.CompletedOpponentMask==1,"first identity completed");while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);
 Check(r.State.FourTurn!.OutcomeAfterRewards==WildBattleOutcome.Won&&r.State.Outcome==WildBattleOutcome.Ongoing,"second terminal deferred");r=Settle(r);
 Check(r.State.Rewards!.CompletedOpponentMask==3&&r.State.Outcome==WildBattleOutcome.Won&&r.Events.OfType<SourceEndedEvent>().Count()==1,"each opponent credited once before Won");
});
Test("EXP Share Lucky Egg trainer traded Pokerus and Macho Brace use canonical owner calculation",()=>{
 var f=Frame(playerReserve:1);f=Body(f,0,f.Slots[0].Body! with{HeldItem="ITEM_LUCKY_EGG"});f=Body(f,2,f.Slots[2].Body! with{HeldItem="ITEM_MACHO_BRACE"});
 var p=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,p with{Members=p.Members.SetItem(2,p.Members[2] with{HeldItem="ITEM_EXP_SHARE"})}),Rewards=f.Rewards! with {Metadata=f.Rewards.Metadata.SetItem(0,Facts(0) with{OriginalTrainerName="BLUE"}).SetItem(1,Facts(1) with{PokerusHistory=SourceRewardPokerusHistory.InfectedOrCured})}};
 var r=ToReward(f);r=Settle(r);var grants=r.Events.OfType<SourceRewardAppliedEvent>().ToArray();Check(grants.Select(x=>x.Recipient.Index).SequenceEqual(new[]{0,1,2}),"party ordered participant and bench share");
 Check(grants[0].Reward.ExperienceGained>grants[1].Reward.ExperienceGained&&grants[1].Reward.EvsGained.Speed==4&&grants[2].Reward.EvsGained.Speed==1,"modifiers and full EVs applied");
 // Same priority, genuine source Speed on Rattata exceeds Pidgey until Macho Brace halves it.
 var order=Frame([Mon("RATTATA",50,"TACKLE"),Mon("PIDGEY",50,"TACKLE"),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);var plain=Begin(order).State.Queue.Select(a=>a.Slot.Value).ToArray();order=Body(order,0,order.Slots[0].Body! with{HeldItem="ITEM_MACHO_BRACE"});var braced=Begin(order).State.Queue.Select(a=>a.Slot.Value).ToArray();Check(Array.IndexOf(plain,0)<Array.IndexOf(plain,1)&&Array.IndexOf(braced,0)>Array.IndexOf(braced,1),"Macho Brace changes real shared action order");
});
Test("both active player slots refresh all six canonical stats on level gain",()=>{
 var f=Frame();foreach(int slot in new[]{0,2}){var b=f.Slots[slot].Body!;b=b with{Pokemon=b.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[b.Pokemon.Species].GrowthRate,51)-1,CurrentHp=10}};f=Body(f,slot,b);}
 var r=Settle(ToReward(f));foreach(int slot in new[]{0,2}){var b=r.State.Get(new(slot)).Body!;var p=r.State.Party(BattleSide.Player).Members[slot/2].Pokemon;Check(b.Pokemon.Level==51&&b.Pokemon.Stats==p.Stats&&b.Pokemon.Stats.SpecialDefense>f.Get(new(slot)).Body!.Pokemon.Stats.SpecialDefense,"all stats including right SpDef");Check(b.Pokemon.CurrentHp==10+b.Pokemon.Stats.MaxHp-f.Get(new(slot)).Body!.Pokemon.Stats.MaxHp,"injury preserved");}
});
Test("genuine learning pauses decline and replacement preserve PP Ups and reject HM atomically",()=>{
 var f=Frame([Mon("RATTATA",12,"QUICK_ATTACK"),Mon("PIDGEY",20,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);var b=f.Slots[0].Body!;
 var names=new[]{"QUICK_ATTACK","CUT","TACKLE","BARRIER"};b=b with{Pokemon=b.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[b.Pokemon.Species].GrowthRate,13)-1,Moves=names.Select((n,i)=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp,i==0?0:0)).ToImmutableArray()}};
 b=b with{Pokemon=b.Pokemon with{Moves=b.Pokemon.Moves.SetItem(2,new(Id("TACKLE"),7,catalog.Moves[Id("TACKLE")].Pp*160/100,3))}};f=Body(f,0,b);
 var r=Settle(ToReward(f));Check(r.State.Rewards!.Pending is {Recipient.Index:0}&&r.Events.OfType<SourceRewardAppliedEvent>().Count()==1,"pause before next party recipient");var pending=r.State.Rewards.Pending!;var candidate=pending.Moves[0];Check(candidate.Move==Id("HYPER_FANG"),"real source level13 learnset");
 Atomic(r.State,SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,candidate.Level,candidate.Move,1)),"HM cannot be forgotten");
 var replacement=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,candidate.Level,candidate.Move,3)));
 Check(replacement.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[3].Move==Id("HYPER_FANG")&&replacement.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[3].PpUps==0,"new slot full source PP zero ups");Check(replacement.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[2].PpUps==3&&replacement.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[2].CurrentPp==7,"other slot bonuses/current PP retained");
 var declined=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,candidate.Level,candidate.Move,null)));Check(declined.State.Rewards!.Pending is null&&declined.State.Rewards.CompletedOpponentMask==1&&!declined.State.Party(BattleSide.Player).Members[0].Pokemon.Moves.Any(m=>m.Move==candidate.Move),"decline then actual second recipient and completion");Check(declined.Events.OfType<SourceRewardAppliedEvent>().Single().Recipient.Index==1,"first recipient not credited twice");
 var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var restored=SourceFourRewards.Learn(catalog,copy,Token(copy),new(pending.Recipient,candidate.Level,candidate.Move,null));Check(Json(declined)==Json(restored),"pending learning serializes exact deterministic continuation");
 Atomic(declined.State,SourceFourRewards.Learn(catalog,declined.State,Token(r.State),new(pending.Recipient,candidate.Level,candidate.Move,null)),"duplicate learning");
});
Test("later learning candidate reevaluates after prior decision instead of freezing known moves",()=>{
 var f=Frame([Mon("BULBASAUR",14,"QUICK_ATTACK"),Mon("PIDGEY",20,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")]);var b=f.Slots[0].Body!;
 b=b with{Pokemon=b.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[b.Pokemon.Species].GrowthRate,15)-1,Moves=new[]{"QUICK_ATTACK","SLEEP_POWDER","TACKLE","CUT"}.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()}};f=Body(f,0,b);
 var r=Settle(ToReward(f));var pending=r.State.Rewards!.Pending!;Check(pending.Moves.Select(m=>m.Move).SequenceEqual(new[]{Id("POISON_POWDER"),Id("SLEEP_POWDER")}),"source same-level pending queue retains currently known later candidate");
 Atomic(r.State,SourceFourRewards.Settle(catalog,r.State,Token(r.State)),"current-token duplicate credit blocked during learning");
 var malformed=r.State with{Rewards=r.State.Rewards with{Pending=pending with{Moves=[new(15,Id("SPLASH"))]}}};Atomic(malformed,SourceFourRewards.Learn(catalog,malformed,Token(malformed),new(pending.Recipient,15,Id("SPLASH"),0)),"forged candidate rejected by canonical learnset");
 var declined=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,15,Id("POISON_POWDER"),null)));Check(declined.State.Rewards!.Pending is null&&declined.Events.OfType<SourceRewardLearningEvent>().Any(e=>e.Candidate.Move==Id("SLEEP_POWDER")&&e.Outcome==SourceRewardLearningOutcome.AlreadyKnown),"later known candidate skips after decline");
 var replaced=Ok(SourceFourRewards.Learn(catalog,r.State,Token(r.State),new(pending.Recipient,15,Id("POISON_POWDER"),1)));Check(replaced.State.Rewards!.Pending!.Moves.Single().Move==Id("SLEEP_POWDER")&&!replaced.Events.OfType<SourceRewardAppliedEvent>().Any(),"replacing old Sleep Powder creates genuine next learning choice without repeat EXP");
 var finished=Ok(SourceFourRewards.Learn(catalog,replaced.State,Token(replaced.State),new(pending.Recipient,15,Id("SLEEP_POWDER"),null)));Check(finished.State.Rewards!.CompletedOpponentMask==1&&finished.Events.OfType<SourceRewardAppliedEvent>().Single().Recipient.Index==1,"completion continues next actual party recipient once");
});
Test("all100 and actual simultaneous no-living-recipient KOs complete empty rewards before terminal",()=>{
 var f=Frame([Mon("RATTATA",100,"QUICK_ATTACK"),Mon("PIDGEY",1,"BARRIER",hp:1),Mon("SPEAROW",100,"BARRIER"),Mon("DODUO",50,"BARRIER",hp:0)]);f=f with{SourceAbsentMask=8,TurnStartAbsentMask=8};var r=ToReward(f);var before=Json(r.State.Party(BattleSide.Player));r=Settle(r);
 Check(!r.Events.OfType<SourceRewardAppliedEvent>().Any()&&r.Events.OfType<SourceOpponentRewardCompletedEvent>().Count()==1&&Json(r.State.Party(BattleSide.Player))==before&&r.State.Outcome==WildBattleOutcome.Won,"all100 verified empty plan before Won");
 f=Frame([Mon("GEODUDE",50,"EXPLOSION"),Mon("PIDGEY",1,"BARRIER",hp:1),Mon("SPEAROW",1,"BARRIER",hp:1),Mon("DODUO",1,"BARRIER",hp:1)]);r=ToReward(f);
 Check(r.State.Parties.All(p=>p.Members.All(m=>m.Pokemon.CurrentHp==0))&&r.State.FourTurn!.Defeats.Count(d=>d.RequiresExperience)==2,"actual four-body KO and two defeats");
 for(int i=0;i<2;i++){r=Settle(r);Check(!r.Events.OfType<SourceRewardAppliedEvent>().Any(),"no fabricated zero EXP grant");}
 Check(r.State.Rewards!.CompletedOpponentMask==3&&r.State.Outcome==WildBattleOutcome.Draw,"both actual empty calculations before Draw");
});
Test("two opponent KOs with surviving reserves consume remaining no-target action then enter both replacements",()=>{
 var f=Frame([Mon("RATTATA",100,"SURF"),Mon("PIDGEY",1,"BARRIER",hp:1),Mon("SPEAROW",50,"PECK"),Mon("DODUO",1,"BARRIER",hp:1)],opponentReserve:2);
 var r=ToReward(f);Check(r.State.NextAction==1&&r.State.FourTurn!.Defeats.Count(d=>d.RequiresExperience)==2,"one real spread action defeats both occupants");r=Settle(r);r=Settle(r);
 int failed=0;while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions){r=Next(r);failed+=r.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason=="deferred-ko-no-target");}
 Check(failed==1&&r.State.Party(BattleSide.Player).Members[1].Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("PECK")].Pp-1,"remaining actual action spends original PP and fails");r=Next(r);Check(r.State.PendingSwitches.Length==2&&r.State.Rewards!.CompletedOpponentMask==3,"both replacement choices after rewards and residuals");r=Finish(r);
 Check(r.CompletedCleanTurn&&r.State.Slots.Where(c=>c.Slot.Side==BattleSide.Opponent).Select(c=>c.Member!.Value.Index).SequenceEqual(new[]{2,3}),"both distinct reserves enter and turn completes");
});
Test("no-target failure keeps retained fainted Pressure PP semantics and no hit RNG",()=>{
 var f=Frame([Mon("RATTATA",100,"SURF"),Mon("DUSCLOPS",1,"BARRIER",hp:1),Mon("SPEAROW",50,"PECK"),Mon("DODUO",1,"BARRIER",hp:1)],opponentReserve:1);
 Check(f.Slots[1].Body!.Pokemon.AbilityId=="ABILITY_PRESSURE","genuine Pressure species/personality");var r=Begin(f,[1,0,1,2]);r=Next(r);r=Settle(r);r=Settle(r);
 while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions){var actor=r.State.Queue[r.State.NextAction].Slot;r=Next(r);if(actor.Value==2){Check(r.Trace.IsEmpty&&r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"retained HP0 Pressure costs two with no target/hit RNG");Check(!r.Events.OfType<SourceHpChangedEvent>().Any(),"no fabricated damage");}}
});
Test("no-target queued action still applies earlier sleep flinch and Truant cancellers",()=>{
 foreach(string blocker in new[]{"sleep","flinch","truant"}){
  var actor=Mon(blocker=="truant"?"SLAKOTH":"SPEAROW",50,"PECK");
  actor=blocker switch{"sleep"=>actor with{Status=3},"flinch"=>actor with{Flinched=true},_=>actor with{TruantLoafing=true}};
  var f=Frame([Mon("RATTATA",100,"SURF"),Mon("DUSCLOPS",1,"BARRIER",hp:1),actor,Mon("DODUO",1,"BARRIER",hp:1)],opponentReserve:1);
  var r=Begin(f,[1,0,1,2]);r=Next(r);r=Settle(r);r=Settle(r);bool checkedActor=false;
  while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions){var slot=r.State.Queue[r.State.NextAction].Slot;r=Next(r);if(slot.Value!=2)continue;checkedActor=true;
   Check(r.Trace.IsEmpty&&!r.Events.OfType<SourcePpCommandEvent>().Any()&&!r.Events.OfType<SourceAttackStringEvent>().Any(),blocker+" cancels before no-target PP or attack string");
   Check(r.State.Party(BattleSide.Player).Members[1].Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("PECK")].Pp,blocker+" retains PP despite fainted Pressure");
   Check(!r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="deferred-ko-no-target"),blocker+" owns the failure reason");
  }
  Check(checkedActor,"real remaining action visited");r=Finish(r);Check(r.CompletedCleanTurn,"blocked no-target action continues normal turn");
 }
});
Test("two delayed opposing KOs retain separate callback positions and settlement bits",()=>{
 var f=Frame([Mon(move:"BARRIER"),Mon("PIDGEY",30,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",30,"BARRIER",hp:1)],opponentReserve:1);
 foreach(int slot in new[]{1,3})f=f with{Slots=f.Slots.SetItem(slot,f.Slots[slot] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(2),10000)}})};
 var r=ToReward(f);Check(r.State.FourTurn!.EndTurn!.NextBattler==2,"first delayed target then callback");r=Settle(r);r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending,"first dead target replaced before next callback");
 var choice=r.State.PendingSwitches[0];r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,Token(r.State),choice.Slot,new(BattleSide.Opponent,2)));r=Next(r);
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.EndTurn!.NextBattler==4&&r.State.FourTurn.Defeats.Single(d=>d.RequiresExperience).Occupant.Slot.Value==3,"second exact target callback");r=Settle(r);r=Finish(r);Check(r.CompletedCleanTurn&&r.State.Rewards!.CompletedOpponentMask==3,"both delayed defeats completed once");
});
Test("stored delayed attacker position resolves after its original occupant departed",()=>{
 var f=Frame([Mon(move:"BARRIER"),Mon("PIDGEY",30,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")],playerReserve:1);
 f=f with{Slots=f.Slots.SetItem(1,f.Slots[1] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(2),10000)}})};var r=Begin(f);var outgoing=r.State.Slots[2].Occupant!.Value;
 var reservation=new SourceSwitchReservation(outgoing,new(BattleSide.Player,2),WildBattleCalledSwitchKind.Voluntary);var reserved=SourceBattleIdentityRules.Reserve(r.State,r.State.Revision,reservation);Check(reserved.Succeeded,"real structural reservation");var incoming=new WildBattleCombatant(reserved.State.Party(BattleSide.Player).Members[2].Pokemon);
 var bound=SourceBattleIdentityRules.BindReservedEntry(reserved.State,reserved.State.Revision,reservation,SourceEntryBoundary.Action,incoming,new());Check(bound.Succeeded,"actual new occupant");r=new(bound.State,[],[],null);
 while(r.State.FourTurn!.Phase!=SourceFourActionPhase.RewardsPending)r=Next(r);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Actor.Value==2&&r.State.Slots[2].Occupant!=outgoing,"delayed source is retained position; no stale actor token");r=Settle(r);Check(r.Events.OfType<SourceRewardAppliedEvent>().Select(e=>e.Recipient.Index).SequenceEqual(new[]{0,1,2}),"original and incoming actual participants stay distinct");
});
Test("permanent reward projection preserves a proven Transform view and refreshes six stats",()=>{
 var ditto=Mon("DITTO",50,"TRANSFORM");var donor=Mon("PIDGEY",50,"TACKLE");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[ditto],0,donor,new(7));Check(start.Succeeded,"source transform setup");var transformed=WildBattleRules.ResolveSourceMoveAction(catalog,start.State!,new(WildBattleSide.Player,0));Check(transformed.Succeeded&&transformed.State!.Player.Transform is not null,"real source transform");
 var body=transformed.State!.Player;var permanent=WildBattleTransformationRules.PersistentPokemon(body);permanent=permanent with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[permanent.Species].GrowthRate,51)-1};body=WildBattleTransformationRules.ApplyPersistentPokemon(body,permanent);
 var r=ToReward(Frame());var imported=SinglesBattleAdapter.ImportSlot(new(0),new(BattleSide.Player,0),1,body);var f=Body(r.State,2,imported.Body!);r=new(f,[],[],null);r=Settle(r);
 var final=r.State.Slots[2].Body!;Check(final.Transform is not null&&final.Pokemon.Species==body.Pokemon.Species&&final.Pokemon.Moves.SequenceEqual(body.Pokemon.Moves),"copied view and PP stay intact");Check(r.State.Party(BattleSide.Player).Members[1].Pokemon.Species==permanent.Species&&final.Pokemon.Level==51&&final.Pokemon.Stats==r.State.Party(BattleSide.Player).Members[1].Pokemon.Stats,"permanent species/growth and both-slot all-stat projection");
});
Test("missing facts stale duplicate and overflow settlement reject atomically",()=>{
 var r=ToReward(Frame());var f=r.State;var old=f with {FourTurn=f.FourTurn! with {Version="firered-four-action-shared-end-turn-v2"}};Atomic(old,SourceFourRewards.Settle(catalog,old,Token(old)),"old v2 diagnostic cursor rejected");Atomic(f,SourceFourRewards.Settle(catalog,f,Token(f) with{Revision=f.Revision-1}),"stale revision");Atomic(f,SourceFourRewards.Settle(catalog,f,Token(f) with{EventSequence=f.EventSequence-1}),"stale events");
 var missing=f with{Rewards=f.Rewards! with{Metadata=f.Rewards.Metadata.SetItem(0,Facts(0) with{PokerusHistory=SourceRewardPokerusHistory.Unknown})}};Atomic(missing,SourceFourRewards.Settle(catalog,missing,Token(missing)),"unknown history");
 foreach(var overflow in new[]{f with{Revision=long.MaxValue},f with{EventSequence=long.MaxValue}})Atomic(overflow,SourceFourRewards.Settle(catalog,overflow,Token(overflow)),"counter exhausted");
 var done=Settle(r);Atomic(done.State,SourceFourRewards.Settle(catalog,done.State,Token(f)),"duplicate award");
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;Check(Json(done)==Json(SourceFourRewards.Settle(catalog,restored,Token(restored))),"reward pause JSON exact");
});
Test("actual endturn KO replacement retires defeated occupant and keeps completion across turns",()=>{
 var r=ToReward(Frame(opponentReserve:1));r=Settle(r);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending&&r.State.FourTurn.EndTurn is not null,"after residual real replacement boundary");
 var before=r.State;var old=before.Slots[1].Occupant;var token=Token(before);r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,before,token,new(1),new(BattleSide.Opponent,2)));
 Check(r.State.Slots[1].Member!.Value.Index==2&&r.State.Slots[1].EntrySerial==2&&r.State.Rewards!.CompletedOpponentMask==1,"new occupant with persistent once-only completion");Check(r.State.FourTurn!.Defeats.All(d=>d.Occupant!=old)&&r.State.Rewards!.Participation[2]==3,"old defeat retired and new opponent actual participation");
 Atomic(r.State,SourceFourActionTurns.ChooseReplacement(catalog,r.State,token,new(1),new(BattleSide.Opponent,2)),"stale replacement");r=Next(r);Check(r.CompletedCleanTurn&&r.State.Rewards!.CompletedOpponentMask==1,"replacement continuation completes retained turn");
});
Test("zero or two player reserves resolve KO vacancies without duplicate reserve selection",()=>{
 foreach(int count in new[]{1,2}){
 var f=Frame([Mon("PIDGEY",1,"BARRIER",hp:1),Mon("RATTATA",100,"QUICK_ATTACK"),Mon("SPEAROW",1,"BARRIER",hp:1),Mon("DODUO",100,"PECK")],playerReserve:count);
 var r=Begin(f);while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions)r=Next(r);r=Next(r);Check(r.State.PendingSwitches.Length==count,"bounded available choices");r=Finish(r);Check(r.CompletedCleanTurn&&r.State.Slots.Where(c=>c.Slot.Side==BattleSide.Player&&r.State.CanAct(c.Slot)).Select(c=>c.Member).Distinct().Count()==count,"real distinct living incoming members");}
});
Test("screen expiry weather KO reward and replacement resume share one end-turn cursor",()=>{
 var f=Frame([Mon(move:"BARRIER"),Mon("RATTATA",50,"BARRIER",hp:1),Mon("SPEAROW",50,"BARRIER"),Mon("DODUO",50,"BARRIER")],opponentReserve:1);
 f=f with {Field=new(){SandstormTurnsRemaining=3},Sides=[new(BattleSide.Player){ReflectTurnsRemaining=1,LightScreenTurnsRemaining=2},new(BattleSide.Opponent){MistTurnsRemaining=1,SafeguardTurnsRemaining=2}]};
 var r=ToReward(f);Check(r.State.NextAction==4&&r.State.Field.SandstormTurnsRemaining==2,"weather KO after all original actions");
 Check(r.State.Sides[0].ReflectTurnsRemaining==0&&r.State.Sides[1].MistTurnsRemaining==0&&r.State.Sides[0].LightScreenTurnsRemaining==1&&r.State.Sides[1].SafeguardTurnsRemaining==1,"side timers each run before weather KO");
 var paused=r.State;var oldHp=paused.Slots.Select(c=>c.Body!.Pokemon.CurrentHp).ToArray();var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(paused))!;
 r=Settle(r);Check(Json(r)==Json(SourceFourRewards.Settle(catalog,restored,Token(restored))),"reward continuation round-trip is deterministic");
 r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending,"real replacement waits for settled weather defeat");
 var p=r.State.PendingSwitches.Single();r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,Token(r.State),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0])));
 r=Next(r);Check(r.CompletedCleanTurn&&r.State.Field.SandstormTurnsRemaining==2&&r.State.Sides[0].LightScreenTurnsRemaining==1&&r.State.Sides[1].SafeguardTurnsRemaining==1,"no timer replay after actual replacement");
 Check(new[]{0,2,3}.All(n=>r.State.Get(new(n)).Body!.Pokemon.CurrentHp==oldHp[n]),"other bodies do not take weather twice");
 Check(r.State.Get(new(1)).Body!.Pokemon.CurrentHp==r.State.Get(new(1)).Body!.Pokemon.Stats.MaxHp&&r.State.Rewards!.CompletedOpponentMask==1,"incoming misses completed weather and award stays once");
});
Console.WriteLine($"{groups} groups; {checks} checks; {failures} failures; runtime {Environment.Version}");return groups>0&&failures==0?0:1;
