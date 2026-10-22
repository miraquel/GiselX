namespace GiselX.Service.Dto;

/// <summary>
/// What the Home page tells a distributor: the next deadline, the uploads that count towards it, and
/// what has not arrived yet - the same check the reminder e-mail makes, so the two never disagree.
/// </summary>
public sealed class UploadDeadlineStatusDto
{
    public DateTime Deadline { get; init; }
    /// <summary>First day of the cycle; the cycle runs through <see cref="Deadline"/> inclusive.</summary>
    public DateTime From { get; init; }
    public int DaysLeft { get; init; }
    public IReadOnlyList<string> Missing { get; init; } = [];
}
