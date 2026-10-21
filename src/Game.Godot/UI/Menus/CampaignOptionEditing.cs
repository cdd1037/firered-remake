using Game.Domain.Options;

/// <summary>Shared option-row edits for keyboard and pointer input. Draft changes
/// are presentation-only until the existing options_save transaction accepts them.</summary>
public static class CampaignOptionEditing
{
    public static bool IsEditable(int row, bool framesAvailable) =>
        row is 0 or 1 or 2 or 3 or 4 || row == 5 && framesAvailable;

    public static SourceGameOptions Change(SourceGameOptions options, int row, int direction, bool framesAvailable)
    {
        if (!IsEditable(row, framesAvailable) || direction == 0) return options;
        int step = Math.Sign(direction);
        return row switch
        {
            0 => options with { TextSpeed = (SourceTextSpeed)(((int)options.TextSpeed + step + 3) % 3) },
            1 => options with { BattleScene = options.BattleScene == SourceBattleScene.On ? SourceBattleScene.Off : SourceBattleScene.On },
            2 => options with { BattleStyle = options.BattleStyle == SourceBattleStyle.Shift ? SourceBattleStyle.Set : SourceBattleStyle.Shift },
            3 => options with { Sound = options.Sound == SourceSound.Mono ? SourceSound.Stereo : SourceSound.Mono },
            4 => options with { ButtonMode = (SourceButtonMode)(((int)options.ButtonMode + step + 3) % 3) },
            5 => options with { Frame = (options.Frame + step + SourceGameOptions.FrameCount) % SourceGameOptions.FrameCount },
            _ => options
        };
    }
}
