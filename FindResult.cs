namespace Jester;

/// <summary>One match from a "Find in Files" search, shown in the results panel.</summary>
public sealed class FindResult
{
    public required string FilePath { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public int Length { get; init; }

    /// <summary>Short "name:line" label shown in the results list.</summary>
    public required string Location { get; init; }

    /// <summary>Trimmed text of the matching line, shown beside the location.</summary>
    public required string Preview { get; init; }
}
