using ClosedXML.Excel;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class StockDownloadControllerTests
{
    [Fact]
    public async Task DownloadBatch_writes_price_unit_after_unit()
    {
        var controller = new StocksController(new StockServiceFake(), null!, null!);

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Unit", sheet.Cell(1, 6).GetString());
        Assert.Equal("Price Unit", sheet.Cell(1, 7).GetString());
        Assert.Equal(12.50m, sheet.Cell(2, 7).GetValue<decimal>());
        Assert.Equal("Saldo Awal", sheet.Cell(1, 8).GetString());
    }

    private sealed class StockServiceFake : IStockService
    {
        public Task<ServiceResponse<IEnumerable<StockDto>>> DownloadBatchAsync(long batchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceResponse<IEnumerable<StockDto>>
            {
                Data =
                [
                    new StockDto
                    {
                        ProductId = "P-1", ProductName = "Product", ProductPackaging = "Box",
                        ProductPcsInCtn = 12, ProductNetto = 1.5m, ProductUnit = "CTN",
                        PriceUnit = 12.50m, SaldoAwal = 2, SaldoMasukPO = 3, SaldoAkhir = 5,
                        BatchNumber = "B-1", ExpiredDate = new DateTime(2027, 1, 1),
                        CompanyId = 7, CreatedDate = new DateTime(2026, 8, 10),
                        StockUploadBatchId = 9, StockDate = new DateTime(2026, 8, 9)
                    }
                ]
            });

        public Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year,
            int month, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IReadOnlyList<StockUploadHistoryOptionDto>>> ListCompletedUploadsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
