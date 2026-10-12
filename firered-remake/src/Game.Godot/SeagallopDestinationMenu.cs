/// <summary>Bounded source multichoice rows and geometry from script_menu.c and seagallop.inc.
/// No commands, world changes or story gates are owned here.</summary>
internal sealed class SeagallopDestinationMenu
{
    internal readonly record struct Layout(int X,int Y,int Width,int Height);
    internal const int Cancel=127;
    internal string SourceMenu {get;}
    internal IReadOnlyList<string> Options {get;}
    internal int Cursor {get;private set;}
    internal SeagallopDestinationMenu(string sourceMenu,IEnumerable<string> options)
    {
        string[] expected=sourceMenu switch
        {
            "MULTICHOICE_ISLAND_23"=>["TWO ISLAND","THREE ISLAND","EXIT"],
            "MULTICHOICE_ISLAND_13"=>["ONE ISLAND","THREE ISLAND","EXIT"],
            "MULTICHOICE_ISLAND_12"=>["ONE ISLAND","TWO ISLAND","EXIT"],
            "MULTICHOICE_SEAGALLOP_V23"=>["VERMILION","TWO ISLAND","THREE ISLAND","EXIT"],
            "MULTICHOICE_SEAGALLOP_V13"=>["VERMILION","ONE ISLAND","THREE ISLAND","EXIT"],
            "MULTICHOICE_SEAGALLOP_V12"=>["VERMILION","ONE ISLAND","TWO ISLAND","EXIT"],
            "MULTICHOICE_SEAGALLOP_123"=>["ONE ISLAND","TWO ISLAND","THREE ISLAND","EXIT"],
            _=>throw new InvalidDataException("Unsupported first-Sevii source menu.")
        };
        if(!options.SequenceEqual(expected))throw new InvalidDataException("First-Sevii source menu options differ.");
        SourceMenu=sourceMenu;Options=Array.AsReadOnly(expected);
    }
    internal Layout Frame(int maximumTextWidth)
    {
        if(maximumTextWidth is <=0 or >200)throw new ArgumentOutOfRangeException(nameof(maximumTextWidth));
        int width=(maximumTextWidth+9)/8+1,left=Math.Min(19,28-width),top=Options.Count==3?6:5;
        return new(left*8,top*8,(width+2)*8,(Options.Count==3?6:7)*8+16);
    }
    internal bool Move(int direction)
    {
        if(direction is not (-1 or 1))throw new ArgumentOutOfRangeException(nameof(direction));
        int next=Options.Count>3?(Cursor+direction+Options.Count)%Options.Count:Math.Clamp(Cursor+direction,0,Options.Count-1);
        if(next==Cursor)return false;Cursor=next;return true;
    }
    internal int RowAt(double x,double y,int textWidth)
    {
        var frame=Frame(textWidth);double left=frame.X+8,top=frame.Y+8;
        return x>=left&&x<frame.X+frame.Width-8&&y>=top&&y<top+14*Options.Count?(int)((y-top)/14):-1;
    }
    internal bool Select(int index)
    {if(index<0||index>=Options.Count)return false;if(index==Cursor)return false;Cursor=index;return true;}
}
