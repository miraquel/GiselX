using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using GiselX.Domain.Common;
using GiselX.Repository;
using Microsoft.Data.SqlClient;

namespace GiselX.Tests;

public sealed class SalesRoleAwareIdentityTests
{
    [Fact]
    public async Task SelectAsync_passes_distributor_identity_mode_to_sql()
    {
        var connection = new RecordingDbConnection();
        var repository = new SalesTransactionRepository(connection, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(Request(), 7, true, CancellationToken.None));

        Assert.True(connection.Parameters.Get<bool>("UseDistributorIdentity"));
    }

    [Fact]
    public async Task SelectAsync_does_not_pass_internal_identity_fields_to_the_stored_procedure()
    {
        var connection = new RecordingDbConnection();
        var repository = new SalesTransactionRepository(connection, null!);
        var request = Request();
        request.Filters["DistributorProductId"] = "P0003";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, 7, true, CancellationToken.None));

        Assert.False(connection.Parameters.Has("DistributorProductId"));
    }

    [Fact]
    public async Task SelectAsync_passes_global_search_for_the_role_aware_product_pair()
    {
        var connection = new RecordingDbConnection();
        var repository = new SalesTransactionRepository(connection, null!);
        var request = Request();
        request.SearchTerm = "Caramel";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, 7, true, CancellationToken.None));

        Assert.Equal("%Caramel%", connection.Parameters.Get<string>("SearchTerm"));
    }

    [Fact]
    public void Select_procedure_projects_filters_and_sorts_with_the_requested_identity()
    {
        var procedure = File.ReadAllText(FindFromSolutionRoot("GiselX.Web", "StoredProcedures", "GiselSales_Select.sql"));

        Assert.Contains("@UseDistributorIdentity", procedure);
        Assert.Contains("CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END AS [ProductId]", procedure);
        Assert.Contains("CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END AS [ProductName]", procedure);
        Assert.Contains("@ProductId         IS NULL OR CASE WHEN @UseDistributorIdentity=1", procedure);
        Assert.Contains("@ProductName       IS NULL OR CASE WHEN @UseDistributorIdentity=1", procedure);
        Assert.Contains("@SearchTerm", procedure);
        Assert.Contains("@SortColumn IN ('productId', 'ProductId')", procedure);
        Assert.Contains("@SortColumn IN ('productName', 'ProductName')", procedure);
    }

    [Fact]
    public void Period_download_procedure_projects_the_same_role_aware_identity()
    {
        var procedure = File.ReadAllText(FindFromSolutionRoot("GiselX.Web", "StoredProcedures", "GiselSales_SelectByCustPeriod.sql"));

        Assert.Contains("@UseDistributorIdentity", procedure);
        Assert.Contains("CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductId],''),[ProductId]) ELSE [ProductId] END AS [ProductId]", procedure);
        Assert.Contains("CASE WHEN @UseDistributorIdentity=1 THEN COALESCE(NULLIF([DistributorProductName],''),[ProductName]) ELSE [ProductName] END AS [ProductName]", procedure);
        Assert.Contains("[CompanyId] = @CompanyId", procedure);
        Assert.Contains("YEAR([InvoiceDate]) = @Year", procedure);
        Assert.Contains("MONTH([InvoiceDate]) = @Month", procedure);
    }

    [Fact]
    public async Task Select_procedure_returns_the_requested_identity_against_localdb()
    {
        var databaseName = $"GiselXSalesIdentity_{Guid.NewGuid():N}";
        var masterConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;TrustServerCertificate=true";
        try
        {
            await using var master = new SqlConnection(masterConnectionString);
            try
            {
                await master.OpenAsync();
            }
            catch (SqlException)
            {
                return;
            }

            await using (var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master))
                await create.ExecuteNonQueryAsync();

            var databaseConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog={databaseName};TrustServerCertificate=true";
            await using var database = new SqlConnection(databaseConnectionString);
            await database.OpenAsync();
            await InstallSchemaAndProcedureAsync(database);
            await InsertDistinctIdentityRowAsync(database);

            var gmk = await SelectIdentityAsync(database, false);
            var distributor = await SelectIdentityAsync(database, true);

            Assert.Equal(("4100039", "GMK Product"), gmk);
            Assert.Equal(("P0003", "Distributor Product"), distributor);
        }
        finally
        {
            await using var master = new SqlConnection(masterConnectionString);
            try
            {
                await master.OpenAsync();
                await using var drop = new SqlCommand($"""
                    IF DB_ID(N'{databaseName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{databaseName}];
                    END
                    """, master);
                await drop.ExecuteNonQueryAsync();
            }
            catch (SqlException)
            {
                // A failed LocalDB setup has no database to remove.
            }
        }
    }

    private static PagedListRequest Request() => new()
    {
        PageNumber = 1,
        PageSize = 10,
        SortBy = "ProductName",
        IsSortAscending = true
    };

    private static async Task InstallSchemaAndProcedureAsync(SqlConnection connection)
    {
        await using var schema = new SqlCommand("""
            CREATE TABLE dbo.SalesTransaction (
                Id INT IDENTITY PRIMARY KEY, CustomerId NVARCHAR(100) NOT NULL, CustomerName NVARCHAR(200) NOT NULL,
                CustomerAlias NVARCHAR(200) NOT NULL, CustomerAddress NVARCHAR(500) NOT NULL,
                ProductId NVARCHAR(100) NOT NULL, ProductBrand NVARCHAR(200) NOT NULL, ProductName NVARCHAR(200) NOT NULL,
                DistributorProductId NVARCHAR(100) NULL, DistributorProductName NVARCHAR(200) NULL,
                ProductPackaging NVARCHAR(100) NOT NULL, ProductPcsInCtn INT NOT NULL, BatchNumber NVARCHAR(100) NOT NULL,
                ExpiredDate DATETIME NOT NULL, InvoiceNo NVARCHAR(100) NOT NULL, InvoiceDate DATETIME NOT NULL,
                InvoiceQty DECIMAL(18,2) NOT NULL, InvoiceUnit NVARCHAR(50) NOT NULL, UnitPrice DECIMAL(18,2) NOT NULL,
                GrossValue DECIMAL(18,2) NOT NULL, DiscountValue DECIMAL(18,2) NOT NULL, DiscountPct DECIMAL(18,2) NOT NULL,
                NetValue DECIMAL(18,2) NOT NULL, InKg DECIMAL(18,2) NOT NULL, GeisaPOId NVARCHAR(100) NOT NULL,
                ShipDate DATETIME NOT NULL, SalesmanNameGMK NVARCHAR(200) NOT NULL, CompanyId INT NOT NULL,
                CreatedDate DATETIME NOT NULL);
            """, connection);
        await schema.ExecuteNonQueryAsync();
        await using var procedure = new SqlCommand(File.ReadAllText(
            FindFromSolutionRoot("GiselX.Web", "StoredProcedures", "GiselSales_Select.sql")), connection);
        await procedure.ExecuteNonQueryAsync();
    }

    private static async Task InsertDistinctIdentityRowAsync(SqlConnection connection)
    {
        await using var insert = new SqlCommand("""
            INSERT INTO dbo.SalesTransaction (CustomerId,CustomerName,CustomerAlias,CustomerAddress,ProductId,ProductBrand,
                ProductName,DistributorProductId,DistributorProductName,ProductPackaging,ProductPcsInCtn,BatchNumber,
                ExpiredDate,InvoiceNo,InvoiceDate,InvoiceQty,InvoiceUnit,UnitPrice,GrossValue,DiscountValue,DiscountPct,
                NetValue,InKg,GeisaPOId,ShipDate,SalesmanNameGMK,CompanyId,CreatedDate)
            VALUES ('C1','Customer','','','4100039','','GMK Product','P0003','Distributor Product','',0,'',
                '2026-01-01','INV-1','2026-08-01',1,'',0,0,0,0,0,0,'','2026-08-01','',7,'2026-08-11');
            """, connection);
        await insert.ExecuteNonQueryAsync();
    }

    private static async Task<(string ProductId, string ProductName)> SelectIdentityAsync(SqlConnection connection,
        bool useDistributorIdentity)
    {
        await using var command = new SqlCommand("""
            EXEC dbo.GiselSales_Select @CompanyId = @CompanyId, @PageNumber = 1, @PageSize = 10,
                @UseDistributorIdentity = @UseDistributorIdentity;
            """, connection);
        command.Parameters.AddWithValue("@CompanyId", 7);
        command.Parameters.AddWithValue("@UseDistributorIdentity", useDistributorIdentity);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(reader.GetOrdinal("ProductId")), reader.GetString(reader.GetOrdinal("ProductName")));
    }

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        public RecordingDbParameterCollection Parameters { get; } = new();
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
        protected override DbParameterCollection DbParameterCollection => connection.Parameters;
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new InvalidOperationException("Recorded command execution.");
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
            CancellationToken cancellationToken) => Task.FromException<DbDataReader>(
            new InvalidOperationException("Recorded command execution."));
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
        public T Get<T>(string name) => (T)_values.Single(parameter =>
            string.Equals(parameter.ParameterName.TrimStart('@'), name, StringComparison.OrdinalIgnoreCase)).Value!;
        public bool Has(string name) => _values.Any(parameter =>
            string.Equals(parameter.ParameterName.TrimStart('@'), name, StringComparison.OrdinalIgnoreCase));
        public override int Add(object value) { _values.Add((DbParameter)value); return _values.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_values).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override int IndexOf(object value) => _values.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _values.FindIndex(parameter => parameter.ParameterName == parameterName);
        public override void Insert(int index, object value) => _values.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _values.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _values.RemoveAt(index);
        public override void RemoveAt(string parameterName) { var index = IndexOf(parameterName); if (index >= 0) RemoveAt(index); }
        protected override DbParameter GetParameter(int index) => _values[index];
        protected override DbParameter GetParameter(string parameterName) => _values[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        { var index = IndexOf(parameterName); if (index < 0) _values.Add(value); else _values[index] = value; }
    }
}
