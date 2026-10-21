using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using Microsoft.AspNetCore.Http;

namespace GiselX.Tests;

public class DistributorTemplateMappingServiceTests
{
    [Fact]
    public async Task SaveAsync_RejectsMissingRequiredFields()
    {
        var dto = ValidMapping();
        dto.Columns = dto.Columns.Where(column => column.CanonicalField != SelloutCanonicalField.InvoiceUnit).ToList();
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains(nameof(SelloutCanonicalField.InvoiceUnit)));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_AllowsOneSourceColumnForMultipleCanonicalFields()
    {
        var dto = ValidMapping();
        dto.Columns[1].SourceColumnNumber = dto.Columns[0].SourceColumnNumber;
        dto.Columns[1].SourceHeader = dto.Columns[0].SourceHeader;
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Equal(2, mapping.Columns.Count(column =>
            column.SourceColumnNumber == dto.Columns[0].SourceColumnNumber));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateCanonicalFields()
    {
        var dto = ValidMapping();
        dto.Columns[1].CanonicalField = dto.Columns[0].CanonicalField;
        var (service, repository, _) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.Contains(response.Errors, error => error.Contains("canonical field", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
    }

    [Fact]
    public async Task SaveAsync_RejectsFirstDataRowNotAfterHeader()
    {
        var dto = ValidMapping();
        dto.FirstDataRowNumber = dto.HeaderRowNumber;
        var (service, repository, _) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.Contains(response.Errors, error => error.Contains("first data row", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateCompanyMappingName()
    {
        var (service, repository, _) = CreateService();
        repository.Mappings.Add(new DistributorTemplateMapping { Id = 5, CompanyId = 2, Name = " weekly sellout " });

        var response = await service.SaveAsync(ValidMapping(), "admin", CancellationToken.None);

        Assert.Contains(response.Errors, error => error.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
    }

    [Fact]
    public async Task SaveAsync_PersistsValidMappingAndCommits()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(ValidMapping(), "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Equal("admin", mapping.CreatedBy);
        Assert.Equal(8, mapping.Columns.Count);
        Assert.True(transaction.CommitCalled);
        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task SaveAsync_RejectsUndefinedCanonicalField()
    {
        var dto = ValidMapping();
        dto.Columns.Add(new DistributorTemplateColumnDto
        {
            SourceColumnNumber = 9,
            SourceHeader = "Forged field",
            CanonicalField = (SelloutCanonicalField)999
        });
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors,
            error => error.Contains("valid canonical field", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_UpdatesRequestedStateAndAuditThenCommits()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new DistributorTemplateMapping { Id = 7, CompanyId = 2, Name = "Weekly" });

        var response = await service.SetActiveAsync(7, 2, false, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var change = Assert.IsType<ActiveChange>(repository.ActiveChange);
        Assert.Equal(7, change.Id);
        Assert.Equal(2, change.CompanyId);
        Assert.False(change.IsActive);
        Assert.Equal("admin", change.ModifiedBy);
        Assert.InRange(change.ModifiedDate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_RejectsMappingFromAnotherCompanyWithoutCommit()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new DistributorTemplateMapping { Id = 7, CompanyId = 2, Name = "Weekly" });

        var response = await service.SetActiveAsync(7, 99, true, "admin", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(repository.ActiveChange);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task DeleteAsync_DeletesUnusedMappingAndCommits()
    {
        var (service, repository, transaction) = CreateService();
        repository.DeleteResult = DistributorTemplateMappingDeleteResult.Deleted;

        var response = await service.DeleteAsync(7, 2, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal((7, 2), repository.DeletedMapping);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task DeleteAsync_BlocksMappingWithUploadHistoryWithoutCommit()
    {
        var (service, repository, transaction) = CreateService();
        repository.DeleteResult = DistributorTemplateMappingDeleteResult.HasUploadHistory;

        var response = await service.DeleteAsync(7, 2, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors,
            error => error.Contains("upload history", StringComparison.OrdinalIgnoreCase));
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task DeleteAsync_RejectsMappingFromAnotherCompanyWithoutCommit()
    {
        var (service, repository, transaction) = CreateService();
        repository.DeleteResult = DistributorTemplateMappingDeleteResult.NotFound;

        var response = await service.DeleteAsync(7, 99, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.False(transaction.CommitCalled);
    }

    private static (DistributorTemplateMappingService Service, FakeMappingRepository Repository, FakeTransaction Transaction) CreateService()
    {
        var repository = new FakeMappingRepository();
        var transaction = new FakeTransaction();
        return (new DistributorTemplateMappingService(repository, transaction), repository, transaction);
    }

    private static SaveDistributorTemplateMappingDto ValidMapping()
    {
        var fields = new[]
        {
            SelloutCanonicalField.CustomerId,
            SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId,
            SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo,
            SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceQty,
            SelloutCanonicalField.InvoiceUnit
        };

        return new SaveDistributorTemplateMappingDto
        {
            CompanyId = 2,
            Name = "Weekly Sellout",
            WorksheetName = "Data",
            HeaderRowNumber = 3,
            FirstDataRowNumber = 5,
            HeaderSignature = "CUSTOMER ID\u001FCUSTOMER NAME",
            Columns = fields.Select((field, index) => new DistributorTemplateColumnDto
            {
                SourceColumnNumber = index + 1,
                SourceHeader = field.ToString(),
                CanonicalField = field
            }).ToList()
        };
    }

    private sealed class FakeMappingRepository : IDistributorTemplateMappingRepository
    {
        public List<DistributorTemplateMapping> Mappings { get; } = [];
        public List<DistributorTemplateMapping> Inserted { get; } = [];
        public DistributorTemplateMappingDeleteResult DeleteResult { get; set; }
        public (int Id, int CompanyId)? DeletedMapping { get; private set; }
        public ActiveChange? ActiveChange { get; private set; }

        public Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DistributorTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());

        public Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DistributorTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId && mapping.IsActive).ToList());

        public Task<DistributorTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Mappings.SingleOrDefault(mapping => mapping.Id == id && mapping.CompanyId == companyId));

        public Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(42);
        }

        public Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken)
        {
            ActiveChange = new ActiveChange(id, companyId, isActive, modifiedBy, modifiedDate);
            return Task.CompletedTask;
        }

        public Task<DistributorTemplateMappingDeleteResult> DeleteAsync(int id, int companyId,
            CancellationToken cancellationToken)
        {
            DeletedMapping = (id, companyId);
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed record ActiveChange(int Id, int CompanyId, bool IsActive, string ModifiedBy,
        DateTime ModifiedDate);

    private sealed class FakeTransaction : IDbTransaction
    {
        public bool CommitCalled { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCalled = true;
        public void Dispose() { }
        public void Rollback() { }
    }
}
