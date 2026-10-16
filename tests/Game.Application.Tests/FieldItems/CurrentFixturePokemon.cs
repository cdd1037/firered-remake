using Game.Application;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.World;

namespace FireRed.Tests.Application.FieldItems;
internal static class CurrentFixturePokemon
{
    internal static CampaignState BringToLead(ICampaignCatalog catalog, CampaignState state, int species)
    {
        int index = Enumerable.Range(0, state.Party.Length).FirstOrDefault(i => state.Party[i].Pokemon.SourceSpeciesId == species, -1);
        if (index < 0)
        {
            var address = state.Storage!.Boxes.SelectMany((box, b) => box.Slots.Select((mon, s) => new { mon, b, s })).First(x => x.mon?.Pokemon.SourceSpeciesId == species);
            var deposited = PokemonStorageRules.Deposit(catalog.PokemonContent, state.Party, state.Storage, 0, 13);
            if (!deposited.Succeeded)
                throw new InvalidDataException("Fixture deposit failed: " + deposited.Diagnostic);
            var withdrawn = PokemonStorageRules.Withdraw(catalog.PokemonContent, deposited.Party, deposited.Storage, address.b, address.s);
            if (!withdrawn.Succeeded)
                throw new InvalidDataException("Fixture withdrawal failed: " + withdrawn.Diagnostic);
            state = state with
            {
                Party = withdrawn.Party,
                Storage = withdrawn.Storage
            };
            index = state.Party.Length - 1;
        }

        return index == 0 ? state : state with
        {
            Party = state.Party.SetItem(0, state.Party[index]).SetItem(index, state.Party[0])
        };
    }

    // Input preparation preserves original permanent identities and source IVs.
    internal static PartyPokemon AtLevel(CampaignPokemonContent content, PartyPokemon member, int level, uint? experience = null)
    {
        var p = member.Pokemon;
        var basis = content.Catalog.Species[p.Species].BaseStats;
        int Other(int b, int iv, int ev, Stat stat) => StatRules.CalculateOther(b, iv, ev, level, p.Nature, stat);
        var stats = new BattleStats(StatRules.CalculateHp(basis.Hp, p.Ivs.Hp, p.Evs.Hp, level), Other(basis.Attack, p.Ivs.Attack, p.Evs.Attack, Stat.Attack), Other(basis.Defense, p.Ivs.Defense, p.Evs.Defense, Stat.Defense), Other(basis.SpecialAttack, p.Ivs.SpecialAttack, p.Evs.SpecialAttack, Stat.SpecialAttack), Other(basis.SpecialDefense, p.Ivs.SpecialDefense, p.Evs.SpecialDefense, Stat.SpecialDefense), Other(basis.Speed, p.Ivs.Speed, p.Evs.Speed, Stat.Speed));
        return member with
        {
            Pokemon = p with
            {
                Level = level,
                Experience = experience ?? GrowthRules.ExperienceAtLevel(content.Catalog.Species[p.Species].GrowthRate, level),
                Stats = stats,
                CurrentHp = stats.MaxHp,
                StatCalculationEvs = null
            }
        };
    }
}
