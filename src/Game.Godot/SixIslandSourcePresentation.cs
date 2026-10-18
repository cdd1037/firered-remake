using Game.Application;
using Game.Domain.World;

/// <summary>Original normal palette, weather and FallWarp task callbacks. No gameplay or map-load authority.</summary>
internal static class SixIslandSourcePresentation
{
    internal sealed class Landing
    {
        private readonly int _initialOffset;
        private readonly int[] _offsets;
        private int _phase, _offsetIndex, _panIndex;
        private bool _weatherInitialized, _weatherReady;
        internal FieldWarpPresentation.NormalPalette Palette { get; } = new(true);
        internal int Frames { get; private set; }
        internal int OffsetY { get; private set; }
        internal int CameraY { get; private set; }
        internal bool Visible { get; private set; }
        internal bool FixedPriority { get; private set; } = true;
        internal bool FallCue { get; private set; }
        internal bool LandingCue { get; private set; }
        internal bool Complete { get; private set; }
        internal int LandingFrames { get; private set; } = -1;
        internal Landing(int spriteBottom)
        {
            if (spriteBottom<=0 || spriteBottom>512) throw new ArgumentOutOfRangeException(nameof(spriteBottom));
            // spriteBottom is actual screen spriteY - centerToCornerY, including coordinate offset.
            _initialOffset=DottedHoleFallPresentation.StartingOffset(spriteBottom,0,0);
            _offsets=DottedHoleFallPresentation.FallingOffsets(_initialOffset).ToArray();
        }
        internal void Advance()
        {
            if (Complete) return;
            Frames++;
            if (LandingFrames>=0) LandingFrames++;
            // Fall task priority0 runs before Weather task priority80; palette updates follow tasks.
            if (_phase==0) _phase=1;
            switch (_phase)
            {
                case 1: if (_weatherReady) _phase=2; break;
                case 2: Visible=true; OffsetY=_initialOffset; FallCue=true; _phase=3; break;
                case 3:
                    OffsetY=_offsets[_offsetIndex++];
                    if (OffsetY>=-16) FixedPriority=false;
                    if (OffsetY==0) { LandingCue=true; LandingFrames=0; _phase=4; }
                    break;
                case 4:
                    CameraY=DottedHoleFallPresentation.CameraPans[_panIndex++];
                    if (_panIndex==DottedHoleFallPresentation.CameraPans.Length) _phase=5;
                    break;
                case 5: CameraY=0; Complete=true; break;
            }
            if (!_weatherInitialized) _weatherInitialized=true;
            else if (!Palette.Active) _weatherReady=true;
            Palette.Advance();
        }
    }

    internal static int SapphireShakeY(int callbacks)
    {
        if (callbacks<0) throw new ArgumentOutOfRangeException(nameof(callbacks));
        int iteration=Math.Min(12,callbacks/3);
        return iteration==0 || callbacks>36 ? 0 : (iteration%2==0?3:-3);
    }
}
