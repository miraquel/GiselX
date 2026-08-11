using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class StockIndexViewModel
{
    public IReadOnlyList<StockUploadHistoryOptionDto> CompletedUploads { get; set; } = [];
}
