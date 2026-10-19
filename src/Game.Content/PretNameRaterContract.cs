using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretNameRaterContract
{
    public const string RelativePath = "content/generated/data/services/name-rater.json";
    public NameRaterSourceContent Source { get; }
    private PretNameRaterContract(NameRaterSourceContent source) { Source = source; }
    public static PretNameRaterContract Load(string root, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var source = ContentJson.ParseStrict<NameRaterSourceContent>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (string.IsNullOrEmpty(source.ChoosePokemonText) || string.IsNullOrEmpty(source.ChoosePokemonCancelText))
            throw new InvalidDataException("name_rater_prompt_missing");
        sourceTexts ??= PretBattleText.Load(root);
        return new(source with { ChoosePokemonText = sourceTexts["gText_ChoosePokemon"],
            ChoosePokemonCancelText = sourceTexts["gText_ChoosePokemonCancel"] });
    }
}
