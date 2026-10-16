namespace Game.Domain.Battle;

public readonly record struct SourceSecondaryEffectSelection(bool Apply, bool Certain, byte Effect);

/// <summary>The source seteffectwithchance command, including its short-circuit RNG order.</summary>
public static class SourceSecondaryEffectRules
{
    public const byte CertainFlag = 0x80;

    public static SourceSecondaryEffectSelection Select(int chance, bool sereneGrace, byte effect,
        bool noEffect, Func<int> draw, bool strictTwineedle = false)
    {
        ArgumentNullException.ThrowIfNull(draw);
        int percent = chance * (sereneGrace ? 2 : 1);
        if ((effect & CertainFlag) != 0 && !noEffect)
            return new(true, true, (byte)(effect & ~CertainFlag));
        // Even a zero effect byte or NO_EFFECT result reaches Random. A >=100
        // percentage is certain only AFTER that draw; it is not the certain-bit branch.
        int roll = draw() % 100;
        bool rolled = strictTwineedle ? roll < percent : roll <= percent;
        return rolled && effect != 0 && !noEffect ? new(true, percent >= 100, effect) : default;
    }
}
