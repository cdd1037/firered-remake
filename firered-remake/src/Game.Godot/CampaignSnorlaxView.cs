using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{

    private void LoadSnorlaxPresentation()
    {
        if(!_catalog.SnorlaxEnabled)return;
        var art=_catalog.SnorlaxArt??throw new InvalidDataException("Snorlax art catalog missing.");
        foreach(var sprite in art.Sprites)
        {
            using var image=Image.CreateFromData(sprite.Width,sprite.Height,false,Image.Format.Rgba8,PngRgba8.Decode(art.Files[sprite.TexturePath], sprite.Width, sprite.Height));
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(ImageTexture.CreateFromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        foreach(var icon in art.Icons)
        {
            var rgba=PngRgba8.Decode(art.Files[icon.TexturePath], icon.Width, icon.Height);
            // Existing source icon presentation expands5-bit components by bit repetition.
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
            using var image=Image.CreateFromData(32,32,false,Image.Format.Rgba8,rgba);
            _storageIcons.Add(new(icon.SpeciesId),ImageTexture.CreateFromImage(image));
        }
        foreach(var foot in art.Footprints)
        {
            using var image=Image.CreateFromData(foot.Width,foot.Height,false,Image.Format.Rgba8,PngRgba8.Decode(art.Files[foot.TexturePath], foot.Width, foot.Height));
            _route22Footprints.Add(foot.SourceSpeciesId,ImageTexture.CreateFromImage(image));
        }
    }

    private bool SnorlaxSilentEvent(CampaignWildBattle battle)=>WildCurrentEvent(battle) is {} current&&SnorlaxWildMessage(current) is {Symbol.Length:0};
    private ForestMessage? SnorlaxWildMessage(WildBattleEvent current)=>!_catalog.SnorlaxEnabled?null:current switch
    {
        WildBattleYawnEvent {Reason:"move",Before:0,After:2} yawn=>new("sText_PkmnWasMadeDrowsy",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(Opposite(yawn.Side))),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(yawn.Side))),
        WildBattleYawnEvent or WildBattleSleepTalkEvent=>new(""),
        WildBattleStatusChangedEvent {Reason:"yawn",Before:0} sleep=>new("sText_PkmnFellAsleep",("B_EFF_NAME_WITH_PREFIX",WildPrefixedName(sleep.Side))),
        WildBattleAbilityPreventedEvent {Ability:"ABILITY_IMMUNITY"} ability=>new("sText_PkmnPreventsPoisoningWith",("B_EFF_NAME_WITH_PREFIX",WildPrefixedName(ability.Target)),("B_DEF_ABILITY","IMMUNITY")),
        WildBattleStatusChangedEvent {Reason:"immunity",After:0} cure=>new("sText_PkmnsXCuredItsYProblem",("B_SCR_ACTIVE_NAME_WITH_PREFIX",WildPrefixedName(cure.Side)),("B_SCR_ACTIVE_ABILITY","IMMUNITY"),("B_BUFF1",_battleText["gText_Poison"])),
        WildBattleMoveFailedEvent {Reason:"no-pp"}=>new("sText_ButNoPPLeft"),
        WildBattleMoveFailedEvent {Reason:"user-awake" or "already-drowsy" or "no-callable-move"}=>new("sText_ButItFailed"),
        _=>null
    };
}
