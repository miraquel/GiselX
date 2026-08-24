using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GiselX.Tests;

public sealed class PowerBiReportPersistenceModelTests
{
    [Fact]
    public void Model_configures_company_scope_names_url_order_and_audit_contract()
    {
        using var db = new GiselXDbContext(new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXPowerBiModelTest;Trusted_Connection=True")
            .Options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PowerBiReport));

        Assert.NotNull(entity);
        Assert.Equal(150, entity!.FindProperty(nameof(PowerBiReport.Name))!.GetMaxLength());
        Assert.Equal(2048, entity.FindProperty(nameof(PowerBiReport.EmbedUrl))!.GetMaxLength());
        Assert.Equal(256, entity.FindProperty(nameof(PowerBiReport.CreatedBy))!.GetMaxLength());
        Assert.Equal(true, entity.FindProperty(nameof(PowerBiReport.IsActive))!.GetDefaultValue());
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
        Assert.Contains(entity.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_PowerBiReport_DisplayOrder_NonNegative" &&
            constraint.Sql == "[DisplayOrder] >= 0");
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PowerBiReport.CompanyId), nameof(PowerBiReport.Name)]));
    }
}
