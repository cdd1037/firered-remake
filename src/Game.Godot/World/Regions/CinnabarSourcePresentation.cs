/// <summary>Discrete FieldCB_FallWarpExit callbacks from the pinned field_effect.c,1155–1289.
/// This is presentation only; Application owns destination and the correlated release.</summary>
internal static class CinnabarSourcePresentation
{
    internal readonly record struct FallPose(int OffsetY,int CameraY,bool Visible,bool FixedPriority,bool FallCue,bool LandingCue,bool Complete,int LandingFrames,float Fade);
    // Mansion WEATHER_SHADE FadeInScreen_RainShowShade consumes16 updates.
    internal const int FadeFrames=16;
    // ResumeMap resets tasks. WeatherInit(priority80) consumes update1; the16
    // shade updates run after FallWarp(priority0). Effect2 observes completion on18
    // and returnsFALSE, so Effect3 first runs on19. (overworld.c/task.c)
    internal const int FirstVisibleFrame=FadeFrames+3;
    internal static FallPose Landing(int frames,int spriteBottom)
    {
        if(frames<0||spriteBottom<=0)throw new ArgumentOutOfRangeException(nameof(frames));
        if(frames<FirstVisibleFrame)return new(0,0,false,true,false,false,false,-1,1-Math.Clamp(frames-1,0,FadeFrames)/(float)FadeFrames);
        int y=-spriteBottom,speed=1,accumulator=0;bool fixedPriority=true;int landingFrame=-1;
        // Effect3 exposes the sprite immediately above the viewport and plays SE_FALL.
        for(int frame=FirstVisibleFrame+1;frame<=frames;frame++)
        {
            y+=speed;
            if(speed<8){accumulator+=speed;if((accumulator&15)!=0)speed<<=1;}
            if(y>=-16)fixedPriority=false;
            if(y>=0){y=0;landingFrame=frame;break;}
        }
        if(landingFrame<0)return new(y,0,true,fixedPriority,true,false,false,-1,0);
        int amplitude=4,camera=0,shakeFrames=0;
        for(int frame=landingFrame+1;frame<=frames;frame++)
        {
            if(amplitude==0)return new(0,0,true,false,true,true,true,frames-landingFrame,0);
            camera=amplitude;amplitude=-amplitude;shakeFrames++;
            if((shakeFrames&3)==0)amplitude>>=1;
        }
        return new(0,camera,true,false,true,true,false,frames-landingFrame,0);
    }
}
