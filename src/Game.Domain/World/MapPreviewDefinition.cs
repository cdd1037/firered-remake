namespace Game.Domain.World;

public enum MapPreviewType { Cave, Forest }

/// <summary>Preview eligibility and its entry-duration flag. Forest maps without an entry producer retain a null flag.</summary>
public sealed record MapPreviewDefinition(MapPreviewType Type, string? VisitFlag);
