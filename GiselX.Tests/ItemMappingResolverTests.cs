using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class ItemMappingResolverTests
{
    [Fact]
    public void Resolve_matches_trimmed_id_case_insensitively_and_ignores_name_difference()
    {
        var resolver = new ItemMappingResolver(new CompanyRepository(), new ItemMappings());
        var context = EnabledContext(new ItemMappingDto
        {
            Id = 9, DistributorItemId = "P0003", DistributorItemName = "Saved Name",
            GmkItemId = "4100039", GmkItemName = "GMK Name", IsActive = true
        });

        var result = resolver.Resolve(context, " p0003 ", "Uploaded Different Name");

        Assert.True(result.IsMapped);
        Assert.Equal("4100039", result.GmkItemId);
        Assert.Equal("Uploaded Different Name", result.DistributorItemName);
        Assert.Null(result.ErrorReason);
    }

    [Fact]
    public void Resolve_when_mapping_is_disabled_preserves_uploaded_identity_for_both_views()
    {
        var resolver = new ItemMappingResolver(new CompanyRepository(), new ItemMappings());

        var result = resolver.Resolve(new ItemMappingContextDto { IsEnabled = false }, " P-1 ", " Product ");

        Assert.False(result.IsMapped);
        Assert.Equal("P-1", result.DistributorItemId);
        Assert.Equal("Product", result.DistributorItemName);
        Assert.Equal("P-1", result.GmkItemId);
        Assert.Equal("Product", result.GmkItemName);
        Assert.Null(result.ItemMappingId);
        Assert.Null(result.ErrorReason);
    }

    [Fact]
    public void Resolve_reports_missing_active_mapping_when_mapping_is_enabled()
    {
        var resolver = new ItemMappingResolver(new CompanyRepository(), new ItemMappings());

        var result = resolver.Resolve(EnabledContext(), " P-MISSING ", "Product");

        Assert.False(result.IsMapped);
        Assert.Equal("P-MISSING", result.DistributorItemId);
        Assert.Equal("Distributor Item ID \"P-MISSING\" has no active GMK item mapping.", result.ErrorReason);
    }

    [Fact]
    public void Resolve_does_not_match_an_inactive_mapping_in_the_context()
    {
        var resolver = new ItemMappingResolver(new CompanyRepository(), new ItemMappings());
        var context = EnabledContext(new ItemMappingDto
        {
            Id = 2, DistributorItemId = "P-INACTIVE", GmkItemId = "4100039", IsActive = false
        });

        var result = resolver.Resolve(context, "P-INACTIVE", "Product");

        Assert.Equal("Distributor Item ID \"P-INACTIVE\" has no active GMK item mapping.", result.ErrorReason);
    }

    [Fact]
    public async Task LoadAsync_excludes_inactive_mappings_when_company_mapping_is_enabled()
    {
        var mappings = new ItemMappings
        {
            Values = [new ItemMapping { Id = 2, CompanyId = 7, DistributorItemId = "inactive", IsActive = false }]
        };
        var resolver = new ItemMappingResolver(new CompanyRepository { Company = new Company { Id = 7, ItemMappingEnabled = true } }, mappings);

        var context = await resolver.LoadAsync(7, CancellationToken.None);
        var result = resolver.Resolve(context, "inactive", "Product");

        Assert.Equal(1, mappings.ActiveReadCount);
        Assert.Equal("Distributor Item ID \"inactive\" has no active GMK item mapping.", result.ErrorReason);
    }

    private static ItemMappingContextDto EnabledContext(params ItemMappingDto[] mappings) => new()
    {
        IsEnabled = true,
        Mappings = mappings
    };

    private sealed class CompanyRepository : ICompanyRepository
    {
        public Company? Company { get; set; } = new() { Id = 7, ItemMappingEnabled = false };
        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Company);
        public Task<GiselX.Domain.Common.PagedList<Company>> GetCompaniesAsync(GiselX.Domain.Common.PagedListRequest pagedListRequest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ItemMappings : IItemMappingRepository
    {
        public IReadOnlyList<ItemMapping> Values { get; set; } = [];
        public int ActiveReadCount { get; private set; }
        public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken)
        {
            ActiveReadCount++;
            return Task.FromResult<IReadOnlyList<ItemMapping>>(Values.Where(value => value.CompanyId == companyId && value.IsActive).ToList());
        }
        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
