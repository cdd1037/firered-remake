using Game.Content;
using Game.Domain.Ids;
using Godot;

// This profile extends authenticated presentation assets only. Application commands
// remain the authority for acquisition, encounters, trainer results and progression.
public partial class CampaignView
{
    private readonly Dictionary<int,Texture2D> _route22Footprints=[];
    // These complete dex packs share one priority block; special form/portrait-only bindings stay separate.
    private readonly PresentationDexIndex _supplementalDex = new();

    private void LoadRoute22PokemonPresentation()
    {
        if(_catalog.Route22Pokemon is not {} source)return;
        RegisterPokemonSprites(source);

    }

    private CaptureDexEntry CaptureDexEntry(int species) =>
        _pokemonCollectionArt?.CaptureEntries.GetValueOrDefault(species)??GiftEggCaptureDexEntry(species)??_supplementalDex.CaptureEntry(species)??SevenIslandCaptureDexEntry(species)??MewtwoCaptureDexEntry(species)??CelioCaptureDexEntry(species)??FiveIslandCaptureDexEntry(species)??SixIslandCaptureDexEntry(species)??FourIslandCaptureDexEntry(species)??SeviiRubyCaptureDexEntry(species)??_catalog.CinnabarArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SaffronGymArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SilphArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SurfArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SafariArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.FuchsiaRouteArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.EeveeArt?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SnorlaxArt?.CaptureDexEntries.GetValueOrDefault(species)??TowerCaptureDexEntry(species)??_earlyDex.CaptureEntry(species)??_captureDex!.Entries[species];

    private PokedexMenuPage PresentedPokedexPage(int species)
    {
        var page=_pokedexMenu!.BuildPage(species,_session.State.SeenSpecies,_session.State.CaughtSpecies,PokedexAccess);
        if(_pokemonCollectionArt is {} collection)page=collection.Present(page);
        if(GiftEggPokedexPage(species,page) is {} giftPage)return giftPage;
        if(MemorialPillarPokedexPage(species,page) is {} memorialPage)return memorialPage;
        if(_supplementalDex.Present(species,page) is {} supplementalPage)return supplementalPage;
        if(SevenIslandPokedexPage(species,page) is {} sevenPage)return sevenPage;
        if(_celioDex.TryGetValue(species,out var celioEntry))return page with{Portrait=celioEntry.Portrait,Footprint=page.Caught?celioEntry.Footprint:null};
        if(_sixIslandDex.TryGetValue(species,out var sixEntry))return page with{Portrait=sixEntry.Portrait,Footprint=page.Caught?sixEntry.Footprint:null};
        if(FiveIslandDexPortrait(species) is {} fivePortrait)return page with{Portrait=fivePortrait};
        if(SixIslandDexPortrait(species) is {} sixPortrait)return page with{Portrait=sixPortrait};
        if(_fourIslandDex.TryGetValue(species,out var fourEntry))return page with{Portrait=fourEntry.Portrait,Footprint=page.Caught?fourEntry.Footprint:null};
        if(_seviiRubyDex.TryGetValue(species,out var rubyEntry))return page with{Portrait=rubyEntry.Portrait,Footprint=page.Caught?rubyEntry.Footprint:null};
        if(PokemonLeagueDexPortrait(species) is {} leaguePortrait)return page with{Portrait=leaguePortrait};
        if(VictoryRoadDexPortrait(species) is {} victoryPortrait)return page with{Portrait=victoryPortrait};
        var entry=_catalog.CinnabarArt?.DexEntries.GetValueOrDefault(species)??_catalog.SaffronGymArt?.DexEntries.GetValueOrDefault(species)??_catalog.SilphArt?.DexEntries.GetValueOrDefault(species)??_catalog.SurfArt?.DexEntries.GetValueOrDefault(species)??_catalog.SafariArt?.DexEntries.GetValueOrDefault(species)??_catalog.FuchsiaRouteArt?.DexEntries.GetValueOrDefault(species)??_catalog.EeveeArt?.DexEntries.GetValueOrDefault(species)??_catalog.SnorlaxArt?.DexEntries.GetValueOrDefault(species)??_towerDexEntries.GetValueOrDefault(species)??_earlyDex.Entry(species);
        if(entry is not null)return page with{Portrait=entry.Portrait,Footprint=page.Caught?entry.Footprint:null};
        var preview=_catalog.BillItems?.DexViews.FirstOrDefault(v=>v.SourceSpeciesId==species);
        return preview is not null?page with{Portrait=BillPcDexPortrait(preview)}:page;
    }

    private static PokedexMenuTexture BillPcDexPortrait(PretBillDexView view)=>new("content/generated/data/items/bill/"+view.PngPath,64,64);

    private byte[] ReadPokedexTexture(PokedexMenuTexture texture)
    {
        if(_pokemonCollectionArt?.Textures.TryGetValue(texture.TexturePath,out var collectionPixels)==true)return collectionPixels;
        if(ReadGiftEggDexTexture(texture) is {} giftPixels)return giftPixels;
        if(ReadMemorialPillarDexTexture(texture) is {} memorialPixels)return memorialPixels;
        if(_supplementalDex.ReadTexture(texture) is {} supplementalPixels)return supplementalPixels;
        if(ReadSevenIslandDexTexture(texture) is {} sevenPixels)return sevenPixels;
        if(ReadMewtwoDexTexture(texture) is {} mewtwoPixels)return mewtwoPixels;
        if(ReadCelioDexTexture(texture) is {} celioPixels)return celioPixels;
        if(ReadFiveIslandDexTexture(texture) is {} fivePixels)return fivePixels;
        if(ReadSixIslandDexTexture(texture) is {} sixPixels)return sixPixels;
        if(ReadFourIslandDexTexture(texture) is {} fourPixels)return fourPixels;
        if(ReadSeviiRubyDexTexture(texture) is {} rubyPixels)return rubyPixels;
        if(ReadPokemonLeagueDexTexture(texture) is {} leaguePixels)return leaguePixels;
        if(ReadVictoryRoadDexTexture(texture) is {} victoryPixels)return victoryPixels;
        if(_catalog.CinnabarArt is {} cinnabar&&cinnabar.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return cinnabar.ReadTexture(texture.TexturePath);
        if(_catalog.SaffronGymArt is {} saffron&&saffron.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return saffron.ReadTexture(texture.TexturePath);
        if(_catalog.SilphArt is {} silph&&silph.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return silph.ReadTexture(texture.TexturePath);
        if(_catalog.SurfArt is {} surf&&surf.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return surf.ReadTexture(texture.TexturePath);
        if(_catalog.SafariArt is {} safari&&safari.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return safari.ReadTexture(texture.TexturePath);
        if(_catalog.FuchsiaRouteArt is {} fuchsia&&fuchsia.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return fuchsia.ReadTexture(texture.TexturePath);
        if(_catalog.EeveeArt is {} eevee&&eevee.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return eevee.ReadTexture(texture.TexturePath);
        if(_catalog.SnorlaxArt is {} snorlax&&snorlax.DexEntries.Values.Any(e=>texture==e.Portrait||texture==e.Footprint))return snorlax.ReadTexture(texture.TexturePath);
        if(_towerDexTextures.TryGetValue(texture.TexturePath,out var towerPixels))return towerPixels;
        if(_catalog.BillItems is {} items)
            foreach(var view in items.DexViews)
                if(texture==BillPcDexPortrait(view))
                {
                    return PretBillItems.DexPng(view, GeneratedResourceFile.Read(_root, "content/generated/data/items/bill/" + view.PngPath));
                }
        if (_earlyDex.ReadTexture(texture) is { } earlyPixels) return earlyPixels;
        return _pokedexMenu!.ReadTexture(texture);
    }
}
