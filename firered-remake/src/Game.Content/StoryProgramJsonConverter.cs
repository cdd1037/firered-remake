using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Events;

namespace Game.Content;

/// <summary>The generated program stores its common path once. Runtime instructions retain
/// their exact original source identity, including symbols crossed by compiled fallthrough.</summary>
internal sealed class StoryProgramJsonConverter : JsonConverter<StoryProgram>
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ProgramData(
        [property: JsonRequired] string Id,
        [property: JsonRequired] string SourcePath,
        [property: JsonRequired] ImmutableArray<InstructionData> Instructions);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record InstructionData(
        [property: JsonRequired] StoryOp Op,
        [property: JsonRequired] string Opcode,
        [property: JsonRequired] ImmutableArray<string> Args,
        [property: JsonRequired] int Line,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceSymbol = null);

    public override StoryProgram Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var data = JsonSerializer.Deserialize<ProgramData>(ref reader, options)
            ?? throw new JsonException("Missing story program.");
        if (string.IsNullOrWhiteSpace(data.Id) || data.SourcePath is null || data.Instructions.IsDefault ||
            (!data.Instructions.IsEmpty && string.IsNullOrWhiteSpace(data.SourcePath)))
            throw new JsonException("Invalid story program metadata.");
        return new(data.Id, data.Instructions.Select(instruction =>
        {
            if (instruction is null || instruction.Line < 1 || string.IsNullOrWhiteSpace(instruction.Opcode) ||
                instruction.Args.IsDefault || instruction.Args.Any(arg => arg is null) ||
                !Enum.IsDefined(instruction.Op) || instruction.SourceSymbol is not null && string.IsNullOrWhiteSpace(instruction.SourceSymbol))
                throw new JsonException("Invalid story instruction metadata.");
            return new StoryInstruction(instruction.Op, instruction.Opcode, instruction.Args,
                new(data.SourcePath, instruction.SourceSymbol ?? data.Id, instruction.Line));
        }).ToImmutableArray());
    }

    public override void Write(Utf8JsonWriter writer, StoryProgram value, JsonSerializerOptions options)
    {
        if (value.Instructions.IsDefault) throw new JsonException("Missing story instructions.");
        var path = value.Instructions.IsEmpty ? "" : value.Instructions[0].Source.Path;
        var instructions = value.Instructions.Select(instruction =>
        {
            if (instruction.Source.Path != path)
                throw new JsonException("Story program instructions must share one source path.");
            return new InstructionData(instruction.Op, instruction.Opcode, instruction.Args, instruction.Source.Line,
                instruction.Source.Symbol == value.Id ? null : instruction.Source.Symbol);
        }).ToImmutableArray();
        JsonSerializer.Serialize(writer, new ProgramData(value.Id, path, instructions), options);
    }
}
