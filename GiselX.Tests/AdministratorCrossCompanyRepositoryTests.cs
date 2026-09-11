using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using GiselX.Domain.Common;
using GiselX.Repository;

namespace GiselX.Tests;

public sealed class AdministratorCrossCompanyRepositoryTests
{
    [Fact]
    public async Task Stock_select_uses_each_distributors_latest_batch_for_administrator_scope()
    {
        var connection = new RecordingDbConnection();
        var repository = new StockRepository(connection, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(new PagedListRequest(), 0, false, CancellationToken.None));

        Assert.Equal(CommandType.Text, connection.LastCommandType);
        Assert.Contains("PARTITION BY CompanyId", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN Company", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyName", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sellout_select_uses_each_distributors_latest_upload_for_administrator_scope()
    {
        var connection = new RecordingDbConnection();
        var repository = new SalesTransactionRepository(connection, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(new PagedListRequest(), 0, false, CancellationToken.None));

        Assert.Equal(CommandType.Text, connection.LastCommandType);
        Assert.Contains("PARTITION BY CompanyId", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN Company", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyName", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_level_select_reads_every_uploader_for_administrator_scope()
    {
        var connection = new RecordingDbConnection();
        var repository = new ServiceLevelRepository(connection, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(new PagedListRequest(), string.Empty, CancellationToken.None));

        Assert.Equal(CommandType.Text, connection.LastCommandType);
        Assert.Contains("JOIN Company", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyName", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CreatedBy = @CreatedBy", connection.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sellout_normal_scope_does_not_forward_administrator_only_company_filter()
    {
        var connection = new RecordingDbConnection();
        var repository = new SalesTransactionRepository(connection, null!);
        var request = new PagedListRequest { Filters = { ["CompanyName"] = "forged" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, 7, false, CancellationToken.None));

        Assert.Equal(CommandType.StoredProcedure, connection.LastCommandType);
        Assert.DoesNotContain("CompanyName", connection.LastParameterNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_level_normal_scope_does_not_forward_administrator_only_company_filter()
    {
        var connection = new RecordingDbConnection();
        var repository = new ServiceLevelRepository(connection, null!);
        var request = new PagedListRequest { Filters = { ["CompanyName"] = "forged" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SelectAsync(request, "distributor", CancellationToken.None));

        Assert.Equal(CommandType.StoredProcedure, connection.LastCommandType);
        Assert.DoesNotContain("CompanyName", connection.LastParameterNames, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;
        public CommandType LastCommandType { get; private set; }
        public IReadOnlyList<string> LastParameterNames { get; private set; } = [];
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
                connection.LastCommandType = CommandType;
                connection.LastParameterNames = DbParameterCollection.Cast<DbParameter>()
                    .Select(parameter => parameter.ParameterName).ToArray();
                throw new InvalidOperationException("Recorded query execution.");
            }
            protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
                CancellationToken cancellationToken)
            {
                connection.LastCommandText = CommandText;
                connection.LastCommandType = CommandType;
                connection.LastParameterNames = DbParameterCollection.Cast<DbParameter>()
                    .Select(parameter => parameter.ParameterName).ToArray();
                return Task.FromException<DbDataReader>(new InvalidOperationException("Recorded query execution."));
            }
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
        public override void RemoveAt(string parameterName) { var index = IndexOf(parameterName); if (index >= 0) RemoveAt(index); }
        protected override DbParameter GetParameter(int index) => _values[index];
        protected override DbParameter GetParameter(string parameterName) => _values[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0) _values.Add(value); else _values[index] = value;
        }
    }
}
