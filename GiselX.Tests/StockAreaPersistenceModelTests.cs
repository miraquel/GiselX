using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GiselX.Tests;

public sealed class StockAreaPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXStockAreaModelTest;Trusted_Connection=True")
        .Options);

    [Fact]
    public void Stock_area_is_company_owned_with_a_unique_name_per_company()
    {
        using var db = CreateContext();
        var area = db.Model.FindEntityType(typeof(StockArea))!;

        Assert.Equal(DeleteBehavior.Restrict, ForeignKey(area, nameof(StockArea.CompanyId)).DeleteBehavior);
        Assert.Contains(area.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(p => p.Name).SequenceEqual([nameof(StockArea.CompanyId), nameof(StockArea.Name)]));
        Assert.Equal(100, area.FindProperty(nameof(StockArea.Name))!.GetMaxLength());
        var aliases = area.FindProperty(nameof(StockArea.Aliases))!;
        Assert.Equal(1000, aliases.GetMaxLength());
        Assert.True(aliases.IsNullable);
    }

    [Fact]
    public void Stock_and_staging_rows_reference_an_optional_area_without_cascade()
    {
        using var db = CreateContext();

        foreach (var type in new[] { typeof(Stock), typeof(StockStaging) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.True(entity.FindProperty("StockAreaId")!.IsNullable);
            var key = ForeignKey(entity, "StockAreaId");
            Assert.Equal(typeof(StockArea), key.PrincipalEntityType.ClrType);
            Assert.Contains(key.DeleteBehavior, new[] { DeleteBehavior.Restrict, DeleteBehavior.NoAction });
            Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(p => p.Name).SequenceEqual(["StockAreaId"]));
        }
    }

    [Fact]
    public void Condition_is_a_non_null_tinyint_defaulting_to_good()
    {
        using var db = CreateContext();

        foreach (var type in new[] { typeof(Stock), typeof(StockStaging) })
        {
            var property = db.Model.FindEntityType(type)!.FindProperty("Condition")!;
            Assert.False(property.IsNullable);
            Assert.Equal("tinyint", property.GetColumnType());
            Assert.Equal(StockCondition.Good, property.GetDefaultValue());
        }
    }

    [Fact]
    public void Staging_keeps_the_raw_area_and_condition_text()
    {
        using var db = CreateContext();
        var staging = db.Model.FindEntityType(typeof(StockStaging))!;

        foreach (var name in new[] { "SourceAreaText", "SourceConditionText" })
        {
            var property = staging.FindProperty(name)!;
            Assert.Equal(200, property.GetMaxLength());
            Assert.True(property.IsNullable);
        }
    }

    [Fact]
    public void Template_mapping_keeps_good_and_bad_condition_lists()
    {
        using var db = CreateContext();
        var mapping = db.Model.FindEntityType(typeof(StockTemplateMapping))!;

        foreach (var name in new[] { "GoodConditionValues", "BadConditionValues" })
        {
            var property = mapping.FindProperty(name)!;
            Assert.Equal(500, property.GetMaxLength());
            Assert.True(property.IsNullable);
        }
    }

    private static IForeignKey ForeignKey(IEntityType entity, string property) =>
        Assert.Single(entity.GetForeignKeys(), key => key.Properties.Single().Name == property);
}
