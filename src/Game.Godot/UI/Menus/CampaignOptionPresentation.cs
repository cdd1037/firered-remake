using System.Globalization;
using Game.Content;
using Game.Domain.Options;

/// <summary>Option rows retain their existing indices and typed values; text is only their display.</summary>
internal static class CampaignOptionPresentation
{
    public static string Label(int row, PresentationTextCatalog text) => text.Get(row switch
    {
        0 => "label.text-speed", 1 => "label.battle-scene", 2 => "label.battle-style", 3 => "label.sound",
        4 => "label.button-mode", 5 => "label.frame", 6 => "label.cancel",
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    });

    public static string Value(SourceGameOptions options, int row, PresentationTextCatalog text)
    {
        if (row == 5) return text.Get("value.frame", ("Number", (options.Frame + 1).ToString(CultureInfo.InvariantCulture)));
        string? id = row switch
        {
            0 => options.TextSpeed switch
            {
                SourceTextSpeed.Slow => "value.text-speed.slow", SourceTextSpeed.Mid => "value.text-speed.mid",
                SourceTextSpeed.Fast => "value.text-speed.fast", _ => throw new ArgumentOutOfRangeException(nameof(options))
            },
            1 => options.BattleScene switch
            {
                SourceBattleScene.On => "value.battle-scene.on", SourceBattleScene.Off => "value.battle-scene.off",
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            },
            2 => options.BattleStyle switch
            {
                SourceBattleStyle.Shift => "value.battle-style.shift", SourceBattleStyle.Set => "value.battle-style.set",
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            },
            3 => options.Sound switch
            {
                SourceSound.Mono => "value.sound.mono", SourceSound.Stereo => "value.sound.stereo",
                _ => throw new ArgumentOutOfRangeException(nameof(options))
            },
            4 => options.ButtonMode switch
            {
                SourceButtonMode.Help => "value.button-mode.help", SourceButtonMode.Lr => "value.button-mode.lr",
                SourceButtonMode.LEqualsA => "value.button-mode.l-equals-a", _ => throw new ArgumentOutOfRangeException(nameof(options))
            },
            _ => null
        };
        return id is null ? "" : text.Get(id);
    }
}
