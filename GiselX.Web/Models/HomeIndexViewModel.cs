using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class HomeIndexViewModel
{
    /// <summary>Null for a company without a deadline schedule; the page then shows no status card.</summary>
    public UploadDeadlineStatusDto? DeadlineStatus { get; init; }
}
