using Game.Content;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<string, ImageTexture> _introMovieTextures = [];
    private IntroMoviePresentation? _introMovie;
    private string IntroMovieSourceMusic => _introMovie is null ? "MUS_NONE" :
        _introMovie.Stage >= IntroMovieStage.Star && _introMovie.Stage <= IntroMovieStage.RevealLogo ? "MUS_GAME_FREAK" :
        (_introMovie.Stage == IntroMovieStage.Grass && _introMovie.Frame >= 12) ||
        (_introMovie.Stage > IntroMovieStage.Grass && _introMovie.Stage <= IntroMovieStage.Fade) ? "MUS_INTRO_FIGHT" : "MUS_NONE";
    private void InitializeIntroMoviePresentation()
    {
        var package = PretIntroMoviePresentation.Load(_root);
        foreach (var definition in package.Textures)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(package.ReadTexture(definition.Name)) != Error.Ok || image.GetWidth() != definition.Width || image.GetHeight() != definition.Height)
                throw new InvalidDataException("Invalid intro movie texture: " + definition.Name);
            _introMovieTextures.Add(definition.Name, ImageTexture.CreateFromImage(image));
        }
    }
    private void BeginIntroMovie() { _introMovie = new(); _entryMode = "intro-movie"; SourceMusic("MUS_NONE"); QueueRedraw(); }
    private void ProcessIntroMoviePresentation(double delta)
    {
        if (_entryMode != "intro-movie") return;
        var before = _introMovie!.Stage;
        string priorMusic = IntroMovieSourceMusic;
        _introMovie.Advance(delta);
        if (_introMovie.Stage == IntroMovieStage.Star && before != IntroMovieStage.Star) SourceSound("MUS_GAME_FREAK");
        else if (priorMusic != IntroMovieSourceMusic) SourceMusic(IntroMovieSourceMusic);
        if (_introMovie.ConsumeNidorinoCry() && !_introMovie.Complete && _sourceAudio is SynthSourceAudioPlayer synthesized)
        {
            const string cue = "CRY_NIDORINO_DOUBLES";
            if (EnsureAudio(cue)) synthesized.PlayDoublesCry(cue, pan: 0x3f);
        }
        if (_introMovie.Complete) BeginAnimatedTitle();
        QueueRedraw();
    }
    private bool TryIntroMovieKey(Key key)
    {
        if (_entryMode != "intro-movie") return false;
        if (_introMovie!.Skip(key is Key.Enter or Key.KpEnter, key == Key.Tab, key is Key.Space or Key.R)) BeginAnimatedTitle();
        return true;
    }
    private void MovieSprite(string name, float x, float y, int frame = 0, int height = 0, float scale = 1, float alpha = 1)
    {
        var texture = _introMovieTextures[name];
        if (height == 0) height = texture.GetHeight();
        DrawTextureRectRegion(texture, new(x, y, texture.GetWidth() * scale, height * scale), new(0, frame * height, texture.GetWidth(), height), new Color(1, 1, 1, Math.Clamp(alpha, 0, 1)));
    }
    // GBA scrolling wraps at the source layer extent. The 32-pixel theatrical mask is applied after composition.
    private void MovieLayer(string name, float scrollX = 0, float scrollY = 0, float alpha = 1)
    {
        var texture = _introMovieTextures[name]; int width = texture.GetWidth(), height = texture.GetHeight();
        float x = -((scrollX % width + width) % width), y = -((scrollY % height + height) % height);
        for (float py = y; py < 160; py += height)
            for (float px = x; px < 240; px += width)
                DrawTexture(texture, new(px, py), new Color(1, 1, 1, Math.Clamp(alpha, 0, 1)));
    }
    private bool DrawIntroMoviePresentation()
    {
        if (_entryMode != "intro-movie") return false;
        var movie = _introMovie!; var stage = movie.Stage; int f = movie.Frame;
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        if (stage == IntroMovieStage.Copyright)
        {
            MovieLayer("copyright");
            if (f < 32) DrawRect(new(0, 0, 240, 160), new Color(1, 1, 1, 1 - f / 32f));
            if (f > 140) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (f - 140) / 32f));
            return true;
        }
        if (stage is >= IntroMovieStage.OpenWindow and <= IntroMovieStage.RevealLogo)
        {
            MovieLayer("gf-bg");
            if (stage == IntroMovieStage.Star)
            {
                if (f < 44) MovieSprite("star", 240 - 6 * f, 47 + f + (float)Math.Cos(f * 3 * Math.PI / 128) * 8);
                for (int i = Math.Max(0, f - 90); i < Math.Min(f, 44); i++)
                {
                    uint r = IntroMoviePresentation.SparkleRandom(i);
                    if ((f + i) % 4 != 0) DrawTextureRectRegion(_introMovieTextures["sparkles-small"], new(240 - i * 6 + (r & 7), 47 + i + (f - i) / 4f, 8, 8), new((f / 4 % 2) * 8, (f / 8 % 2) * 8, 8, 8), new Color(1, 1, 1, 1 - (f - i) / 120f));
                }
            }
            if (stage is IntroMovieStage.RevealName or IntroMovieStage.RevealLogo)
            {
                float alpha = stage == IntroMovieStage.RevealName ? (f - 40) / 48f : f > 110 ? 1 - (f - 110) / 20f : 1;
                MovieSprite("gf-name", 48, 72, alpha: alpha);
                if (stage == IntroMovieStage.RevealName && f < 60)
                    for (int i = 0; i < 7; i++) MovieSprite("sparkles-big", 40 + i * 23, 64, (f / 4 + i) % 4, 32, 1, 1 - f / 60f);
                if (stage == IntroMovieStage.RevealLogo)
                {
                    MovieSprite("gf-logo", 104, 38, alpha: Math.Min(f / 16f, alpha));
                    if (f > 16) MovieSprite("gf-presents", 88, 104, alpha: alpha);
                }
            }
            int half = stage == IntroMovieStage.OpenWindow ? Math.Min(48, f * 8) : 48;
            DrawRect(new(0, 0, 240, 80 - half), Colors.Black); DrawRect(new(0, 80 + half, 240, 80 - half), Colors.Black); return true;
        }
        if (stage == IntroMovieStage.Grass)
        {
            MovieLayer("grass-bg", 0, f > 32 ? Math.Min(2, (f - 32) / 4) * 128 : 0);
            MovieLayer("grass-front", 0, (f / 6 % 3) * 128 - Math.Max(0, f - 32) * 1.125f);
            if (f < 12) DrawRect(new(0, 0, 240, 160), new Color(1, 1, 1, 1 - f / 12f));
        }
        else if (stage == IntroMovieStage.WideShot)
        {
            MovieLayer("wide-bg", -f * .875f); MovieSprite("wide-gengar", 40, 48); MovieSprite("wide-nidorino", 136, 48); MovieLayer("wide-plants", f * 1.0625f);
            if (f < 12) DrawRect(new(0, 0, 240, 160), new Color(1, 1, 1, 1 - f / 12f));
        }
        else if (stage == IntroMovieStage.CloseUp)
        {
            MovieLayer("wide-bg", 0, 256); MovieLayer("close-gengar", 0, 462 + f * .125f); MovieLayer("close-nidorino", 0, 40 - f * .140625f);
        }
        else if (stage is >= IntroMovieStage.Entrance and <= IntroMovieStage.Fade) DrawMovieFight(stage, f);
        DrawRect(new(0, 0, 240, 32), Colors.Black); DrawRect(new(0, 128, 240, 32), Colors.Black);
        return true;
    }
    private void DrawMovieFight(IntroMovieStage stage, int f)
    {
        MovieLayer("fight-bg", _introMovie!.FightBackgroundPixelX);
        float nx = 180, ny = 100, gx = 239, gy = 496; int pose = 0, gengarFrame = _introMovie.GengarFrame;
        if (stage == IntroMovieStage.Entrance)
        {
            int speed = 55, position = 0, gs = 1024, gp = 24 * 256;
            for (int i = 1; i <= f; i++) { if (i >= 40 && speed > 1) speed--; position += speed; if (i >= 40 && gs > 16) gs -= 16; gp += gs; }
            nx = Math.Min(180, position >> 4); gx = Math.Min(239, gp / 256f);
        }
        if (stage == IntroMovieStage.Cry) { pose = f < 9 ? 2 : 1; ny += f < 9 ? 3 : f / 2 % 2; }
        if (stage == IntroMovieStage.Swipe)
        {
            var swipe = IntroMoviePresentation.GengarSwipeAt(f);
            gengarFrame = swipe.AnimationFrame;
            gx += (float)Math.Cos(swipe.SineIndex * Math.PI / 128) * swipe.AmplitudeX;
            gy -= swipe.AmplitudeY * (1 - (float)Math.Sin(swipe.SineIndex * Math.PI / 128));
            if (f >= 36 && f < 48 && f % 2 == 0)
            { MovieSprite("swipe", 116, 46, f < 44 ? 0 : 1, 64); DrawTextureRectRegion(_introMovieTextures["swipe"], new(116, 110, 32, 16), new(0, f < 44 ? 128 : 144, 32, 16)); }
            if (f >= 32) ApplyMovieRecoil(f - 32, ref nx, ref ny, ref pose);
        }
        if (stage == IntroMovieStage.Recoil) ApplyMovieRecoil(f + 15, ref nx, ref ny, ref pose);
        if (stage is IntroMovieStage.HopOne or IntroMovieStage.HopTwo)
        { int h = Math.Clamp(f - 5, 0, 8); nx += (stage == IntroMovieStage.HopTwo ? 10.5f : 0) + h * 1.5f; ny -= (float)Math.Sin(h * Math.PI / 8) * 8; pose = h > 0 && h < 8 ? 3 : 2; }
        if (stage >= IntroMovieStage.WaitCharge) nx += 21;
        if (stage is >= IntroMovieStage.Charge)
        {
            pose = f < 58 && stage == IntroMovieStage.Charge ? 2 : 4;
            int t = stage == IntroMovieStage.Charge ? Math.Max(0, f - 58) : 80, travel = 0, speed = 36;
            for (int i = 0; i < t && travel >> 4 <= 63; i++) { travel += speed; if (speed > 12) speed--; }
            nx -= travel >> 4; ny -= (float)Math.Sin((travel >> 4) * Math.PI / 128) * 48;
            if (stage == IntroMovieStage.Charge && f < 18) nx += f / 2 % 2;
            float white = stage == IntroMovieStage.Charge ? Math.Clamp((f - 48) / 48f, 0, 1) : 1;
            DrawRect(new(0, 32, 240, 96), new Color(1, 1, 1, white));
        }
        float zoom = stage == IntroMovieStage.Zoom ? 1 + f / 8f : stage == IntroMovieStage.Fade ? 2 : 1;
        if (_introMovie.GengarAsSprite) MovieSprite("gengar-static", 17 - (zoom - 1) * 63, 40 - (zoom - 1) * 63, scale: zoom);
        else MovieLayer("fight-gengar", gx, gy + gengarFrame * 128);
        MovieSprite("nidorino", nx - 32, ny - 32 - (zoom - 1) * 42, pose, 64, zoom);
        if (_introMovie.FightGrassVisible) MovieSprite("grass", _introMovie.FightGrassX - 32, 96, 0, 32);
        if (stage == IntroMovieStage.Fade) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, f / 8f));
    }
    private void ApplyMovieRecoil(int t, ref float x, ref float y, ref int pose)
    {
        pose = 2;
        if (t < 5) return;
        int air = Math.Min(16, t - 5); float offset = (air * 40 - air * (air - 1) / 2) / 16f;
        if (t < 21) { x += offset; y -= (float)Math.Sin(air * Math.PI / 16) * 24; pose = 3; return; }
        offset += Math.Min(13, t - 21) * 28 / 16f;
        if (t >= 34) pose = 0;
        if (t >= 51) { float h = Math.Clamp((t - 56) / 16f, 0, 1); offset *= 1 - h; y -= (float)Math.Sin(h * Math.PI) * 16; pose = t < 56 ? 2 : h < 1 ? 3 : 0; }
        x += offset;
        for (int i = 0; i < 7; i++)
        {
            int age = t - 28 - i;
            if (age is >= 0 and < 38 && age % 4 < 2)
            { uint r = IntroMoviePresentation.SparkleRandom(i); MovieSprite("dust", x - 30 - age * ((r % 13 + 8) / 16f), y + 16 + age * (r % 3) / 16f, Math.Min(3, age / 10), 16); }
        }
    }
    private void DisposeIntroMoviePresentation()
    { foreach (var texture in _introMovieTextures.Values) texture.Dispose(); _introMovieTextures.Clear(); }
}
