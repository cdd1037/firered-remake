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
    public static string Run(string root)
    {
        var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(root); var catalog = proof.Catalog;
        var records = new List<object>();
        MoveId Id(string name) => new("pret:MOVE_" + name);
        WildBattleCombatant Mon(string species, string move) {
            var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
            return new(p with { Moves = [new(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)] });
        }
        foreach (uint seed in new uint[] { 0, 31 })
        foreach (string branch in new[] { "create", "duplicate", "insufficient", "binding", "hit", "break", "secondary", "multi", "status", "stat", "tickle", "self-stat", "shell-bell", "kings-rock", "focus-band", "contact" }) {
            string move = branch switch { "create" or "duplicate" or "insufficient" or "binding" => "SUBSTITUTE", "secondary" => "BODY_SLAM", "multi" => "FURY_SWIPES", "status" => "TOXIC", "stat" => "GROWL", "tickle" => "TICKLE", "self-stat" => "METAL_CLAW", _ => "TACKLE" };
            var actor=Mon("PIDGEY",move);var target=Mon(branch=="contact"?"CARVANHA":"SNORLAX","DEFENSE_CURL");
            if (branch == "duplicate") actor=actor with { CalledVolatile=new(){SubstituteHp=15} };
            else if(branch=="insufficient")actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=actor.Pokemon.Stats.MaxHp/4}};
            else if(branch=="binding")actor=actor with{BindTurnsRemaining=3,BindingMove=Id("WRAP")};
            else if(branch!="create")target=target with{CalledVolatile=new(){SubstituteHp=branch is "break" or "secondary" or "multi"?1:40}};
            if(branch is "shell-bell" or "kings-rock")actor=actor with{HeldItem=branch=="shell-bell"?"ITEM_SHELL_BELL":"ITEM_KINGS_ROCK",Pokemon=actor.Pokemon with{CurrentHp=actor.Pokemon.Stats.MaxHp/2}};
            if(branch=="focus-band")target=target with{HeldItem="ITEM_FOCUS_BAND"};
            var state=new WildBattleState(actor,target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
            var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));
            if(!result.Succeeded)throw new Exception(branch+": "+result.Fault);
            records.Add(new{branch,seed,sha256=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))))});
        }
        return JsonSerializer.Serialize(new{cases=records.Count,records});
    }
}
