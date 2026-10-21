using ClosedXML.Excel;
using System.Security.Claims;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class StockDownloadControllerTests
{
    [Fact]
    public async Task DownloadBatch_writes_smallest_unit_then_price_unit_after_unit()
    {
        var controller = CreateController();

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Unit", sheet.Cell(1, 8).GetString());
        Assert.Equal("Smallest Unit", sheet.Cell(1, 9).GetString());
        Assert.Equal("PCS", sheet.Cell(2, 9).GetString());
        Assert.Equal("Qty per Smallest Unit", sheet.Cell(1, 10).GetString());
        Assert.Equal(24m, sheet.Cell(2, 10).GetValue<decimal>());
        Assert.Equal("Price Unit", sheet.Cell(1, 11).GetString());
        Assert.Equal(12.50m, sheet.Cell(2, 11).GetValue<decimal>());
        Assert.Equal("Saldo Awal", sheet.Cell(1, 12).GetString());
    }

    [Fact]
    public async Task DownloadBatch_writes_area_and_condition_after_product_name()
    {
        var controller = CreateController();

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Product Name", sheet.Cell(1, 2).GetString());
        Assert.Equal("Area", sheet.Cell(1, 3).GetString());
        Assert.Equal("Condition", sheet.Cell(1, 4).GetString());
        Assert.Equal("Good", sheet.Cell(2, 4).GetString());
        Assert.Equal("Packaging", sheet.Cell(1, 5).GetString());
    }

    [Fact]
    public async Task DownloadBatch_keeps_the_trailing_date_columns_after_the_new_unit_columns()
    {
        var controller = CreateController();

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Batch No", sheet.Cell(1, 15).GetString());
        Assert.Equal("Expired Date", sheet.Cell(1, 16).GetString());
        Assert.Equal("2027-01-01", sheet.Cell(2, 16).GetString());
        Assert.Equal("Stock Date", sheet.Cell(1, 17).GetString());
        Assert.Equal("2026-08-09", sheet.Cell(2, 17).GetString());
    }

    [Theory]
    [InlineData("Distributor", "P-DIST", "Distributor Product")]
    [InlineData("GMK Administrator", "4100039", "GMK Product")]
    public async Task DownloadBatch_writes_identity_for_the_callers_exact_role(string role, string productId,
        string productName)
    {
        var controller = CreateController(role);

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal(productId, sheet.Cell(2, 1).GetString());
        Assert.Equal(productName, sheet.Cell(2, 2).GetString());
    }

    [Theory]
    [InlineData("Administrator", "Distributor", "Test Distributor")]
    [InlineData("Distributor", "Product ID", "P-DIST")]
    public async Task DownloadBatch_adds_distributor_column_only_for_administrator(string role,
        string expectedHeader, string expectedValue)
    {
        var controller = CreateController(role);

        var result = Assert.IsType<FileStreamResult>(
            await controller.DownloadBatch(9, CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal(expectedHeader, sheet.Cell(1, 1).GetString());
        Assert.Equal(expectedValue, sheet.Cell(2, 1).GetString());
    }

    private static StocksController CreateController(string? role = null) => new(new StockServiceFake(), null!, null!)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = role is null
                    ? new ClaimsPrincipal(new ClaimsIdentity())
                    : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test",
                        ClaimTypes.Name, ClaimTypes.Role))
            }
        }
    };

    private sealed class StockServiceFake : IStockService
    {
        public Task<ServiceResponse<IEnumerable<StockDto>>> DownloadBatchAsync(long batchId,
            bool useDistributorIdentity, CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceResponse<IEnumerable<StockDto>>
            {
                Data =
                [
                    new StockDto
                    {
                        ProductId = useDistributorIdentity ? "P-DIST" : "4100039",
                        ProductName = useDistributorIdentity ? "Distributor Product" : "GMK Product",
                        ProductPackaging = "Box",
                        ProductPcsInCtn = 12, ProductNetto = 1.5m, ProductUnit = "CTN",
                        SmallestUnit = "PCS", SmallestUnitQty = 24m,
                        PriceUnit = 12.50m, SaldoAwal = 2, SaldoMasukPO = 3, SaldoAkhir = 5,
                        BatchNumber = "B-1", ExpiredDate = new DateTime(2027, 1, 1),
                        CompanyId = 7, CreatedDate = new DateTime(2026, 8, 10),
                        StockUploadBatchId = 9, StockDate = new DateTime(2026, 8, 9),
                        CompanyName = "Test Distributor"
                    }
                ]
            });

        public Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto request,
            bool useDistributorIdentity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year,
            int month, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IReadOnlyList<StockUploadHistoryOptionDto>>> ListCompletedUploadsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
