using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Generated-only opening text and shared naming presentation. No raw source is read at runtime.</summary>
public sealed class PretOpeningPresentation
{
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<ImmutableArray<string>> Keyboard { get; }
    public ImmutableDictionary<string, ImmutableArray<OpeningAnimationFrame>> Animations { get; }
    public ImmutableArray<string> RivalNames { get; }
    public ImmutableArray<string> RivalNameSymbols { get; }
    public ImmutableArray<ImmutableArray<int>> NamingColumnOffsets { get; }
    public ImmutableDictionary<string, NamingScreenTemplate> NamingTemplates { get; }
    public NamingScreenLayout NamingLayout { get; }
    public static readonly ImmutableArray<string> TextureNames = ["title", "background", "oak", "red", "leaf", "red-white", "leaf-white", "rival", "platform", "nidoran-f", "title-border", "title-lettering", "title-lettering-hidden", "title-mon", "title-mon-gray", "title-mon-white", "title-logo", "flames", "slash", "pikachu-body", "pikachu-ears", "pikachu-eyes", "controls-2", "controls-3", "pikachu-background", "naming-background", "naming-keyboard-upper", "naming-keyboard-lower", "naming-keyboard-symbols", "naming-back-button", "naming-ok-button", "naming-page-swap-frame", "naming-page-swap-upper", "naming-page-swap-lower", "naming-page-swap-others", "naming-page-button-upper", "naming-page-button-lower", "naming-page-button-others", "naming-cursor", "naming-input-arrow", "naming-underscore", "naming-rival", "naming-red", "naming-leaf", "naming-pc-icon"];
    private readonly string directory;
    private PretOpeningPresentation(string directory, ImmutableDictionary<string, string> texts, ImmutableArray<ImmutableArray<string>> keyboard, ImmutableDictionary<string, ImmutableArray<OpeningAnimationFrame>> animations, ImmutableArray<string> rivalNameSymbols, ImmutableArray<ImmutableArray<int>> namingColumnOffsets, ImmutableDictionary<string, NamingScreenTemplate> namingTemplates, NamingScreenLayout namingLayout)
    { this.directory = directory; Texts = texts; Keyboard = keyboard; Animations = animations; RivalNameSymbols = rivalNameSymbols; RivalNames = rivalNameSymbols.Select(symbol => texts[symbol]).ToImmutableArray(); NamingColumnOffsets = namingColumnOffsets; NamingTemplates = namingTemplates; NamingLayout = namingLayout; }

    public static PretOpeningPresentation Load(string root, SourceTextPack? pack = null,
        IReadOnlyDictionary<string, string>? battleText = null)
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
        foreach (var (name, frames) in new[] { ("flames", 10), ("pikachu-body", 2), ("pikachu-ears", 2), ("pikachu-eyes", 2), ("naming-player", 9), ("naming-rival", 9), ("naming-pc", 2) })
            if (!animations.TryGetValue(name, out var track) || track.IsDefaultOrEmpty || track.Any(f => f.Frame < 0 || f.Frame >= frames || f.Duration < 1 || f.Duration > 60)) throw new InvalidDataException("Opening animation invalid: " + name);
        foreach (string name in new[] { "Intro", "DPad", "AButton", "BButton", "StartButton", "SelectButton", "LRButtons" })
            if (!texts.TryGetValue("gControlsGuide_Text_" + name, out var text) || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Controls guide text missing.");
        for (int i = 1; i <= 3; i++)
            if (!texts.TryGetValue("gPikachuIntro_Text_Page" + i, out var text) || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Pikachu intro text missing.");
        var rivalNameSymbols = json.GetProperty("rivalNameSymbols").Deserialize<ImmutableArray<string>>();
        if (rivalNameSymbols.IsDefault || rivalNameSymbols.Length != 4 ||
            rivalNameSymbols.Distinct(StringComparer.Ordinal).Count() != rivalNameSymbols.Length ||
            rivalNameSymbols.Any(symbol => !symbol.StartsWith("gNameChoice_", StringComparison.Ordinal) || !texts.ContainsKey(symbol)))
            throw new InvalidDataException("Rival name suggestion identities invalid.");
        var namingColumnOffsets = json.GetProperty("namingColumnOffsets").Deserialize<ImmutableArray<ImmutableArray<int>>>();
        if (namingColumnOffsets.Length != keyboard.Length || namingColumnOffsets.Where((row, i) => row.Length != keyboard[i][0].Length || row.Any(x => x is < 0 or > 140) || !row.SequenceEqual(row.Distinct().Order())).Any())
            throw new InvalidDataException("Naming column positions invalid.");
        var namingOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = true };
        if (!json.TryGetProperty("namingTemplates", out var templatesJson) || !json.TryGetProperty("namingLayout", out var layoutJson))
            throw new InvalidDataException("Naming templates or layout missing.");
        ImmutableDictionary<string, NamingScreenTemplate> namingTemplates;
        NamingScreenLayout namingLayout;
        try
        {
            namingTemplates = templatesJson.Deserialize<ImmutableDictionary<string, NamingScreenTemplate>>(namingOptions) ?? throw new InvalidDataException("Naming templates missing.");
            namingLayout = layoutJson.Deserialize<NamingScreenLayout>(namingOptions) ?? throw new InvalidDataException("Naming layout missing.");
        }
        catch (JsonException exception) { throw new InvalidDataException("Naming presentation invalid.", exception); }
        string[] templateIds = ["NAMING_SCREEN_PLAYER", "NAMING_SCREEN_BOX", "NAMING_SCREEN_CAUGHT_MON", "NAMING_SCREEN_NICKNAME", "NAMING_SCREEN_RIVAL"];
        if (namingTemplates.Count != templateIds.Length || templateIds.Any(id => !namingTemplates.ContainsKey(id)))
            throw new InvalidDataException("Naming templates incomplete.");
        if (namingLayout.DisplayWidth != 240 || namingLayout.TitleX is < 0 or >= 240 || namingLayout.TitleY is < 16 or >= 64 ||
            namingLayout.EntryY is < 32 or >= 80 || namingLayout.EntryCellWidth is < 1 or > 16 || namingLayout.EntryCenterOffset is < 0 or > 16 ||
            namingLayout.GenderX is < 0 or >= 240 || namingLayout.GenderY is < 32 or >= 80)
            throw new InvalidDataException("Naming layout invalid.");
        foreach (var (id, template) in namingTemplates)
            if (template is null || template.MaxChars is < 1 or > 10 || template.IconFunction is < 1 or > 4 ||
                template.InitialPage < 0 || template.InitialPage >= keyboard.Length ||
                string.IsNullOrWhiteSpace(template.Title) || template.Title.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(template.TitleSymbol) ||
                template.IconX is < 16 or >= 224 || template.IconY is < 16 or >= 64 ||
                template.PrefixSpeciesName != template.AddGenderIcon || template.PrefixSpeciesName != (template.IconFunction == 3) ||
                namingLayout.EntryX(template.MaxChars) < 64 || namingLayout.EntryX(template.MaxChars) + template.MaxChars * namingLayout.EntryCellWidth > 176)
                throw new InvalidDataException("Naming template invalid: " + id);
        if (namingTemplates["NAMING_SCREEN_BOX"].MaxChars != PokemonStorageCustomizationRules.BoxNameLength ||
            namingTemplates["NAMING_SCREEN_CAUGHT_MON"] != namingTemplates["NAMING_SCREEN_NICKNAME"])
            throw new InvalidDataException("Naming template contracts disagree.");
        var selected = pack ?? SourceTextPack.Current;
        var speech = texts.Where(pair => !pair.Key.StartsWith("gNameChoice_", StringComparison.Ordinal))
            .ToImmutableDictionary(StringComparer.Ordinal);
        texts = texts.SetItems(selected.Apply("opening", speech, preserveControls: true));
        // These are preset choices. Existing player/rival values never pass through this projection.
        var names = rivalNameSymbols.Append("gNameChoice_Red").Append("gNameChoice_Leaf")
            .Distinct(StringComparer.Ordinal).ToImmutableDictionary(symbol => symbol, symbol => texts[symbol], StringComparer.Ordinal);
        names = selected.Apply("opening-names", names);
        if (names.Values.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 7 ||
            name.Any(character => !PokemonStateValidator.NicknameAlphabet.Contains(character))))
            throw new InvalidDataException("Opening name suggestions violate the existing name encoding contract.");
        texts = texts.SetItems(names);
        var source = battleText ?? PretBattleText.Load(root, selected);
        string Title(NamingScreenTemplate template) => source.TryGetValue(template.TitleSymbol, out var title) &&
            !string.IsNullOrWhiteSpace(title) && !title.Any(character => char.IsControl(character) || character is '{' or '}' or '\\')
            ? title : throw new InvalidDataException("Naming title source invalid: " + template.TitleSymbol);
        namingTemplates = namingTemplates.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value with { Title = Title(pair.Value) }, StringComparer.Ordinal);
        return new(directory, texts, keyboard, animations, rivalNameSymbols, namingColumnOffsets, namingTemplates, namingLayout);
    }

    public ImmutableArray<string> Pages(string name, string player, string rival) => Texts["gOakSpeech_Text_" + name]
        .Split('\f', StringSplitOptions.RemoveEmptyEntries)
        .Select(page => Regex.Replace(page, @"\{(PLAYER|RIVAL)\}",
            match => match.Groups[1].Value == "PLAYER" ? player : rival)).ToImmutableArray();

    public byte[] ReadTexture(string name)
    {
        if (!TextureNames.Contains(name)) throw new ArgumentOutOfRangeException(nameof(name));
        return File.ReadAllBytes(Path.Combine(directory, name + ".png"));
    }
}

public sealed record OpeningAnimationFrame(int Frame, int Duration);

/// <summary>InitialPage indexes the generated keyboard order: upper, lower, symbols.</summary>
public sealed record NamingScreenTemplate(bool CopyExistingString, int MaxChars, int IconFunction,
    bool AddGenderIcon, int InitialPage, string Title, bool PrefixSpeciesName, int IconX, int IconY)
{
    public string TitleSymbol { get; init; } = "";
}

public sealed record NamingScreenLayout(int DisplayWidth, int TitleX, int TitleY, int EntryY,
    int EntryCellWidth, int EntryCenterOffset, int GenderX, int GenderY)
{
    public int EntryX(int maxChars) => (DisplayWidth - maxChars * EntryCellWidth) / 2 + EntryCenterOffset;
}
