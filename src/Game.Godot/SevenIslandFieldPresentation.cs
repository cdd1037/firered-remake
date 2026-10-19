/// <summary>Source field_weather/field_specials projection; no gameplay or script mutation.</summary>
internal static class SevenIslandFieldPresentation
{
    internal static int ShadeChannel(int rgb5)
    {
        return Game.Application.FieldShadePresentation.Channel(rgb5);
    }
    internal static (int X,int Y) ShakeOffset(int horizontal,int vertical,int shakes,int delay,int frame)
    {
        if((horizontal,vertical,shakes,delay) is not ((1,1,8,3) or (2,2,10,3) or (3,3,12,2)))
            throw new ArgumentException("Unadmitted Tanoby ShakeScreen parameters.");
        int iteration=Math.Max(0,frame)/delay;
        if(iteration==0||iteration>=shakes)return (0,0);
        int sign=(iteration&1)==0?1:-1;
        // Original task assigns 0x8005 to X and 0x8004 to Y despite the source comment.
        return (vertical*sign,horizontal*sign);
    }
}
