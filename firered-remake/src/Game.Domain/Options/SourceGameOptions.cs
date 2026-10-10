namespace Game.Domain.Options;

// Values are the original SaveBlock2 option values, not presentation indices.
public enum SourceTextSpeed { Slow = 0, Mid = 1, Fast = 2 }
public enum SourceBattleScene { On = 0, Off = 1 }
public enum SourceBattleStyle { Shift = 0, Set = 1 }
public enum SourceSound { Mono = 0, Stereo = 1 }
public enum SourceButtonMode { Help = 0, Lr = 1, LEqualsA = 2 }

/// <summary>The six original options. Frame is zero-based: source TYPE 1 through TYPE 10.</summary>
public sealed record SourceGameOptions(
    SourceTextSpeed TextSpeed = SourceTextSpeed.Mid,
    SourceBattleScene BattleScene = SourceBattleScene.On,
    SourceBattleStyle BattleStyle = SourceBattleStyle.Shift,
    SourceSound Sound = SourceSound.Mono,
    SourceButtonMode ButtonMode = SourceButtonMode.Help,
    int Frame = 0)
{
    public static SourceGameOptions Defaults { get; } = new();
    public const int FrameCount = 10;

    public static string? Validate(SourceGameOptions options)
    {
        if (!Enum.IsDefined(options.TextSpeed)) return "options_text_speed_invalid";
        if (!Enum.IsDefined(options.BattleScene)) return "options_battle_scene_invalid";
        if (!Enum.IsDefined(options.BattleStyle)) return "options_battle_style_invalid";
        if (!Enum.IsDefined(options.Sound)) return "options_sound_invalid";
        if (!Enum.IsDefined(options.ButtonMode)) return "options_button_mode_invalid";
        if (options.Frame is < 0 or >= FrameCount) return "options_frame_invalid";
        return null;
    }
}
