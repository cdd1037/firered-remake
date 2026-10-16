using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Generated-only opening text and optional naming keyboard. No raw source is read at runtime.</summary>
public sealed class PretOpeningPresentation
{
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<ImmutableArray<string>> Keyboard { get; }
    private readonly string directory;
    private PretOpeningPresentation(string directory, ImmutableDictionary<string, string> texts, ImmutableArray<ImmutableArray<string>> keyboard)
    { this.directory = directory; Texts = texts; Keyboard = keyboard; }

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
        return new(directory, texts, keyboard);
    }

    public ImmutableArray<string> Pages(string name, string player, string rival) => Texts["gOakSpeech_Text_" + name]
        .Replace("{PLAYER}", player, StringComparison.Ordinal).Replace("{RIVAL}", rival, StringComparison.Ordinal)
        .Split('\f', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();

    public byte[] ReadTexture(string name)
    {
        if (name is not ("title" or "background" or "oak" or "red" or "leaf" or "rival" or "platform" or "nidoran-f")) throw new ArgumentOutOfRangeException(nameof(name));
        return File.ReadAllBytes(Path.Combine(directory, name + ".png"));
    }
}
