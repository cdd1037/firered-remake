using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Ids;
using Godot;

// This profile extends authenticated presentation assets only. Application commands
// remain the authority for acquisition, encounters, trainer results and progression.
public partial class CampaignView
{
    private readonly Dictionary<int,Texture2D> _route22Footprints=[];

    private void LoadRoute22PokemonPresentation()
    {
        if(_catalog.Route22Pokemon is not {} source)return;
        if(_battleArt.Count!=116)throw new InvalidDataException("Route22 must extend the existing twenty-nine species.");
        foreach(var sprite in source.Sprites)
        {
            using var image=new Image();
            if(image.LoadPngFromBuffer(source.ReadSpriteTexture(new(sprite.SpeciesId),sprite.Side,sprite.PaletteVariant=="shiny"))!=Error.Ok)
                throw new InvalidDataException("Invalid Route22 sprite PNG.");
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=64||image.GetHeight()!=64||Convert.ToHexStringLower(SHA256.HashData(image.GetData()))!=sprite.RgbaSha256)
                throw new InvalidDataException("Route22 sprite pixels differ.");
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),
                new(ImageTexture.CreateFromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        foreach(var foot in source.Footprints)
        {
            using var image=new Image();
            if(image.LoadPngFromBuffer(source.ReadFootprintTexture(foot.SourceSpeciesId))!=Error.Ok)
                throw new InvalidDataException("Invalid Route22 footprint PNG.");
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=16||image.GetHeight()!=16||Convert.ToHexStringLower(SHA256.HashData(image.GetData()))!=foot.RgbaSha256)
                throw new InvalidDataException("Route22 footprint pixels differ.");
            _route22Footprints.Add(foot.SourceSpeciesId,ImageTexture.CreateFromImage(image));
        }
        if(_battleArt.Count!=128||_route22Footprints.Count!=3)throw new InvalidDataException("Route22 presentation inventory differs.");
    }

    private CaptureDexEntry CaptureDexEntry(int species) =>
        _catalog.RocketHideoutPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.CeladonGymPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.CeladonApproachPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.RockTunnelPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.Route11Pokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.SsAnnePokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.VermilionPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.BillPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.CeruleanPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.MtMoonPokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.Route3Pokemon?.CaptureDexEntries.GetValueOrDefault(species)??_catalog.Route22Pokemon?.CaptureDexEntries.GetValueOrDefault(species)??_captureDex!.Entries[species];

    private PokedexMenuPage PresentedPokedexPage(int species)
    {
        var page=_pokedexMenu!.BuildPage(species,_session.State.SeenSpecies,_session.State.CaughtSpecies,PokedexAccess);
        var entry=_catalog.RocketHideoutPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.CeladonGymPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.CeladonApproachPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.RockTunnelPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.Route11Pokemon?.DexEntries.GetValueOrDefault(species)??_catalog.SsAnnePokemon?.DexEntries.GetValueOrDefault(species)??_catalog.VermilionPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.BillPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.CeruleanPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.MtMoonPokemon?.DexEntries.GetValueOrDefault(species)??_catalog.Route3Pokemon?.DexEntries.GetValueOrDefault(species)??_catalog.Route22Pokemon?.DexEntries.GetValueOrDefault(species);
        if(entry is not null)return page with{Portrait=entry.Portrait,Footprint=page.Caught?entry.Footprint:null};
        var preview=_catalog.BillItems?.DexViews.FirstOrDefault(v=>v.SourceSpeciesId==species);
        return preview is not null?page with{Portrait=BillPcDexPortrait(preview)}:page;
    }

    private static PokedexMenuTexture BillPcDexPortrait(PretBillDexView view)=>new("content/generated/bill-items/"+view.PngPath,view.PngSha256,view.RgbaSha256,64,64);

    private byte[] ReadPokedexTexture(PokedexMenuTexture texture)
    {
        if(_catalog.BillItems is {} items)
            foreach(var view in items.DexViews)
                if(texture==BillPcDexPortrait(view))
                {
                    using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,PretBillItems.DexRgba(view));
                    return image.SavePngToBuffer();
                }
        if(_catalog.RocketHideoutPokemon is {} rocket)
            foreach(var entry in rocket.DexEntries.Values)
            {
                if(texture==entry.Portrait)return rocket.ReadSpriteTexture(rocket.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return rocket.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.CeladonGymPokemon is {} gym)
            foreach(var entry in gym.DexEntries.Values)
            {
                if(texture==entry.Portrait)return gym.ReadSpriteTexture(gym.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return gym.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.CeladonApproachPokemon is {} approach)
            foreach(var entry in approach.DexEntries.Values)
            {
                if(texture==entry.Portrait)return approach.ReadSpriteTexture(approach.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return approach.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.RockTunnelPokemon is {} rockTunnel)
            foreach(var entry in rockTunnel.DexEntries.Values)
            {
                if(texture==entry.Portrait)return rockTunnel.ReadSpriteTexture(rockTunnel.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return rockTunnel.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.Route11Pokemon is {} route11)
            foreach(var entry in route11.DexEntries.Values)
            {
                if(texture==entry.Portrait)return route11.ReadSpriteTexture(route11.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return route11.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.SsAnnePokemon is {} ship)
            foreach(var entry in ship.DexEntries.Values)
            {
                if(texture==entry.Portrait)return ship.ReadSpriteTexture(ship.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return ship.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.VermilionPokemon is {} vermilion)
            foreach(var entry in vermilion.DexEntries.Values)
            {
                if(texture==entry.Portrait)return vermilion.ReadSpriteTexture(vermilion.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return vermilion.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.BillPokemon is {} bill)
            foreach(var entry in bill.DexEntries.Values)
            {
                if(texture==entry.Portrait)return bill.ReadSpriteTexture(bill.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return bill.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.CeruleanPokemon is {} city)
            foreach(var entry in city.DexEntries.Values)
            {
                if(texture==entry.Portrait)return city.ReadSpriteTexture(city.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return city.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.MtMoonPokemon is {} moon)
            foreach(var entry in moon.DexEntries.Values)
            {
                if(texture==entry.Portrait)return moon.ReadSpriteTexture(moon.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return moon.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.Route3Pokemon is {} route3)
            foreach(var entry in route3.DexEntries.Values)
            {
                if(texture==entry.Portrait)return route3.ReadSpriteTexture(route3.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return route3.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        if(_catalog.Route22Pokemon is {} source)
            foreach(var entry in source.DexEntries.Values)
            {
                if(texture==entry.Portrait)return source.ReadSpriteTexture(source.Catalog.Species.Values.Single(s=>s.SourceSpeciesId==entry.SourceSpeciesId).Id,"front");
                if(texture==entry.Footprint)return source.ReadFootprintTexture(entry.SourceSpeciesId);
            }
        return _pokedexMenu!.ReadTexture(texture);
    }
}
