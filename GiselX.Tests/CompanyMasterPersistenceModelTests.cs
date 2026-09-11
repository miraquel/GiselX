using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GiselX.Tests;

public sealed class CompanyMasterPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXCompanyMasterModelTest;Trusted_Connection=True")
        .Options);

    [Theory]
    [InlineData(typeof(Channel), "UX_Channel_Code")]
    [InlineData(typeof(Salesman), "UX_Salesman_Code")]
    public void Model_keeps_master_codes_unique(Type entityType, string indexName)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType)!;

        var index = Assert.Single(entity.GetIndexes(), candidate => candidate.IsUnique);
        Assert.Equal(["Code"], index.Properties.Select(property => property.Name));
        Assert.Equal(indexName, index.GetDatabaseName());
    }

    [Theory]
    [InlineData(typeof(Channel))]
    [InlineData(typeof(Salesman))]
    public void Model_configures_required_master_fields_and_audit_columns(Type entityType)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType)!;

        AssertRequired(entity, "Code", 50);
        AssertRequired(entity, "Name", 200);
        Assert.Equal(256, entity.FindProperty("CreatedBy")!.GetMaxLength());
        Assert.False(entity.FindProperty("CreatedBy")!.IsNullable);
        Assert.Equal(256, entity.FindProperty("ModifiedBy")!.GetMaxLength());
        Assert.True(entity.FindProperty("ModifiedBy")!.IsNullable);
        Assert.Equal("datetime2", entity.FindProperty("CreatedDate")!.GetColumnType());
        Assert.Equal("datetime2", entity.FindProperty("ModifiedDate")!.GetColumnType());
        Assert.True(entity.FindProperty("ModifiedDate")!.IsNullable);
        Assert.Equal(true, entity.FindProperty("IsActive")!.GetDefaultValue());
    }

    [Theory]
    [InlineData("ChannelId", typeof(Channel))]
    [InlineData("SalesmanId", typeof(Salesman))]
    public void Company_references_each_master_optionally_and_restricts_deletion(string foreignKeyName, Type masterType)
    {
        using var db = CreateContext();
        var company = db.Model.FindEntityType(typeof(Company))!;

        Assert.True(company.FindProperty(foreignKeyName)!.IsNullable);
        var foreignKey = Assert.Single(company.GetForeignKeys(),
            candidate => candidate.Properties.Any(property => property.Name == foreignKeyName));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(masterType, foreignKey.PrincipalEntityType.ClrType);
    }

    [Theory]
    [InlineData("ChannelName")]
    [InlineData("SalesmanName")]
    public void Company_display_names_stay_out_of_the_table(string propertyName)
    {
        using var db = CreateContext();

        Assert.Null(db.Model.FindEntityType(typeof(Company))!.FindProperty(propertyName));
    }

    private static void AssertRequired(IEntityType entity, string propertyName, int maxLength)
    {
        var property = entity.FindProperty(propertyName)!;
        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }
}
