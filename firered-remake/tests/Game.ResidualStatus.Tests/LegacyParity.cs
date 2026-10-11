using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

static class LegacyParity
{
    public static string Run(string root, bool verifyAdapter = false)
    {
        var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;var records=new List<object>();
        MoveId Id(string s)=>new("pret:MOVE_"+s);
        WildBattleCombatant Mon(string species,string name) {
            var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
            return new(p with {Moves=[new(Id(name),catalog.Moves[Id(name)].Pp,catalog.Moves[Id(name)].Pp)]});
        }
        string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
        WildBattleState State(WildBattleCombatant actor,WildBattleCombatant foe,uint seed)=>new(actor,foe,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
        foreach(var name in new[]{"LEECH_SEED","INGRAIN"})
        foreach(string branch in new[]{"normal","duplicate","grass","wounded"})
        foreach(uint seed in new uint[]{0,7,31}) {
            var actor=Mon("PIDGEY",name);var foe=Mon(branch=="grass"?"BULBASAUR":"RATTATA","DEFENSE_CURL");
            if(branch=="duplicate"){if(name=="INGRAIN")actor=actor with{Rooted=true};else foe=foe with{LeechSeedSource=WildBattleSide.Player};}
            if(branch=="wounded")actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=1}};
            var r=WildBattleRules.ResolveSourceMoveAction(catalog,State(actor,foe,seed),new(WildBattleSide.Player,0));
            if(!r.Succeeded)throw new Exception(name+": "+r.Fault);
            records.Add(new{kind="action",name,branch,seed,sha256=Hash(r)});
            if (verifyAdapter)
            {
                var source = State(actor,foe,seed) with { CalledField = new() };
                var expected = WildBattleRules.ResolveSourceMoveAction(catalog,source,new(WildBattleSide.Player,0));
                WildBattleCalledPartyMember M(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
                var imported = SinglesBattleAdapter.Import(catalog,source,[new(BattleSide.Player,[M(actor)]),new(BattleSide.Opponent,[M(foe)])]);
                if(!imported.Succeeded)throw new Exception("singles import "+imported.Fault);
                var f=imported.Frame!;var actual=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(name=="INGRAIN"?0:1)));
                if(!actual.Succeeded)throw new Exception("singles kernel "+actual.Fault);
                var exported=SinglesBattleAdapter.Export(actual.State);
                if(!expected.Succeeded||!exported.Succeeded||JsonSerializer.Serialize(exported.State)!=JsonSerializer.Serialize(expected.State)||
                    JsonSerializer.Serialize(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray())!=JsonSerializer.Serialize(expected.Battle.Events)||
                    JsonSerializer.Serialize(actual.Trace)!=JsonSerializer.Serialize(expected.Battle.Trace))throw new Exception(name+" "+branch+" "+seed+" exact singles adapter parity");
            }
        }
        foreach(var branch in new[]{"root-heal","root-full","root-minimum","seed-heal","seed-full","clear-body","clear-body-low-hp","seed-source-fainted"}) {
            var actor=Mon("PIDGEY","DEFENSE_CURL");var foe=Mon(branch.StartsWith("clear-body")?"TENTACOOL":"RATTATA","DEFENSE_CURL");
            if(branch.StartsWith("root"))actor=actor with{Rooted=true,Pokemon=actor.Pokemon with{CurrentHp=branch=="root-full"?actor.Pokemon.Stats.MaxHp:branch=="root-minimum"?1:actor.Pokemon.Stats.MaxHp-1}};
            else {foe=foe with{LeechSeedSource=WildBattleSide.Player};actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=branch=="seed-full"?actor.Pokemon.Stats.MaxHp:branch=="clear-body-low-hp"?1:actor.Pokemon.Stats.MaxHp/2}};if(branch=="clear-body-low-hp")foe=foe with{Pokemon=foe.Pokemon with{CurrentHp=1}};}
            // A fainted source is tested by the shared residual's storage seam in the four-slot suite;
            // the singles public phase rightly requires its previous replacement to finish first.
            if(branch=="seed-source-fainted")continue;
            var state=State(actor,foe,31);WildBattleCalledPartyMember M(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
            state=state with{CalledParty=new([M(actor),M(Mon("PIDGEY","DEFENSE_CURL"))],[M(foe),M(Mon("RATTATA","DEFENSE_CURL"))],0,0,WildBattleCalledBattleKind.Trainer)};
            var r=WildBattleRules.ResolveSourceEndTurnPhase(catalog,state);
            if(!r.Succeeded)throw new Exception(branch+": "+r.Battle.Fault);
            records.Add(new{kind="residual",branch,sha256=Hash(r)});
        }
        return JsonSerializer.Serialize(new{cases=records.Count,records});
    }
}
