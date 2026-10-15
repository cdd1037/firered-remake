using System.Collections.Immutable;
namespace Game.Domain.Encounters;

/// <summary>Exact pinned Good Rod slots for 22 loaded ordinary Kanto maps. Safari and unloaded maps are excluded.</summary>
public static class GoodRodEncounterTables
{
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
    [
        new(new("map.celadon-city"), 10,
            [new(2, new("pret:SPECIES_MAGIKARP"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_MAGIKARP"), 5, 15, 20)]),
        new(new("map.cerulean-cave-1f"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.cerulean-cave-b1f"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.cerulean-city"), 10,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.cinnabar-island"), 10,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.fuchsia-city"), 10,
            [new(2, new("pret:SPECIES_GOLDEEN"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_POLIWAG"), 5, 15, 20)]),
        new(new("map.pallet-town"), 10,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-10"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-11"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-12"), 60,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-13"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-21-north"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-21-south"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-22"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.route-23"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.route-24"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-25"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.route-4"), 20,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.route-6"), 20,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
        new(new("map.ssanne-exterior"), 10,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.vermilion-city"), 10,
            [new(2, new("pret:SPECIES_HORSEA"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_KRABBY"), 5, 15, 20)]),
        new(new("map.viridian-city"), 10,
            [new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60), new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20), new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20)]),
    ];
}
