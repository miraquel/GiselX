using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using GiselX.Domain.Common;
using GiselX.Repository;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GiselX.Tests;

public sealed class StockRoleAwareIdentityTests
{
    [Theory]
    [InlineData("Distributor", "Distributor")]
    [InlineData("GMK Administrator", "GMK")]
    public async Task Stock_api_select_uses_distributor_identity_only_for_the_exact_distributor_role(string role,
        string expectedIdentity)
    {
        var controller = new StocksController(new RoleAwareStockServiceFake())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test",
                        ClaimTypes.Name, ClaimTypes.Role))
                }
            }
        };

        var result = Assert.IsType<ObjectResult>(await controller.SelectAsync(new PagedListRequestDto(),
            CancellationToken.None));
        var response = Assert.IsType<ServiceResponse<PagedListDto<StockDto>>>(result.Value);

        Assert.Equal(expectedIdentity, response.Message);
    }

    [Fact]
    public async Task SelectAsync_uses_gmk_columns_for_product_filter_search_and_sort_when_not_requested()
    {
        var connection = new RecordingDbConnection();
        var repository = new StockRepository(connection, null!);
        var request = new PagedListRequest
        {
            Filters = new Dictionary<string, string> { ["ProductId"] = "4100039" },
            SearchTerm = "Caramel",
            SortBy = "ProductName",
            IsSortAscending = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, 7, false, CancellationToken.None));

        Assert.Contains("s.[ProductId] LIKE @ProductId", connection.LastCommandText);
        Assert.Contains("s.[ProductId] LIKE @Search OR s.[ProductName] LIKE @Search", connection.LastCommandText);
        Assert.Contains("ORDER BY s.[ProductName] ASC", connection.LastCommandText);
        Assert.DoesNotContain("COALESCE(NULLIF(s.DistributorProductId", connection.LastCommandText);
    }

    [Fact]
    public async Task SelectAsync_uses_distributor_columns_for_product_filter_search_and_sort_when_requested()
    {
        var connection = new RecordingDbConnection();
        var repository = new StockRepository(connection, null!);
        var request = new PagedListRequest
        {
            Filters = new Dictionary<string, string> { ["ProductId"] = "P0003" },
            SearchTerm = "Caramel",
            SortBy = "ProductName",
            IsSortAscending = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, 7, true, CancellationToken.None));

        Assert.Contains("COALESCE(NULLIF(s.DistributorProductId, ''), s.ProductId) AS ProductId", connection.LastCommandText);
        Assert.Contains("COALESCE(NULLIF(s.DistributorProductName, ''), s.ProductName) AS ProductName", connection.LastCommandText);
        Assert.Contains("COALESCE(NULLIF(s.DistributorProductId, ''), s.ProductId) LIKE @ProductId", connection.LastCommandText);
        Assert.Contains("COALESCE(NULLIF(s.DistributorProductId, ''), s.ProductId) LIKE @Search", connection.LastCommandText);
        Assert.Contains("COALESCE(NULLIF(s.DistributorProductName, ''), s.ProductName) LIKE @Search", connection.LastCommandText);
        Assert.Contains("ORDER BY COALESCE(NULLIF(s.DistributorProductName, ''), s.ProductName) ASC", connection.LastCommandText);
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        public string LastCommandText { get; set; } = string.Empty;
        [AllowNull] public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Recording";
        public override string DataSource => "Recording";
        public override string ServerVersion => "1";
        public override ConnectionState State => ConnectionState.Open;
        public override int ConnectionTimeout => 0;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);
    }

    private sealed class RecordingDbCommand(RecordingDbConnection connection) : DbCommand
    {
        [AllowNull] public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new RecordingDbParameterCollection();
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.LastCommandText = CommandText;
            throw new InvalidOperationException("Recorded query execution.");
        }
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            connection.LastCommandText = CommandText;
            return Task.FromException<DbDataReader>(new InvalidOperationException("Recorded query execution."));
        }
    }

    private sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override DataRowVersion SourceVersion { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _values = [];
        public override int Count => _values.Count;
        public override object SyncRoot => ((System.Collections.ICollection)_values).SyncRoot;
        public override int Add(object value) { _values.Add((DbParameter)value); return _values.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_values).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override int IndexOf(object value) => _values.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _values.FindIndex(value => value.ParameterName == parameterName);
        public override void Insert(int index, object value) => _values.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _values.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _values.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) RemoveAt(index);
        }
        protected override DbParameter GetParameter(int index) => _values[index];
        protected override DbParameter GetParameter(string parameterName) => _values[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0) _values.Add(value);
            else _values[index] = value;
        }
    }

    private sealed class RoleAwareStockServiceFake : IStockService
    {
        public Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto pagedListRequest,
            bool useDistributorIdentity, CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceResponse<PagedListDto<StockDto>>
            {
                Message = useDistributorIdentity ? "Distributor" : "GMK"
            });

        public Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year,
            int month, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IReadOnlyList<StockUploadHistoryOptionDto>>> ListCompletedUploadsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<StockDto>>> DownloadBatchAsync(long batchId,
            bool useDistributorIdentity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
