using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length!=1)return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
WildBattleCombatant Mon(string item="ITEM_NONE",uint status=0,int confusion=0){var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_MAGIKARP"),30,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;return new(p with{Moves=[new(new("pret:MOVE_SPLASH"),40,40)],CurrentHp=20},Status:status,HeldItem:item){ConfusionTurnsRemaining=confusion};}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant m)=>new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,[Member(m),Member(Mon())]),new(BattleSide.Opponent,[Member(Mon()),Member(Mon())])],Slots=[new(new(0),new(BattleSide.Player,0),1,m),new(new(1),new(BattleSide.Opponent,0),1,Mon()),new(new(2),new(BattleSide.Player,1),1,Mon()),new(new(3),new(BattleSide.Opponent,1),1,Mon())]};
var receipts=new List<object>();
foreach(var (item,status,confusion) in new[]{("ITEM_NONE",0u,0),("ITEM_ORAN_BERRY",0u,0),("ITEM_CHERI_BERRY",64u,0),("ITEM_CHESTO_BERRY",3u,0),("ITEM_PECHA_BERRY",8u,0),("ITEM_RAWST_BERRY",16u,0),("ITEM_ASPEAR_BERRY",32u,0),("ITEM_PERSIM_BERRY",0u,4),("ITEM_CHOICE_BAND",0u,0),("ITEM_FOCUS_BAND",0u,0),("ITEM_KINGS_ROCK",0u,0),("ITEM_SHELL_BELL",0u,0)}){
 var m=Mon(item,status,confusion);var entry=SourceFourEntryAbilities.InitializeEntry(catalog,Frame(m),0);if(!entry.Succeeded)throw new Exception(entry.Fault);var f=entry.State;
 var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot)).ToImmutableArray());if(!r.Succeeded)throw new Exception(r.Fault);
 receipts.Add(new{item,kind="entry",result=entry});receipts.Add(new{item,kind="begin",result=r});
 while(r.State.FourTurn is not null&&r.Continuation is{} c){r=SourceFourActionTurns.Advance(catalog,r.State,c);if(!r.Succeeded)throw new Exception(r.Fault);receipts.Add(new{item,kind="advance",result=r});}
 if(confusion!=0)continue;
 var s=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[m],0,Mon(),new(31));if(!s.Succeeded)throw new Exception(s.Fault);receipts.Add(new{item,kind="single-entry",result=s});
 var t=WildBattleRules.ResolveMove(catalog,s.State!,0);if(!t.Succeeded)throw new Exception(t.Fault);receipts.Add(new{item,kind="single-turn",result=t});
}
// Freeze actual public run receipts across the pre-Smoke-Ball and completed implementations.
foreach (string species in new[] { "MAGIKARP", "RATTATA" }) foreach (uint seed in new uint[] { 0, 31, 12345 })
{
 var p = SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),30,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 var move = new MoveId(species == "RATTATA" ? "pret:MOVE_TAIL_WHIP" : "pret:MOVE_SPLASH");
 var runner = new WildBattleCombatant(p with{Moves=[new(move,catalog.Moves[move].Pp,catalog.Moves[move].Pp)]});
 var foe = SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_MAGIKARP"),80,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 var entry = WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[runner],0,new(foe with{Moves=[new(new("pret:MOVE_SPLASH"),40,40)]}),new(seed));
 if(!entry.Succeeded)throw new Exception(entry.Fault);
 var before = entry.State! with{RunAttempts=7};
 var result = WildBattleRules.Run(catalog,before);if(!result.Succeeded)throw new Exception(result.Fault);
 receipts.Add(new{kind="ordinary-run-baseline",species,seed,before,result});
}
Console.WriteLine(JsonSerializer.Serialize(receipts));Console.WriteLine("HELD_BASELINE_PARITY_END");return 0;
