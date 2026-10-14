using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Tutorials;

namespace Game.Content;

/// <summary>Authenticated original Teachy TV data. Extra source inventory and asset
/// metadata stay in the resource document and are intentionally not battle rules.</summary>
public static class PretTeachyTv
{
    public const string Profile = "firered-teachy-tv-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string GeneratedSha256 = "6424e38662eb07a4a35b22d37744f95cbdc6a243791f7a1cf3f5230a2a023db3";

    public static TeachyTvCatalog Load(string json)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != GeneratedSha256)
            throw new InvalidDataException("teachy_tv_catalog_hash");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("profile").GetString() != Profile ||
            root.GetProperty("sourceCommit").GetString() != SourceCommit)
            throw new InvalidDataException("teachy_tv_catalog_identity");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter<TeachyTvDemonstrationKind>(allowIntegerValues: false));
        var lessons = root.GetProperty("lessons").Deserialize<ImmutableArray<TeachyTvLesson>>(options);
        var texts = root.GetProperty("texts").Deserialize<ImmutableArray<TeachyTvText>>(options).ToImmutableDictionary(x => x.Symbol);
        var menus = root.GetProperty("menu");
        var full = menus[0].GetProperty("entries").Deserialize<ImmutableArray<TeachyTvMenuEntry>>(options);
        var limited = menus[1].GetProperty("entries").Deserialize<ImmutableArray<TeachyTvMenuEntry>>(options);
        if (lessons.Length != 6 || texts.Count != 42 || full.Length != 7 || limited.Length != 5 ||
            !lessons.Select(x => x.SourceIndex).SequenceEqual(Enumerable.Range(0, 6)) ||
            !full.Select(x => x.SourceIndex).SequenceEqual([0,1,2,3,4,5,-2]) ||
            !limited.Select(x => x.SourceIndex).SequenceEqual([0,1,2,3,-2]))
            throw new InvalidDataException("teachy_tv_catalog_shape");
        foreach (var lesson in lessons)
        {
            if (lesson.RequiresTmCase != (lesson.SourceIndex >= 4) ||
                lesson.Commands.Length != (lesson.SourceIndex < 4 ? 19 : 16) ||
                lesson.ResumeCommandIndex != (lesson.SourceIndex < 4 ? 12 : 9) ||
                lesson.Commands[lesson.ResumeCommandIndex - 1] != "TTVcmd_TaskBattleOrFadeByOptionChosen" ||
                !texts.ContainsKey(lesson.LabelSymbol) || !texts.ContainsKey(lesson.IntroductionText) || !texts.ContainsKey(lesson.ConclusionText) ||
                (lesson.Battle is not null) != (lesson.SourceIndex < 4))
                throw new InvalidDataException("teachy_tv_lesson_contract");
        }
        return new(lessons, full, limited, texts, root.GetProperty("helloText").GetString()!);
    }
}
