using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public CompanyRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken)
    {
        const string query = "Company_Insert";
        
        var parameters = new DynamicParameters();
        parameters.Add("@Name", company.Name);
        parameters.Add("@Address", company.Address);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );

        return await _dbConnection.QuerySingleAsync<Company>(command);
    }

    public async Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string query = "Company_SelectById";
        
        var parameters = new DynamicParameters();
        parameters.Add("@Id", id);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );

        return await _dbConnection.QuerySingleAsync<Company>(command);
    }

    public async Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken)
    {
        const string query = "Company_Select";
        
        var parameters = new DynamicParameters();
        // loop Filters
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => typeof(Company).GetProperty(parameter.Key)?.CanRead is true))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                continue;
            }
            
            // get the data type of the property
            var propertyInfo = typeof(Company).GetProperty(parameter.Key);
            if (propertyInfo == null)
            {
                throw new Exception($"Property {parameter.Key} is not found.");
            }

            switch (propertyInfo.PropertyType)
            {
                case { } t when t == typeof(string):
                    if (parameter.Value.Contains('*') || parameter.Value.Contains('%'))
                    {
                        parameters.Add($"@{parameter.Key}", parameter.Value.Replace('*', '%'));
                    }
                    else
                    {
                        parameters.Add($"@{parameter.Key}", parameter.Value);
                    }
                    break;
                case { } t when t == typeof(int):
                    if (int.TryParse(parameter.Value, out var intValue))
                    {
                        parameters.Add($"@{parameter.Key}", intValue);
                    }
                    break;
                case { } t when t == typeof(decimal):
                    if (decimal.TryParse(parameter.Value, out var decimalValue))
                    {
                        parameters.Add($"@{parameter.Key}", decimalValue);
                    }
                    break;
                case { } t when t == typeof(DateTime):
                    if (DateTime.TryParse(parameter.Value, out var dateTimeValue) || DateTime.TryParseExact(parameter.Value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTimeValue))
                    {
                        parameters.Add($"@{parameter.Key}", dateTimeValue);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Type {propertyInfo.PropertyType} is not supported.");
            }
        }
        
        parameters.Add("@PageNumber", pagedListRequest.PageNumber);
        parameters.Add("@PageSize", pagedListRequest.PageSize);
        parameters.Add("@SortOrder", pagedListRequest.IsSortAscending ? "ASC" : "DESC");
        parameters.Add("@SortColumn", pagedListRequest.SortBy);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure);

        var result = await _dbConnection.QueryMultipleAsync(command);
        var items = result.Read<Company>().ToList();
        var totalCount = result.ReadSingle<int>();
        
        return new PagedList<Company>(items, pagedListRequest.PageNumber, pagedListRequest.PageSize, totalCount);
    }

    public async Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken)
    {
        const string query = "Company_Update";
        
        var parameters = new DynamicParameters();
        parameters.Add("@Id", company.Id);
        parameters.Add("@Name", company.Name);
        parameters.Add("@Address", company.Address);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );

        return await _dbConnection.QuerySingleAsync<Company>(command);
    }
}