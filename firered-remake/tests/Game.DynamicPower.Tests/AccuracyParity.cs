using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

internal sealed record AccuracyCase(string Move, string Context, uint Seed, bool Reverse, string Sha256,
    string WithoutPpCursorSha256, bool Hit, int? AccuracyRoll, int Draws, ulong PpCursor);
internal static class AccuracyParity
{
    internal static List<AccuracyCase> Capture(SourceMechanicsCatalog proof, bool hustleBoundary=false)
    {
        var catalog=proof.Catalog;var rows=new List<AccuracyCase>();
        MoveId Id(string s)=>new("pret:MOVE_"+s);
        WildBattleCombatant Mon(string species){
            var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),80,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
            return new(p with{Moves=[new(Id("METRONOME"),10,10)]});
        }
        string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))).ToLowerInvariant();
        foreach(string move in new[]{"FLAIL","REVERSAL","LOW_KICK"})foreach(string context in hustleBoundary?new[]{"hustle"}:new[]{"plain","hustle","lock-on","fly","dive","dig"})foreach(uint seed in hustleBoundary?new uint[]{58}:new uint[]{0,17,31,127})foreach(bool reverse in new[]{false,true}){
            var side=reverse?WildBattleSide.Opponent:WildBattleSide.Player;var other=reverse?WildBattleSide.Player:WildBattleSide.Opponent;
            var actor=Mon(context=="hustle"?"TOGEPI":"CLEFAIRY");var target=Mon("CLEFAIRY");
            if(context=="bright-powder")target=target with{HeldItem="ITEM_BRIGHT_POWDER"};
            if(context=="lax-incense")target=target with{HeldItem="ITEM_LAX_INCENSE"};
            var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
            if(context is "lock-on" or "fly" or "dive" or "dig"){
                var preparation=WildBattleRules.ResolveCalledMoveAction(catalog,state,new(context=="lock-on"?side:other,0,Id(context=="lock-on"?"LOCK_ON":context.ToUpperInvariant())));
                if(!preparation.Succeeded)throw new Exception(context+" preparation: "+preparation.Fault);
                state=preparation.State! with{Rng=new(seed)};
                if(context=="lock-on")state=reverse?state with{Opponent=state.Opponent with{AccuracyStage=0}}:state with{Player=state.Player with{AccuracyStage=0}};
            }
            var result=WildBattleRules.ResolveCalledMoveAction(catalog,state,new(side,0,Id(move)));
            if(!result.Succeeded)throw new Exception(context+" "+move+": "+result.Fault);
            var normalized=result with{Battle=result.Battle with{Events=result.Battle.Events.Select(e=>e is WildBattlePpCommandEvent pp?pp with{RngCursor=0}:e).ToImmutableArray()}};
            var accuracy=result.Battle.Trace.Where(t=>t.Purpose.EndsWith(".accuracy")).ToArray();
            rows.Add(new(move,context,seed,reverse,Hash(result),Hash(normalized),result.Battle.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Damage>0),accuracy.Length==0?null:accuracy.Single().Raw%100+1,result.Battle.Trace.Length,result.Battle.Events.OfType<WildBattlePpCommandEvent>().Single().RngCursor));
        }
        return rows;
    }
}
