using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public class SelloutPersistenceModelTests
{
    private static GiselXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXModelTest;Trusted_Connection=True")
            .Options;
        return new GiselXDbContext(options);
    }

    [Fact]
    public void Model_DefinesMappingBatchStagingAndWeeklySalesIndex()
    {
        using var db = CreateContext();

        Assert.NotNull(db.Model.FindEntityType(typeof(DistributorTemplateMapping)));
        Assert.NotNull(db.Model.FindEntityType(typeof(DistributorTemplateColumn)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SelloutUploadBatch)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SelloutStaging)));

        var sales = db.Model.FindEntityType(typeof(SalesTransaction))!;
        Assert.Contains(sales.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SalesTransaction.CompanyId), nameof(SalesTransaction.ReportingWeekStart)]));
    }

    [Fact]
    public void MappingColumns_AreUniqueBySourceAndCanonicalField()
    {
        using var db = CreateContext();
        var columns = db.Model.FindEntityType(typeof(DistributorTemplateColumn))!;
        var indexes = columns.GetIndexes().Where(index => index.IsUnique).ToList();

        Assert.Contains(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DistributorTemplateColumn.DistributorTemplateMappingId), nameof(DistributorTemplateColumn.SourceColumnNumber)]));
        Assert.Contains(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DistributorTemplateColumn.DistributorTemplateMappingId), nameof(DistributorTemplateColumn.CanonicalField)]));
    }

    [Fact]
    public void CompletedBatchIndex_IsUniqueAndFiltered()
    {
        using var db = CreateContext();
        var batch = db.Model.FindEntityType(typeof(SelloutUploadBatch))!;
        var index = Assert.Single(batch.GetIndexes(), candidate => candidate.IsUnique);

        Assert.Equal("[Status] = 2", index.GetFilter());
        Assert.Equal(
            [nameof(SelloutUploadBatch.CompanyId), nameof(SelloutUploadBatch.ReportingWeekStart)],
            index.Properties.Select(property => property.Name));
    }
}
