using System.Diagnostics;
using System.Text.Json;
using FireRedAudio;
using Godot;

public partial class PcmDemo : Node
{
    const int Block = 512;
    readonly float[] pcm = new float[Block * 2];
    readonly Vector2[] frames = new Vector2[Block];
    readonly List<object> events = new();
    readonly Stopwatch clock = new();
    AudioStreamPlayer player = null!;
    AudioStreamGenerator generator = null!;
    AudioEffectCapture capture = null!;
    long capturedFrames, mutedFrames;
    double capturedEnergy, mutedEnergy;
    AudioStreamGeneratorPlayback playback = null!;
    Synth synth = null!;
    Label label = null!;
    int step, capacity, totalSkips, baselineSkips, startupSkips, afterStallSkips, observedKindsMask, lastFreeAfterPump, maxConsumedBurst;
    long pushedFrames, pumpCalls, generatedBlocks;
    double gain = 1, peak;
    bool finished, released;
    readonly bool interactive = System.Environment.GetEnvironmentVariable("DEMO_INTERACTIVE") == "1";
    readonly double duration = double.Parse(System.Environment.GetEnvironmentVariable("DEMO_SECONDS") ?? "58", System.Globalization.CultureInfo.InvariantCulture);
    readonly int targetQueuedFrames = int.Parse(System.Environment.GetEnvironmentVariable("DEMO_QUEUE") ?? "5120");
    readonly float bufferSeconds = float.Parse(System.Environment.GetEnvironmentVariable("DEMO_BUFFER") ?? "0.1", System.Globalization.CultureInfo.InvariantCulture);
    string resultPath = "";

    public override void _Ready()
    {
        try
        {
            GetTree().AutoAcceptQuit = false;
            string root = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
            resultPath = Path.Combine(root, interactive ? "results/phase5-godot/interactive.json" : "results/phase5-godot/headless.json");
            string? testCase = System.Environment.GetEnvironmentVariable("DEMO_CASE");
            if (testCase is not null)
            {
                if (testCase.Length > 40 || testCase.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                    throw new ArgumentException("Invalid experiment case name");
                string outputGroup = System.Environment.GetEnvironmentVariable("DEMO_RESULTS_GROUP") ?? "phase6-latency";
                if (outputGroup.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                    throw new ArgumentException("Invalid experiment output group");
                resultPath = Path.Combine(root, "results", outputGroup, testCase + ".json");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
            var resources = Resources.FromPack(Path.Combine(root, "resources/firered.pack"));
            var catalog = NormalCryCatalog.FromFile(Path.Combine(root, "resources/normal-cries.bin"));
            synth = new Synth(resources, AudioProfile.SourceFiltered, catalog) { EnableTrace = true };
            synth.PrepareNormalCry(124);
            synth.Start(0x086dd844, 0);
            generator = new AudioStreamGenerator { MixRate = Synth.Rate, BufferLength = bufferSeconds, MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom };
            player = new AudioStreamPlayer { Stream = generator, VolumeDb = 0 };
            AddChild(player);
            capture = new AudioEffectCapture { BufferLength = .5f };
            AudioServer.AddBusEffect(0, capture);
            player.Play();
            player.StreamPaused = true;
            playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
            capacity = playback.GetFramesAvailable();
            label = new Label { Position = new Vector2(24, 24), Text = "Original runtime PCM experiment" };
            AddChild(label);
            if (interactive)
                AddControls();
            clock.Start();
            Pump();
            Record("start");
        }
        catch (Exception ex) { Fail(ex); }
    }

    public override void _Process(double delta)
    {
        if (finished)
            return;
        try
        {
            double now = clock.Elapsed.TotalSeconds;
            if (!interactive)
            {
                if (step == 0 && now >= 2)
                {
                    startupSkips = playback.GetSkips();
                    synth.Start(0x086b5720, 1);
                    Record("select");
                    step++;
                }
                if (step == 1 && now >= 3)
                {
                    synth.StartNormalCry(124);
                    Record("cry-duck");
                    step++;
                }
                if (step == 2 && now >= 4)
                {
                    gain = .35;
                    Record("gain-0.35");
                    step++;
                }
                if (step == 3 && now >= 5)
                {
                    if (!synth.CryPlaying)
                        throw new InvalidOperationException("Cancellation test requires a live cry");
                    Record("before-cancel-active-cry");
                    synth.Stop(2);
                    Record("after-cancel-active-cry");
                    step++;
                }
                if (step == 4 && now >= 5.25)
                {
                    gain = 0;
                    Record("mute");
                    step++;
                }
                if (step == 5 && now >= 6)
                {
                    synth.Stop(0);
                    synth.Stop(1);
                    synth.Stop(2);
                    player.Stop();
                    player.Stream = null;
                    playback.Dispose();
                    generator.Dispose();
                    generator = new AudioStreamGenerator { MixRate = Synth.Rate, BufferLength = bufferSeconds, MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom };
                    player.Stream = generator;
                    player.Play();
                    player.StreamPaused = true;
                    playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
                    capacity = playback.GetFramesAvailable();
                    Record("stop-replace-output-ring");
                    step++;
                }
                if (step == 6 && now >= 7)
                {
                    gain = 1;
                    synth.Start(0x086dd844, 0);
                    Record("restart-pallet");
                    step++;
                }
                if (step == 7 && now >= 8)
                {
                    baselineSkips = playback.GetSkips();
                    Record("intentional-250ms-producer-stall");
                    Thread.Sleep(250);
                    step++;
                }
                if (step == 8 && now >= 9) { afterStallSkips = playback.GetSkips(); Record("recovered-after-stall"); step++; }
                if (step == 9 && now >= 10) { synth.Start(0x086bc670, 1); Record("door-noise-overlay"); step++; }
                if (step == 10 && now >= 52) { synth.Start(0x086d1ddc, 0); Record("route1-wave-switch"); step++; }
            }
            Pump();
            foreach (var sample in capture.GetBuffer(capture.GetFramesAvailable()))
            {
                double energy = sample.X * sample.X + sample.Y * sample.Y;
                capturedEnergy += energy;
                capturedFrames++;
                if (now >= 5.8 && now < 6)
                {
                    mutedEnergy += energy;
                    mutedFrames++;
                }
            }
            totalSkips = playback.GetSkips();
            label.Text = $"Original C# synthesis → Godot PCM\nProfile: SourceFiltered  48kHz stereo\nTime: {now:F1}s   queue: {(capacity - playback.GetFramesAvailable()) / 48.0:F1} ms\nUnderruns: {totalSkips}  Cry: {synth.CryPlaying}  Duck: {synth.BgmDucked}  Gain: {gain:F2}\nPeak: {peak:F4}  PCM voices: {synth.MaxPcm}\nNo recorded-audio fallback. Device may be Dummy.";
            if (!interactive && now >= duration)
                Finish();
        }
        catch (Exception ex) { Fail(ex); }
    }

    void AddControls()
    {
        var box = new VBoxContainer { Position = new Vector2(24, 210), Size = new Vector2(700, 230) };
        AddChild(box);
        void Button(string text, Action action)
        {
            var button = new Button { Text = text };
            button.Pressed += () =>
            {
                try
                {
                    action();
                    if (!finished) { Pump(); Record("ui:" + text); }
                }
                catch (Exception ex) { GD.PrintErr(ex.Message); }
            };
            box.AddChild(button);
        }
        Button("Restart Pallet BGM", () => synth.Start(0x086dd844, 0));
        Button("Play SELECT over BGM", () => synth.Start(0x086b5720, 1));
        Button("Play Jynx normal cry + duck", () => synth.StartNormalCry(124));
        Button("Cancel active cry", () => synth.Stop(2));
        Button("Stop BGM", () => synth.Stop(0));
        var volume = new HSlider { MinValue = 0, MaxValue = 1, Step = .01, Value = 1 };
        volume.ValueChanged += value => gain = value;
        box.AddChild(volume);
        Button("Close and release audio", Finish);
    }

    void Pump()
    {
        pumpCalls++;
        if (pumpCalls > 1)
            maxConsumedBurst = Math.Max(maxConsumedBurst, playback.GetFramesAvailable() - lastFreeAfterPump);
        while (playback.GetFramesAvailable() >= Block && capacity - playback.GetFramesAvailable() + Block <= targetQueuedFrames)
        {
            synth.Render(pcm);
            foreach (var voice in synth.Voices)
                if (voice.Active) observedKindsMask |= 1 << (voice.Instrument!.Type & 7);
            for (int i = 0; i < Block; i++)
            {
                float left = (float)(pcm[i * 2] * gain), right = (float)(pcm[i * 2 + 1] * gain);
                if (!float.IsFinite(left) || !float.IsFinite(right))
                    throw new InvalidDataException("Nonfinite PCM");
                peak = Math.Max(peak, Math.Max(Math.Abs(left), Math.Abs(right)));
                frames[i] = new Vector2(left, right);
            }
            if (!playback.PushBuffer(frames))
                throw new InvalidOperationException("Audio ring rejected available block");
            pushedFrames += Block;
            generatedBlocks++;
        }
        lastFreeAfterPump = playback.GetFramesAvailable();
        player.StreamPaused = false;
    }

    void Record(string name) => events.Add(new
    {
        name,
        wallSeconds = clock.Elapsed.TotalSeconds,
        producedFrame = synth.OutputFrames,
        queuedFrames = capacity - playback.GetFramesAvailable(),
        skips = playback.GetSkips(),
        ducked = synth.BgmDucked,
        cryPlaying = synth.CryPlaying,
        gain
    });

    async void Finish()
    {
        if (finished) return;
        Record("finish");
        string[] loops = synth.Trace.Where(x => x.Contains("goto ")).ToArray();
        var report = new
        {
            scope = "Isolated Godot AudioStreamGenerator; Dummy/headless is stream validation, no audible-device acceptance",
            rate = Synth.Rate,
            block = Block,
            requestedBufferSeconds = bufferSeconds,
            mixRate = AudioServer.GetMixRate(),
            outputLatency = AudioServer.GetOutputLatency(),
            actualCapacityFrames = capacity,
            queueUpperBoundMs = Math.Min(capacity, targetQueuedFrames) / 48.0,
            targetQueuedFrames,
            elapsedSeconds = clock.Elapsed.TotalSeconds,
            pushedFrames,
            generatedBlocks,
            pumpCalls,
            maxConsumedBurst,
            startupSkips,
            baselineSkips,
            finalSkips = totalSkips,
            afterStallSkips,
            unplannedSkipsAfterRecovery = totalSkips - afterStallSkips,
            deliberateProducerStallMs = 250,
            recoveredAfterStall = pushedFrames > 9 * Synth.Rate,
            peak,
            observedKindsMask,
            synth.Clipped,
            synth.MaxPcm,
            events,
            loops,
            capturedFrames,
            capturedRms = Math.Sqrt(capturedEnergy / Math.Max(1, 2 * capturedFrames)),
            mutedFrames,
            mutedRms = Math.Sqrt(mutedEnergy / Math.Max(1, 2 * mutedFrames)),
            captureDiscardedFrames = capture.GetDiscardedFrames(),
            workingSetBytes = Process.GetCurrentProcess().WorkingSet64
        };
        File.WriteAllText(resultPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        finished = true;
        ReleaseOutput();
        // Let the audio server retire its stopped playback reference before process shutdown.
        await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        GD.Print($"PCM_DEMO_COMPLETE {resultPath}");
        GetTree().Quit();
    }

    void Fail(Exception ex)
    {
        finished = true;
        GD.PrintErr(ex.ToString());
        GetTree().Quit(1);
    }

    void ReleaseOutput()
    {
        if (released) return;
        released = true;
        if (GodotObject.IsInstanceValid(player)) { player.Stop(); player.Stream = null; }
        playback?.Dispose();
        generator?.Dispose();
        if (capture is not null)
        {
            AudioServer.RemoveBusEffect(0, 0);
            capture.Dispose();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && !finished) Finish();
    }

    public override void _ExitTree() => ReleaseOutput();
}
