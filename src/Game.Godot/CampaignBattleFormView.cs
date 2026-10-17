using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    private SourceBattleFormCatalog? _battleForms;
    private readonly Dictionary<(int Species,int Form,string Face,bool Shiny,bool Transformed),BattleArt> _battleFormArt=[];
    private SourceBattleFormCatalog BattleForms=>_battleForms??=new(System.IO.Path.Combine(_root,"content/generated/battle/art/campaign-hit-art"));

    private WildBattleCombatant? SingleDisplayedBody(CampaignWildBattle battle,WildBattleSide side)
    {
        if(side==WildBattleSide.Player&&WildWithdrawing(battle)&&WildCurrentEvent(battle) is WildBattlePlayerSwitchedEvent switched)
            return switched.Previous;
        return battle.DisplayBatch?.Display.Bodies[(int)side];
    }

    private BattleArt SingleBattleArt(CampaignWildBattle battle,WildBattleSide side,PokemonInstance identity)
    {
        var appearance=FuchsiaBattleAppearance(battle,side,identity);string face=side==WildBattleSide.Player?"back":"front";
        if(!BattleForms.HasForms(appearance.Species))return _battleArt[(appearance.Species,face,appearance.Shiny)];
        var body=SingleDisplayedBody(battle,side)??throw new InvalidDataException("Source form display body is missing.");
        return FormBattleArt(body,new((int)side));
    }

    private BattleArt DoubleBattleArt(WildBattleCombatant body,int slot)
    {
        var appearance=body.Transform?.CopiedPokemon??body.Pokemon;int species=PresentedRenderSpecies(appearance);
        if(!BattleForms.HasForms(species))return _battleArt[(species,(slot&1)==0?"back":"front",appearance.IsShiny)];
        return FormBattleArt(body,new(slot));
    }

    private SourceBattlerFormMaterial FormBattleMaterial(WildBattleCombatant body,AnimationSlot slot)
    {
        var appearance=CampaignBattleFormAppearance.From(body);
        return BattleForms.Resolve(PresentedRenderSpecies(appearance.RenderPokemon),slot,(int)appearance.Form,
            appearance.Shiny,appearance.Transformed);
    }

    private BattleArt FormBattleArt(WildBattleCombatant body,AnimationSlot slot)
    {
        var appearance=CampaignBattleFormAppearance.From(body);
        int species=PresentedRenderSpecies(appearance.RenderPokemon),form=(int)appearance.Form;
        string face=slot.IsPlayer?"back":"front";var key=(species,form,face,appearance.Shiny,appearance.Transformed);
        if(_battleFormArt.TryGetValue(key,out var asset))return asset;
        var material=FormBattleMaterial(body,new(slot.Value&1));var pixels=material.Pixels.Pixels;var rgba=new byte[pixels.Length*4];
        for(int i=0;i<pixels.Length;i++)
        {
            int c=pixels[i];rgba[i*4]=(byte)((c&31)*255/31);rgba[i*4+1]=(byte)(((c>>5)&31)*255/31);
            rgba[i*4+2]=(byte)(((c>>10)&31)*255/31);rgba[i*4+3]=(byte)((c&0x8000)!=0?255:0);
        }
        using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,rgba);
        var center=material.Geometry.Center(false);asset=new(ImageTexture.CreateFromImage(image),new(center.X-32,center.Y-32));
        _battleFormArt.Add(key,asset);return asset;
    }

    private void DisposeBattleForms()
    {
        foreach(var art in _battleFormArt.Values)art.Texture.Dispose();
        _battleFormArt.Clear();_battleForms=null;
    }
}
