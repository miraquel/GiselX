using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public sealed class StockPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXStockModelTest;Trusted_Connection=True")
        .Options);

    [Fact]
    public void Model_defines_immutable_stock_batch_relationships()
    {
        using var db = CreateContext();
        var batch = db.Model.FindEntityType(typeof(StockUploadBatch))!;
        var staging = db.Model.FindEntityType(typeof(StockStaging))!;
        var stock = db.Model.FindEntityType(typeof(Stock))!;

        Assert.Equal(DeleteBehavior.Restrict, ForeignKey(batch, nameof(StockUploadBatch.CompanyId)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, ForeignKey(batch, nameof(StockUploadBatch.StockTemplateMappingId)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, ForeignKey(staging, nameof(StockStaging.StockUploadBatchId)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, ForeignKey(stock, nameof(Stock.StockUploadBatchId)).DeleteBehavior);
        Assert.True(stock.FindProperty(nameof(Stock.StockUploadBatchId))!.IsNullable);
        Assert.True(stock.FindProperty(nameof(Stock.StockDate))!.IsNullable);
    }

    [Fact]
    public void Model_maps_nullable_sheet_source_on_stock_staging()
    {
        using var db = CreateContext();
        var staging = db.Model.FindEntityType(typeof(StockStaging))!;
        var mappingProperty = staging.FindProperty(nameof(StockStaging.StockTemplateMappingId))!;
        var worksheetProperty = staging.FindProperty(nameof(StockStaging.SourceWorksheetName))!;

        Assert.True(mappingProperty.IsNullable);
        Assert.True(worksheetProperty.IsNullable);
        Assert.Equal(255, worksheetProperty.GetMaxLength());
        Assert.Equal(DeleteBehavior.Restrict,
            ForeignKey(staging, nameof(StockStaging.StockTemplateMappingId)).DeleteBehavior);
    }

    [Fact]
    public void Model_indexes_stock_audit_and_snapshot_queries()
    {
        using var db = CreateContext();
        var batch = db.Model.FindEntityType(typeof(StockUploadBatch))!;
        var stock = db.Model.FindEntityType(typeof(Stock))!;

        Assert.Contains(batch.GetIndexes(), index => Names(index)
            .SequenceEqual([nameof(StockUploadBatch.CompanyId), nameof(StockUploadBatch.CompletedDate), nameof(StockUploadBatch.Id)]));
        Assert.Contains(batch.GetIndexes(), index => Names(index)
            .SequenceEqual([nameof(StockUploadBatch.CompanyId), nameof(StockUploadBatch.StockDate)]));
        // Every Stock reader enumerates the distinct (batch, area) pairs, and publication looks up
        // the area of a batch's rows, so the batch index leads into the area.
        Assert.Contains(stock.GetIndexes(), index => Names(index)
            .SequenceEqual([nameof(Stock.StockUploadBatchId), nameof(Stock.StockAreaId)]));
        Assert.DoesNotContain(stock.GetIndexes(), index => Names(index).SequenceEqual([nameof(Stock.StockUploadBatchId)]));
    }

    [Fact]
    public void Model_maps_nullable_price_unit_on_final_and_staging_stock()
    {
        using var db = CreateContext();
        var stockProperty = db.Model.FindEntityType(typeof(Stock))!
            .FindProperty(nameof(Stock.PriceUnit))!;
        var stagingProperty = db.Model.FindEntityType(typeof(StockStaging))!
            .FindProperty(nameof(StockStaging.PriceUnit))!;

        Assert.True(stockProperty.IsNullable);
        Assert.True(stagingProperty.IsNullable);
        Assert.Equal("decimal(18,2)", stockProperty.GetColumnType()!.Replace(" ", string.Empty));
        Assert.Equal("decimal(18,2)", stagingProperty.GetColumnType()!.Replace(" ", string.Empty));
    }

    [Fact]
    public void Model_maps_nullable_smallest_unit_fields_on_final_and_staging_stock()
    {
        using var db = CreateContext();
        var stock = db.Model.FindEntityType(typeof(Stock))!;
        var staging = db.Model.FindEntityType(typeof(StockStaging))!;

        foreach (var entity in new[] { stock, staging })
        {
            var unit = entity.FindProperty(nameof(Stock.SmallestUnit))!;
            var qty = entity.FindProperty(nameof(Stock.SmallestUnitQty))!;

            Assert.True(unit.IsNullable);
            Assert.Equal(50, unit.GetMaxLength());
            Assert.True(qty.IsNullable);
            Assert.Equal("decimal(18,4)", qty.GetColumnType()!.Replace(" ", string.Empty));
        }
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IForeignKey ForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        string property) => Assert.Single(entity.GetForeignKeys(), key => key.Properties.Single().Name == property);

    private static IEnumerable<string> Names(Microsoft.EntityFrameworkCore.Metadata.IIndex index) =>
        index.Properties.Select(property => property.Name);
}
