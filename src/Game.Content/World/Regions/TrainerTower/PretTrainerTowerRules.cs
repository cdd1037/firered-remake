using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record TrainerTowerRulesDocument(TrainerTowerLocalData Local);
public sealed record TrainerTowerPrizeDefinition(TrainerTowerChallengeKind Course, ItemId Item, int SourceItemId, int Quantity);
public sealed record TrainerTowerItemsDocument(ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop,
    ImmutableArray<TrainerTowerPrizeDefinition> Prizes);

/// <summary>Loads the local Trainer Tower courses, opponents and prizes.</summary>
public static class PretTrainerTowerRules
{
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    private static T Document<T>(string root,string name, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(Read(root,"content/generated/data/regions/trainer-tower/" + name), options ?? Options)
            ?? throw new InvalidDataException("trainer_tower_source_missing:" + name);
    public static TrainerTowerRulesDocument LoadDocument(string root)
    {
        var document = Document<TrainerTowerRulesDocument>(root,"catalog.json");
        _ = TrainerTowerCatalog.Authenticate(document.Local);
        return document;
    }
    public static TrainerTowerCatalog Load(string root) => TrainerTowerCatalog.Authenticate(LoadDocument(root).Local);

    /// <summary>Template identity, fixed Pokémon creation and saved course receipts remain source-owned.
    /// Facility speech reuses the same word-ID display catalog as Mail and profile editing.</summary>
    public static TrainerTowerCatalog WithDisplayText(TrainerTowerCatalog source, SourceEasyChatCatalog words,
        SourceTextPack? pack = null)
    {
        string Key(TrainerTowerFloorTemplate floor, TrainerTowerTrainerTemplate trainer) =>
            floor.Symbol + "/" + trainer.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var names = (pack ?? SourceTextPack.Current).Apply("trainer-tower-names", source.Templates.SelectMany(floor =>
            floor.Trainers.Select(trainer => KeyValuePair.Create(Key(floor, trainer), trainer.Name)))
            .ToDictionary(pair => pair.Key, pair => pair.Value), preserveControls: true);
        ImmutableArray<TrainerTowerWord> Speech(ImmutableArray<TrainerTowerWord> speech) => speech.Select(word => {
            if (word.Value is < ushort.MinValue or > ushort.MaxValue ||
                !SourceEasyChatRules.IsKnownWord(words, (ushort)word.Value))
                throw new InvalidDataException("trainer_tower_speech_word_missing: " + word.Value);
            return word with { Text = SourceEasyChatRules.WordText(words, (ushort)word.Value) };
        }).ToImmutableArray();
        return TrainerTowerCatalog.Authenticate(source.Data with {
            Templates = source.Templates.Select(floor => floor with {
                Trainers = floor.Trainers.Select(trainer => trainer with {
                    Name = names[Key(floor, trainer)], SpeechBefore = Speech(trainer.SpeechBefore),
                    SpeechWin = Speech(trainer.SpeechWin), SpeechLose = Speech(trainer.SpeechLose),
                    SpeechAfter = Speech(trainer.SpeechAfter)
                }).ToImmutableArray()
            }).ToImmutableArray()
        });
    }
    public static TrainerTowerItemsDocument LoadItemsDocument(string root)
    {
        var options = new JsonSerializerOptions(Options);
        SharedItemCatalog.LoadProject(root).AddConverters(options);
        var d = Document<TrainerTowerItemsDocument>(root,"items.json", options);
        var courses = Load(root).Courses;
        if (d.Additions.IsDefaultOrEmpty || d.Prizes.IsDefault || d.Prizes.Length != courses.Length ||
            d.Prizes.Where((p, i) => (int)p.Course != i || p.Item.Value != "pret:" + courses[i].PrizeItem ||
                p.SourceItemId != courses[i].SourcePrizeItemId || p.Quantity != 1).Any())
            throw new InvalidDataException("trainer_tower_items_scope");
        return d;
    }

}
