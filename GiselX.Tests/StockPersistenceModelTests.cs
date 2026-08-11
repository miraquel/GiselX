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
    public void Model_indexes_stock_audit_and_snapshot_queries()
    {
        using var db = CreateContext();
        var batch = db.Model.FindEntityType(typeof(StockUploadBatch))!;
        var stock = db.Model.FindEntityType(typeof(Stock))!;

        Assert.Contains(batch.GetIndexes(), index => Names(index)
            .SequenceEqual([nameof(StockUploadBatch.CompanyId), nameof(StockUploadBatch.CompletedDate), nameof(StockUploadBatch.Id)]));
        Assert.Contains(batch.GetIndexes(), index => Names(index)
            .SequenceEqual([nameof(StockUploadBatch.CompanyId), nameof(StockUploadBatch.StockDate)]));
        Assert.Contains(stock.GetIndexes(), index => Names(index).SequenceEqual([nameof(Stock.StockUploadBatchId)]));
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

    private static Microsoft.EntityFrameworkCore.Metadata.IForeignKey ForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        string property) => Assert.Single(entity.GetForeignKeys(), key => key.Properties.Single().Name == property);

    private static IEnumerable<string> Names(Microsoft.EntityFrameworkCore.Metadata.IIndex index) =>
        index.Properties.Select(property => property.Name);
}
