using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using GiselX.Repository;

namespace GiselX.Tests;

public sealed class UnmappedDistributorItemQueryTests
{
    [Fact]
    public async Task Unmapped_query_scopes_both_staging_sources_to_the_company()
    {
        var (repository, connection, transaction) = CreateRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedDistributorItemsAsync(8, cancellation.Token));

        var sql = connection.LastCommandText;
        Assert.Contains("FROM StockStaging staging", sql);
        Assert.Contains("INNER JOIN StockUploadBatch batch ON batch.Id = staging.StockUploadBatchId", sql);
        Assert.Contains("FROM SelloutStaging staging", sql);
        Assert.Contains("INNER JOIN SelloutUploadBatch batch ON batch.Id = staging.SelloutUploadBatchId", sql);
        Assert.Equal(2, CountOccurrences(sql, "WHERE batch.CompanyId = @CompanyId"));
        Assert.Contains("WHERE mapping.CompanyId = @CompanyId", sql);
        Assert.Equal(8, connection.LastParameters["CompanyId"]);
        Assert.Same(transaction, connection.LastTransaction);
        Assert.Equal(cancellation.Token, connection.LastCancellationToken);
    }

    [Fact]
    public async Task Unmapped_query_selects_only_rows_the_resolver_could_not_map()
    {
        var (repository, connection, _) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedDistributorItemsAsync(8, CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Equal(2, CountOccurrences(sql, "staging.ItemMappingId IS NULL"));
        Assert.Equal(2, CountOccurrences(sql, "NULLIF(LTRIM(RTRIM(staging.DistributorProductId))"));
    }

    [Fact]
    public async Task Unmapped_query_excludes_ids_that_already_have_a_mapping_regardless_of_case_or_padding()
    {
        var (repository, connection, _) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedDistributorItemsAsync(8, CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("WHERE NOT EXISTS", sql);
        Assert.Contains("FROM ItemMapping mapping", sql);
        Assert.Contains("UPPER(LTRIM(RTRIM(mapping.DistributorItemId))) = ranked.NormalizedItemId", sql);
    }

    [Fact]
    public async Task Unmapped_query_reports_both_sources_and_the_most_recent_distributor_item_name()
    {
        var (repository, connection, _) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedDistributorItemsAsync(8, CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("CAST(MAX(ranked.FromStock) AS bit) AS SeenInStock", sql);
        Assert.Contains("CAST(MAX(ranked.FromSellout) AS bit) AS SeenInSellout", sql);
        Assert.Contains("MAX(CASE WHEN ranked.RecencyRank = 1 THEN ranked.DistributorItemName END) AS DistributorItemName", sql);
        Assert.Contains("COUNT(*) AS OccurrenceCount", sql);
        Assert.Contains("MIN(ranked.UploadedDate) AS FirstSeenDate", sql);
        Assert.Contains("MAX(ranked.UploadedDate) AS LastSeenDate", sql);
        Assert.Contains("GROUP BY ranked.NormalizedItemId", sql);
    }

    [Fact]
    public async Task Gmk_suggestion_query_reads_only_active_mappings_across_companies()
    {
        var (repository, connection, transaction) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetGmkItemSuggestionsAsync(CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("FROM ItemMapping", sql);
        Assert.Contains("WHERE IsActive = 1", sql);
        Assert.Contains("GROUP BY LTRIM(RTRIM(GmkItemId))", sql);
        Assert.DoesNotContain("@CompanyId", sql);
        Assert.Same(transaction, connection.LastTransaction);
    }

    [Fact]
    public async Task Unmapped_summary_query_only_covers_companies_with_item_mapping_enabled()
    {
        var (repository, connection, transaction) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedSummaryByCompanyAsync(CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("company.ItemMappingEnabled = 1", sql);
        Assert.DoesNotContain("@CompanyId", sql);
        Assert.Same(transaction, connection.LastTransaction);
    }

    [Fact]
    public async Task Unmapped_summary_query_matches_existing_mappings_within_the_same_company()
    {
        var (repository, connection, _) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedSummaryByCompanyAsync(CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("WHERE NOT EXISTS", sql);
        Assert.Contains("mapping.CompanyId = discovered.CompanyId", sql);
        Assert.Contains("UPPER(LTRIM(RTRIM(mapping.DistributorItemId))) = discovered.NormalizedItemId", sql);
    }

    [Fact]
    public async Task Unmapped_summary_query_reads_both_staging_sources_with_the_unmapped_filter()
    {
        var (repository, connection, _) = CreateRepository();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            repository.GetUnmappedSummaryByCompanyAsync(CancellationToken.None));

        var sql = connection.LastCommandText;
        Assert.Contains("FROM StockStaging staging", sql);
        Assert.Contains("FROM SelloutStaging staging", sql);
        Assert.Equal(2, CountOccurrences(sql, "staging.ItemMappingId IS NULL"));
        Assert.Equal(2, CountOccurrences(sql, "NULLIF(LTRIM(RTRIM(staging.DistributorProductId)), '') IS NOT NULL"));
        Assert.Contains("COUNT(DISTINCT discovered.NormalizedItemId) AS UnmappedItemCount", sql);
    }

    private static int CountOccurrences(string value, string term)
    {
        var count = 0;
        var index = value.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = value.IndexOf(term, index + term.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static (ItemMappingRepository Repository, RecordingDbConnection Connection,
        RecordingDbTransaction Transaction) CreateRepository()
    {
        var connection = new RecordingDbConnection();
        var transaction = new RecordingDbTransaction(connection);
        return (new ItemMappingRepository(connection, transaction), connection, transaction);
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;
        public Dictionary<string, object?> LastParameters { get; private set; } = [];
        public DbTransaction? LastTransaction { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
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

        public void Record(DbCommand command, CancellationToken cancellationToken)
        {
            LastCommandText = command.CommandText;
            LastParameters = command.Parameters.Cast<DbParameter>()
                .ToDictionary(parameter => parameter.ParameterName,
                    parameter => parameter.Value is DBNull ? null : parameter.Value);
            LastTransaction = command.Transaction;
            LastCancellationToken = cancellationToken;
        }
    }

    private sealed class RecordingDbTransaction(DbConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
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
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.Record(this, CancellationToken.None);
            throw new NotSupportedException();
        }
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            connection.Record(this, cancellationToken);
            throw new NotSupportedException();
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
        private readonly ArrayList _values = [];
        public override int Count => _values.Count;
        public override object SyncRoot => ((ICollection)_values).SyncRoot;
        public override int Add(object value) => _values.Add(value);
        public override void AddRange(Array values) => _values.AddRange(values);
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains(value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => _values.CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override int IndexOf(object value) => _values.IndexOf(value);
        public override int IndexOf(string parameterName)
        {
            for (var index = 0; index < _values.Count; index++)
            {
                if (((DbParameter)_values[index]!).ParameterName == parameterName) return index;
            }

            return -1;
        }
        public override void Insert(int index, object value) => _values.Insert(index, value);
        public override void Remove(object value) => _values.Remove(value);
        public override void RemoveAt(int index) => _values.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => (DbParameter)_values[index]!;
        protected override DbParameter GetParameter(string parameterName) => GetParameter(IndexOf(parameterName));
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) =>
            SetParameter(IndexOf(parameterName), value);
    }
}
