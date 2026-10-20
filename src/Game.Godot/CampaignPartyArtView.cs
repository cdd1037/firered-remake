using Game.Domain.Pokemon;
using Game.Domain.Capture;
using Godot;

// Shared single-layout party skin. Slot indices are never sorted or reconstructed;
// service/battle callers retain eligibility, owned identity, and event-time values.
public partial class CampaignView
{
    private Texture2D? _partyAtlas;
    private int _partyLastRightSlot = 1;
    private static Rect2 NativePartySlot(int slot, NativePartyLayout layout=NativePartyLayout.Single) { var b=PartyPresentationAssets.Slot(slot,layout);return new(b.X,b.Y,b.Width,b.Height); }
    private static Rect2 NativePartyCancel { get { var b=PartyPresentationAssets.Cancel;return new(b.X,b.Y,b.Width,b.Height); } }
    private void PartyAtlas(Rect2 destination, Rect2 source)
    {
        _partyAtlas ??= SceneTextures.Texture(System.IO.Path.Combine(_root,"content/generated/ui/party-screen/atlas.png"));
        DrawTextureRectRegion(_partyAtlas,destination,source);
    }
    private bool NativePartyMove(Key key, ref int cursor, int count, bool cancel = true, NativePartyLayout layout=NativePartyLayout.Single)
    {
        if (key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return false;
        cursor=PartyPresentationAssets.Move(cursor,count,cancel,key==Key.Left?-1:key==Key.Right?1:0,key==Key.Up?-1:key==Key.Down?1:0,ref _partyLastRightSlot,layout);
        return true;
    }
    private int NativePartyHit(Vector2 point, int count, bool cancel=true, NativePartyLayout layout=NativePartyLayout.Single)
    {
        for(int i=0;i<count;i++)if(NativePartySlot(i,layout).HasPoint(point))return i;
        return cancel&&NativePartyCancel.HasPoint(point)?count:-1;
    }
    private bool MouseNativeParty(Vector2 point, ref int cursor, int count, bool cancel=true, NativePartyLayout layout=NativePartyLayout.Single)
    {
        int slot=NativePartyHit(point,count,cancel,layout);if(slot<0)return false;
        cursor=slot;return MouseKey();
    }
    private bool PointerNativeParty(Vector2 point,int direction,ref int cursor,int count,bool cancel=true, NativePartyLayout layout=NativePartyLayout.Single)
    {
        int slot=NativePartyHit(point,count,cancel,layout);if(slot<0)return false;
        if(direction!=0)return MouseNavigationKey(direction);
        if(cursor==slot)return false;cursor=slot;return true;
    }
    private void DrawNativeParty(IReadOnlyList<PartyPokemon> party,int cursor,string prompt,int swapSource=-1,bool cancel=true,string? cancelText=null,Func<int, PartyPokemon>? displayed=null, Func<int,string?>? description=null, NativePartyLayout layout=NativePartyLayout.Single)
    {
        PartyAtlas(new(0,0,240,160),new(0,0,240,160));
        for(int i=0;i<6;i++)
        {
            var rect=NativePartySlot(i,layout);bool main=rect.Size.X==80;
            if(i>=party.Count){if(!main)PartyAtlas(rect,new(80,832,144,24));continue;}
            var owned=displayed?.Invoke(i)??party[i];var mon=owned.Pokemon;string? label=description?.Invoke(i);
            bool selected=i==cursor;int state=i==swapSource?4:!owned.IsEgg&&mon.CurrentHp==0?2:0;
            if(selected)state++;
            PartyAtlas(rect,new(main?0:80,160+((owned.IsEgg||label is not null?6:0)+state)*56,rect.Size.X,rect.Size.Y));
            Vector2 origin=rect.Position;
            Vector2 icon=origin+new Vector2(-8,main?0:-6);
            PartyAtlas(new(origin+(main?new Vector2(-8,-6):new Vector2(-10,1)),new(32,32)),new(144,selected?896:864,32,32));
            DrawTextureRectRegion(PresentedOwnedIcon(owned),new(icon,new(32,32)),new(0,0,32,32));
            if(owned.HeldItem!="ITEM_NONE" && !string.IsNullOrEmpty(owned.HeldItem))
                PartyAtlas(new(icon+new Vector2(16,22),new(8,8)),new(112,SourceMailRules.IsMailItem(owned.HeldItem)?872:864,8,8));
            SourceScrollingText(_sourceSmallWhite,origin+(main?new Vector2(24,11):new Vector2(22,3)),
                PresentedOwnedName(owned),main?52:label is not null?53:owned.IsEgg?114:64);
            if(label is not null)SourceText(_sourceSmallWhite,origin+(main?new Vector2(12,34):new Vector2(77,4)),label);
            if(owned.IsEgg)continue;
            Vector2 level=origin+(main?new Vector2(32,20):new Vector2(32,12));
            if(mon.CurrentHp!=0&&owned.Status==0)SourceText(_sourceSmallWhite,level,"Lv"+mon.Level);
            else DrawForestPartyStatus(owned,level);
            if(mon.Gender!=PokemonGender.Genderless)SourceText(mon.Gender==PokemonGender.Female?_healthFemale:_healthMale,origin+(main?new Vector2(64,20):new Vector2(64,12)),mon.Gender==PokemonGender.Female?"♀":"♂");
            if(label is not null)continue;
            Vector2 hp=origin+(main?new Vector2(24,35):new Vector2(88,10));
            int width=mon.CurrentHp==0?0:Math.Max(1,48*mon.CurrentHp/mon.Stats.MaxHp);
            PartyAtlas(new(hp,new(48,3)),new(0,908,48,3));
            if(width>0)PartyAtlas(new(hp,new(width,3)),new(0,mon.CurrentHp*2>mon.Stats.MaxHp?896:mon.CurrentHp*5>mon.Stats.MaxHp?900:904,width,3));
            string numbers=$"{mon.CurrentHp,3}/{mon.Stats.MaxHp,3}";
            SourceText(_sourceSmallWhite,origin+(main?new Vector2(30,36):new Vector2(94,12)),numbers);
        }
        // Source message window is 21 tiles wide; CANCEL occupies the right seven.
        _battleSkin.DrawUserFrame(this,new(0,128,184,32));
        SourceText(_sourceSmall,new(8,137),prompt.Replace("\n"," "));
        if(cancel){PartyAtlas(NativePartyCancel,new(cursor==party.Count?56:0,864,56,16));SourceText(_sourceSmallWhite,new(195,137),cancelText??_summaryPartyText.Label(SummaryPartyAction.Cancel));}
    }
    private void DrawNativePartyMessage(string text)
    {
        _battleSkin.DrawUserFrame(this,new(0,112,240,48));
        ItemLines(_sourceNormal,new(8,120),text,16);
    }
}
