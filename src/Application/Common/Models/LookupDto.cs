namespace backend.Application.Common.Models;

/// <summary>
/// Generic lightweight DTO used for dropdown / lookup lists.
/// </summary>
public class LookupDto
{
    public int Id { get; init; }

    public string? Title { get; init; }
}
