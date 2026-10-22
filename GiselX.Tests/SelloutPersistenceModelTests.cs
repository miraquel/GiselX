using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

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
    public void Sales_are_indexed_by_company_and_invoice_date_covering_the_batch()
    {
        using var db = CreateContext();
        // Include columns are stored only in the design-time model.
        var model = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IDesignTimeModel>().Model;
        var sales = model.FindEntityType(typeof(SalesTransaction))!;

        var index = Assert.Single(sales.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SalesTransaction.CompanyId), nameof(SalesTransaction.InvoiceDate)]));

        Assert.False(index.IsUnique);
        Assert.Equal([nameof(SalesTransaction.SelloutUploadBatchId)], index.GetIncludeProperties());
    }

    [Fact]
    public void Batches_of_one_week_are_indexed_but_not_unique()
    {
        using var db = CreateContext();
        var batches = db.Model.FindEntityType(typeof(SelloutUploadBatch))!;

        var index = Assert.Single(batches.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SelloutUploadBatch.CompanyId), nameof(SelloutUploadBatch.ReportingWeekStart)]));

        Assert.False(index.IsUnique);
        Assert.Null(index.GetFilter());
    }

    [Fact]
    public void MappingColumns_AllowSourceReuseButRemainUniqueByCanonicalField()
    {
        using var db = CreateContext();
        var columns = db.Model.FindEntityType(typeof(DistributorTemplateColumn))!;
        var indexes = columns.GetIndexes().ToList();

        var sourceIndex = Assert.Single(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DistributorTemplateColumn.DistributorTemplateMappingId), nameof(DistributorTemplateColumn.SourceColumnNumber)]));
        var canonicalIndex = Assert.Single(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(DistributorTemplateColumn.DistributorTemplateMappingId), nameof(DistributorTemplateColumn.CanonicalField)]));

        Assert.False(sourceIndex.IsUnique);
        Assert.True(canonicalIndex.IsUnique);
    }

    [Fact]
    public void StockMappingColumns_AllowSourceReuseButRemainUniqueByCanonicalField()
    {
        using var db = CreateContext();
        var columns = db.Model.FindEntityType(typeof(StockTemplateColumn))!;
        var indexes = columns.GetIndexes().ToList();

        var sourceIndex = Assert.Single(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(StockTemplateColumn.StockTemplateMappingId), nameof(StockTemplateColumn.SourceColumnNumber)]));
        var canonicalIndex = Assert.Single(indexes, index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(StockTemplateColumn.StockTemplateMappingId), nameof(StockTemplateColumn.CanonicalField)]));

        Assert.False(sourceIndex.IsUnique);
        Assert.True(canonicalIndex.IsUnique);
    }

    [Fact]
    public void Model_defines_upload_group_relationships_json_and_audit_index()
    {
        using var db = CreateContext();
        var group = db.Model.FindEntityType(typeof(SelloutUploadGroup));

        Assert.NotNull(group);
        Assert.Equal("nvarchar(max)", group.FindProperty(nameof(SelloutUploadGroup.IgnoredSummaryRowsJson))!.GetColumnType());
        Assert.Equal("nvarchar(max)", group.FindProperty(nameof(SelloutUploadGroup.ValidationErrorsJson))!.GetColumnType());
        Assert.Contains(group.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(SelloutUploadGroup.CompanyId), nameof(SelloutUploadGroup.UploadedDate)]));

        var companyForeignKey = Assert.Single(group.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Single().Name == nameof(SelloutUploadGroup.CompanyId));
        Assert.Equal(DeleteBehavior.Restrict, companyForeignKey.DeleteBehavior);

        var mappingForeignKey = Assert.Single(group.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Single().Name == nameof(SelloutUploadGroup.DistributorTemplateMappingId));
        Assert.False(mappingForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, mappingForeignKey.DeleteBehavior);

        var batch = db.Model.FindEntityType(typeof(SelloutUploadBatch))!;
        var groupForeignKey = Assert.Single(batch.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Single().Name == nameof(SelloutUploadBatch.SelloutUploadGroupId));
        Assert.False(groupForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, groupForeignKey.DeleteBehavior);
    }
}
