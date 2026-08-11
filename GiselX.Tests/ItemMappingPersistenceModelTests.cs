using GiselX.Domain;
using GiselX.Web.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

public sealed class ItemMappingPersistenceModelTests
{
    private static GiselXDbContext CreateContext() => new(new DbContextOptionsBuilder<GiselXDbContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXItemMappingModelTest;Trusted_Connection=True")
        .Options);

    [Fact]
    public void Model_requires_company_scoped_unique_distributor_item_id()
    {
        using var db = CreateContext();
        var mapping = db.Model.FindEntityType(typeof(ItemMapping))!;

        var index = Assert.Single(mapping.GetIndexes(), candidate => candidate.IsUnique);
        Assert.Equal(
            [nameof(ItemMapping.CompanyId), nameof(ItemMapping.DistributorItemId)],
            index.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, ForeignKey(mapping, nameof(ItemMapping.CompanyId)).DeleteBehavior);
    }

    [Fact]
    public void Model_configures_required_item_identity_fields_audit_and_company_switch()
    {
        using var db = CreateContext();
        var mapping = db.Model.FindEntityType(typeof(ItemMapping))!;
        var company = db.Model.FindEntityType(typeof(Company))!;

        AssertIdentity(mapping, nameof(ItemMapping.GmkItemId), 100);
        AssertIdentity(mapping, nameof(ItemMapping.GmkItemName), 200);
        AssertIdentity(mapping, nameof(ItemMapping.DistributorItemId), 100);
        AssertIdentity(mapping, nameof(ItemMapping.DistributorItemName), 200);
        Assert.Equal(256, mapping.FindProperty(nameof(ItemMapping.CreatedBy))!.GetMaxLength());
        Assert.Equal(256, mapping.FindProperty(nameof(ItemMapping.ModifiedBy))!.GetMaxLength());
        Assert.Equal(false, company.FindProperty(nameof(Company.ItemMappingEnabled))!.GetDefaultValue());
    }

    [Fact]
    public void Model_persists_nullable_dual_identity_and_mapping_references()
    {
        using var db = CreateContext();

        foreach (var type in new[] { typeof(Stock), typeof(StockStaging), typeof(SalesTransaction), typeof(SelloutStaging) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.True(entity.FindProperty("DistributorProductId")!.IsNullable);
            Assert.True(entity.FindProperty("DistributorProductName")!.IsNullable);
            Assert.True(entity.FindProperty("ItemMappingId")!.IsNullable);
        }

        Assert.Equal(DeleteBehavior.Restrict,
            ForeignKey(db.Model.FindEntityType(typeof(Stock))!, nameof(Stock.ItemMappingId)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict,
            ForeignKey(db.Model.FindEntityType(typeof(SalesTransaction))!, nameof(SalesTransaction.ItemMappingId)).DeleteBehavior);
    }

    [Fact]
    public void Model_preserves_the_historical_128_character_identity_key_contract()
    {
        using var db = CreateContext();

        var login = db.Model.FindEntityType(typeof(IdentityUserLogin<string>))!;
        var token = db.Model.FindEntityType(typeof(IdentityUserToken<string>))!;

        Assert.Equal(128, login.FindProperty(nameof(IdentityUserLogin<string>.LoginProvider))!.GetMaxLength());
        Assert.Equal(128, login.FindProperty(nameof(IdentityUserLogin<string>.ProviderKey))!.GetMaxLength());
        Assert.Equal(128, token.FindProperty(nameof(IdentityUserToken<string>.LoginProvider))!.GetMaxLength());
        Assert.Equal(128, token.FindProperty(nameof(IdentityUserToken<string>.Name))!.GetMaxLength());
    }

    private static void AssertIdentity(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType mapping,
        string propertyName,
        int maxLength)
    {
        var property = mapping.FindProperty(propertyName)!;
        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IForeignKey ForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        string property) => Assert.Single(entity.GetForeignKeys(), key => key.Properties.Single().Name == property);
}
