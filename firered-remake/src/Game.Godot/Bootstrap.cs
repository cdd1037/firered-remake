using Game.Application;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Godot;

public partial class Bootstrap : Node2D
{
    private int _frames;
    private bool _harness;
    private bool _story;
    private bool _probe;
    private Font _font = null!;
    private string _lastKey = "等待按键";
    private AudioStreamPlayer _tone = null!;
    private string _evidencePath = "";
    public override void _Ready()
    {
        GameStartupSelection startup;
        try
        {
            var environment=System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(e=>(string)e.Key,e=>(string?)e.Value,StringComparer.Ordinal);
            startup=GameStartupSelection.Resolve(OS.GetCmdlineUserArgs(),environment);
        }
        catch(InvalidOperationException error)
        {
            GD.PushError(error.Message);GetTree().Quit(22);return;
        }
        _probe=startup.Mode==GameStartupMode.Probe;
        _harness=startup.Mode==GameStartupMode.Harness;
        _story=startup.Mode==GameStartupMode.Current;
        _evidencePath = OS.GetEnvironment("FIRERED_EVIDENCE");
        if (!string.IsNullOrEmpty(_evidencePath)) System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(_evidencePath, ".events.log"), "");
        var evidence = new
        {
            engine = Engine.GetVersionInfo()["string"].AsString(),
            framework = RuntimeInformation.FrameworkDescription,
            runtime = System.Environment.Version.ToString(),
            runtimeMajor = System.Environment.Version.Major,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            targetFramework = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName,
            coreLibrary = typeof(object).Assembly.Location,
            display = DisplayServer.GetName(),
            audio = AudioServer.GetDriverName(),
            nodeEnteredSceneTree = IsInsideTree(),
            moduleVersionId = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString(),
            probeRevision = 2
        };
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
        Record("RUNTIME_PROBE " + json);
        if (!string.IsNullOrEmpty(_evidencePath))
            System.IO.File.WriteAllText(_evidencePath, json);
        if (System.Environment.Version.Major != 10 || !evidence.engine.StartsWith("4.7.2", StringComparison.Ordinal))
        {
            GD.PushError("Required Godot 4.7.2 / actual .NET runtime major 10 did not match.");
            GetTree().Quit(17);
            return;
        }
        _font = GD.Load<FontFile>("res://Assets/ProbeFont.ttc");
        if (_font is null)
        {
            GD.PushError("Required font res://Assets/ProbeFont.ttc could not be loaded. From the project repository root, run bash scripts/ensure-godot-imports.sh to restore its Godot import cache, then launch again.");
            GetTree().Quit(18);
            return;
        }
        var missing = "火红工程验证方向键确认退出等待按键".Where(c => !_font.HasChar(c)).ToArray();
        Record("CHINESE_GLYPHS " + (missing.Length == 0 ? "PASS" : "MISSING " + new string(missing)));
        if (_story) { AddChild(new CampaignView{Startup=startup}); return; }
        if (_harness) { AddChild(new EngineeringHarness()); return; }
        var spriteImage = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        spriteImage.Fill(Colors.Transparent);
        for (var y = 1; y < 11; y++) for (var x = 1; x < 11; x++)
            spriteImage.SetPixel(x, y, y < 6 ? new Color("ef685a") : new Color("f5edcf"));
        AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(spriteImage), Position = new Vector2(120, 119) });
        var pcm = new byte[22050];
        for (var i = 0; i < pcm.Length / 2; i++)
        {
            short value = (short)(Math.Sin(i * 2 * Math.PI * 440 / 22050) * 3000);
            pcm[i * 2] = (byte)value; pcm[i * 2 + 1] = (byte)(value >> 8);
        }
        _tone = new AudioStreamPlayer { Stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Data = pcm } };
        AddChild(_tone);
        QueueRedraw();
    }
    private void Record(string text)
    {
        GD.Print(text);
        if (!string.IsNullOrEmpty(_evidencePath)) System.IO.File.AppendAllText(System.IO.Path.ChangeExtension(_evidencePath, ".events.log"), text + "\n");
    }
    public override void _Draw()
    {
        if (_font is null || _harness || _story) return;
        DrawRect(new Rect2(12, 12, 216, 136), new Color("172a43"));
        DrawRect(new Rect2(17, 17, 206, 126), new Color("0d182c"));
        DrawString(_font, new Vector2(25, 44), "火红工程验证", fontSize: 14, modulate: new Color("ffdb85"));
        DrawString(ThemeDB.FallbackFont, new Vector2(25, 70), "Godot 4.7.2 / .NET 10", fontSize: 12);
        DrawString(_font, new Vector2(25, 90), "方向键 / 确认 / Esc退出", fontSize: 10);
        DrawString(_font, new Vector2(25, 139), _lastKey, fontSize: 9);

    }
    public override void _Process(double delta)
    {
        _frames++;
        if (_probe && _frames >= 15) GetTree().Quit();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_harness || _story) return;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            Record("INPUT_PROBE " + key.Keycode);
            _lastKey = key.Keycode.ToString();
            if (key.Keycode == Key.Enter) { _tone.Play(); Record("AUDIO_PLAY_REQUEST 440Hz 500ms driver=" + AudioServer.GetDriverName()); }
            if (key.Keycode == Key.F12 && !string.IsNullOrEmpty(_evidencePath))
            {
                var path = System.IO.Path.ChangeExtension(_evidencePath, ".png");
                var error = GetViewport().GetTexture().GetImage().SavePng(path);
                Record("SCREENSHOT_PROBE " + error + " " + path);
            }
            QueueRedraw();
            if (key.Keycode == Key.Escape) GetTree().Quit();
        }
    }
}
