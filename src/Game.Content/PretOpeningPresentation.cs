using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Generated-only opening text and optional naming keyboard. No raw source is read at runtime.</summary>
public sealed class PretOpeningPresentation
{
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<ImmutableArray<string>> Keyboard { get; }
    public ImmutableDictionary<string, ImmutableArray<OpeningAnimationFrame>> Animations { get; }
    public ImmutableArray<string> RivalNames { get; }
    public ImmutableArray<ImmutableArray<int>> NamingColumnOffsets { get; }
    public static readonly ImmutableArray<string> TextureNames = ["title", "background", "oak", "red", "leaf", "red-white", "leaf-white", "rival", "platform", "nidoran-f", "title-border", "title-lettering", "title-lettering-hidden", "title-mon", "title-mon-gray", "title-mon-white", "title-logo", "flames", "slash", "pikachu-body", "pikachu-ears", "pikachu-eyes", "controls-2", "controls-3", "pikachu-background", "naming-background", "naming-keyboard-upper", "naming-keyboard-lower", "naming-keyboard-symbols", "naming-back-button", "naming-ok-button", "naming-page-swap-frame", "naming-page-swap-upper", "naming-page-swap-lower", "naming-page-swap-others", "naming-page-button-upper", "naming-page-button-lower", "naming-page-button-others", "naming-cursor", "naming-input-arrow", "naming-underscore", "naming-rival", "naming-red", "naming-leaf"];
    private readonly string directory;
    private PretOpeningPresentation(string directory, ImmutableDictionary<string, string> texts, ImmutableArray<ImmutableArray<string>> keyboard, ImmutableDictionary<string, ImmutableArray<OpeningAnimationFrame>> animations, ImmutableArray<string> rivalNames, ImmutableArray<ImmutableArray<int>> namingColumnOffsets)
    { this.directory = directory; Texts = texts; Keyboard = keyboard; Animations = animations; RivalNames = rivalNames; NamingColumnOffsets = namingColumnOffsets; }

    public static PretOpeningPresentation Load(string root)
    {
        string directory = Path.Combine(root, "content/generated/ui/opening");
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "opening.json")));
        var json = document.RootElement;
        if (json.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Opening schema differs.");
        var texts = json.GetProperty("texts").Deserialize<ImmutableDictionary<string, string>>() ?? throw new InvalidDataException("Opening text missing.");
        var keyboard = json.GetProperty("keyboard").Deserialize<ImmutableArray<ImmutableArray<string>>>();
        if (keyboard.Length != 3 || keyboard.Where((page, i) => page.Length != 4 || page.Any(row => row.Length != (i == 2 ? 6 : 8) || row.Any(c => !PokemonStateValidator.NicknameAlphabet.Contains(c)))).Any())
            throw new InvalidDataException("Opening keyboard invalid.");
        foreach (string name in new[] { "WelcomeToTheWorld", "ThisWorld", "IsInhabitedFarAndWide", "IStudyPokemon", "TellMeALittleAboutYourself", "AskPlayerGender", "YourNameWhatIsIt", "SoYourNameIsPlayer", "WhatWasHisName", "YourRivalsNameWhatWasIt", "ConfirmRivalName", "RememberRivalsName", "LetsGo" })
            if (!texts.TryGetValue("gOakSpeech_Text_" + name, out string? text) || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Opening text missing: " + name);
        var animations = json.GetProperty("animations").Deserialize<ImmutableDictionary<string, ImmutableArray<OpeningAnimationFrame>>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Opening animations missing.");
        foreach (var (name, frames) in new[] { ("flames", 10), ("pikachu-body", 2), ("pikachu-ears", 2), ("pikachu-eyes", 2), ("naming-player", 9), ("naming-rival", 9) })
            if (!animations.TryGetValue(name, out var track) || track.IsDefaultOrEmpty || track.Any(f => f.Frame < 0 || f.Frame >= frames || f.Duration < 1 || f.Duration > 60)) throw new InvalidDataException("Opening animation invalid: " + name);
        foreach (string name in new[] { "Intro", "DPad", "AButton", "BButton", "StartButton", "SelectButton", "LRButtons" })
            if (!texts.TryGetValue("gControlsGuide_Text_" + name, out var text) || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Controls guide text missing.");
        for (int i = 1; i <= 3; i++)
            if (!texts.TryGetValue("gPikachuIntro_Text_Page" + i, out var text) || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Pikachu intro text missing.");
        var rivalNames = json.GetProperty("rivalNames").Deserialize<ImmutableArray<string>>();
        if (rivalNames.IsDefault || rivalNames.Length != 4 || rivalNames.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 7 || n.Any(c => !PokemonStateValidator.NicknameAlphabet.Contains(c)))) throw new InvalidDataException("Rival name suggestions invalid.");
        var namingColumnOffsets = json.GetProperty("namingColumnOffsets").Deserialize<ImmutableArray<ImmutableArray<int>>>();
        if (namingColumnOffsets.Length != keyboard.Length || namingColumnOffsets.Where((row, i) => row.Length != keyboard[i][0].Length || row.Any(x => x is < 0 or > 140) || !row.SequenceEqual(row.Distinct().Order())).Any())
            throw new InvalidDataException("Naming column positions invalid.");
        return new(directory, texts, keyboard, animations, rivalNames, namingColumnOffsets);
    }

    public ImmutableArray<string> Pages(string name, string player, string rival) => Texts["gOakSpeech_Text_" + name]
        .Replace("{PLAYER}", player, StringComparison.Ordinal).Replace("{RIVAL}", rival, StringComparison.Ordinal)
        .Split('\f', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();

    public byte[] ReadTexture(string name)
    {
        if (!TextureNames.Contains(name)) throw new ArgumentOutOfRangeException(nameof(name));
        return File.ReadAllBytes(Path.Combine(directory, name + ".png"));
    }
}

public sealed record OpeningAnimationFrame(int Frame, int Duration);
