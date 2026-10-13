using Game.Domain.Pokemon;

/// <summary>Display keys only. Canonical species, personality and trainer IDs remain unchanged.</summary>
internal static class SevenIslandRenderKeys
{
    internal static int Species(int sourceSpecies, uint personality) =>
        UnownFormRules.ResolveRenderSpeciesId(sourceSpecies, personality);

    internal static (int Species, bool Shiny) Appearance(int sourceSpecies, uint personality, uint trainerId) =>
        (Species(sourceSpecies, personality), ((trainerId >> 16) ^ (trainerId & 0xffff) ^
            (personality >> 16) ^ (personality & 0xffff)) < 8);

    internal static (int Species, bool Shiny) Pokedex(int sourceSpecies, uint? unownPersonality) =>
        sourceSpecies == UnownFormRules.SourceSpeciesId
            ? (UnownFormRules.RenderSpeciesId(unownPersonality ?? throw new InvalidDataException("Unown dex personality missing.")),
                UnownFormRules.PokedexPortraitIsShiny(unownPersonality.Value))
            : (sourceSpecies, false);
}
