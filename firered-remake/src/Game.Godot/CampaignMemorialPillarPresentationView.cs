using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Presentation.BattleAnimations;
using Godot;

/// <summary>Opponent-only birds share ordinary battlers, Transform geometry and seen-Dex rendering.</summary>
public partial class CampaignView
{
    private MemorialPillarPresentationData? _memorialPillarArt;

    private void LoadMemorialPillarPresentation()
    {
        if (_catalog.MemorialPillarOpponents is not { } opponents) return;
        if (!_session.IsCurrentGame || _ordinaryEvolutionArt is null ||
            !ReferenceEquals(MemorialPillarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !ReferenceEquals(OwnedBattlePokemonCatalog.SourceAncestor(OwnedBattlePokemonCatalog.For(_catalog.PokemonContent?.Catalog),
                OrdinaryEvolutionOwnedPokemonCatalog.For), opponents.Owned))
            throw new InvalidDataException("Memorial Pillar presentation requires authenticated current opponents and owned predecessor.");
        var art = PretMemorialPillarPresentation.Load(_root);
        int battleCount = _battleArt.Count, portraitCount = _portraits.Count, iconCount = _storageIcons.Count, footprintCount = _route22Footprints.Count;
        foreach (var entry in art.DexEntries.Values)
        {
            var id = new Game.Domain.Ids.SpeciesId("pret:SPECIES_" + entry.Name);
            if (!opponents.Catalog.Species.TryGetValue(id, out var species) || species.SourceSpeciesId != entry.SourceSpeciesId ||
                opponents.Owned.Catalog.Species.ContainsKey(id) || _pokedexMenu is null ||
                entry with { Portrait = null, Footprint = null } != _pokedexMenu.Entries[entry.SourceSpeciesId])
                throw new InvalidDataException("Memorial Pillar opponent species or seen-Dex binding differs.");
        }
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            using var image = Image.CreateFromData(64, 64, false, Image.Format.Rgba8, art.Files[row.GetProperty("rgbaPath").GetString()!]);
            var texture = SceneTextures.FromImage(image);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
        }
        if (_battleArt.Count != battleCount + 8 || _portraits.Count != portraitCount + 2 ||
            _storageIcons.Count != iconCount || _route22Footprints.Count != footprintCount || opponents.Owned.Catalog.Species.Count != 186)
            throw new InvalidDataException("Memorial Pillar opponent-only presentation inventory differs.");
        _memorialPillarArt = art;
        Write("memorialPillarPresentation", new { PretMemorialPillarPresentation.ManifestSha256, newBattleSprites = 8,
            seenDexPortraits = 2, newNormalCryCues = 2, newOwnedSpecies = 0, newIcons = 0, newFootprints = 0,
            newMoveAnimations = 0, nativeWindowVerified = false, physicalAudioVerified = false });
    }

    private void AddMemorialPillarMoveGeometry()
    {
        if (_memorialPillarArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            int species = row.GetProperty("species").GetInt32(); string side = row.GetProperty("side").GetString()!;
            var geometry = new AnimationBattler(new(side == "back" ? 0 : 1), "", row.GetProperty("yOffset").GetInt32(),
                row.GetProperty("elevation").GetInt32(), row.GetProperty("sourceWidth").GetInt32(), row.GetProperty("sourceHeight").GetInt32());
            if (_sourceBattlerGeometry.TryGetValue((species, side), out var old))
            {
                if (old.YOffset != geometry.YOffset || old.Elevation != geometry.Elevation || old.SourceWidth != geometry.SourceWidth || old.SourceHeight != geometry.SourceHeight)
                    throw new InvalidDataException("Memorial Pillar inherited geometry differs.");
            }
            else _sourceBattlerGeometry.Add((species, side), geometry);
        }
    }
    private PokedexMenuPage? MemorialPillarPokedexPage(int species, PokedexMenuPage page) =>
        _memorialPillarArt?.DexEntries.GetValueOrDefault(species) is { } entry ? page with { Portrait = entry.Portrait, Footprint = null } : null;
    private byte[]? ReadMemorialPillarDexTexture(PokedexMenuTexture texture) => _memorialPillarArt is { } art &&
        art.DexEntries.Values.Any(e => e.Portrait == texture) ? art.Files[texture.TexturePath] : null;
    private string? MemorialPillarSpeciesName(int species) => _memorialPillarArt?.DexEntries.GetValueOrDefault(species)?.Name;
    private void DisposeMemorialPillarPresentation() => _memorialPillarArt = null;
}
