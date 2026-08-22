using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GiselX.Domain;
using GiselX.Repository;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Tests;

public sealed class PowerBiReportRepositoryContractTests
{
    [Fact]
    public async Task GetActiveByIdAndCompanyAsync_emits_id_company_and_active_predicates_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetActiveByIdAndCompanyAsync(17, 8, cancellation.Token));

        Assert.Contains("WHERE Id = @Id AND CompanyId = @CompanyId AND IsActive = 1", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "Id", 17);
        AssertParameter(connection, "CompanyId", 8);
    }

    [Fact]
    public async Task ListActiveByCompanyAsync_emits_company_active_filter_and_menu_order_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ListActiveByCompanyAsync(8, cancellation.Token));

        Assert.Contains("WHERE CompanyId = @CompanyId AND IsActive = 1", connection.LastCommandText);
        Assert.Contains("ORDER BY DisplayOrder, Name", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "CompanyId", 8);
    }

    [Fact]
    public async Task ListAdminAsync_emits_optional_company_filter_and_admin_order_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ListAdminAsync(null, cancellation.Token));

        Assert.Contains("WHERE (@CompanyId IS NULL OR CompanyId = @CompanyId)", connection.LastCommandText);
        Assert.Contains("ORDER BY CompanyId, DisplayOrder, Name", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "CompanyId", null);
    }

    [Fact]
    public async Task GetAdminByIdAsync_emits_an_exact_id_lookup_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAdminByIdAsync(17, cancellation.Token));

        Assert.Contains("WHERE Id = @Id;", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "Id", 17);
    }

    [Fact]
    public async Task NameExistsAsync_emits_company_name_and_excluded_id_parameters_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.ScalarResult = true;
        using var cancellation = new CancellationTokenSource();

        var exists = await repository.NameExistsAsync(8, "Sales", 17, cancellation.Token);

        Assert.True(exists);
        Assert.Contains("CompanyId = @CompanyId AND Name = @Name", connection.LastCommandText);
        Assert.Contains("@ExcludeId IS NULL OR Id <> @ExcludeId", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "CompanyId", 8);
        AssertParameter(connection, "Name", "Sales");
        AssertParameter(connection, "ExcludeId", 17);
    }

    [Fact]
    public async Task GetNextDisplayOrderAsync_emits_a_company_scoped_parameterized_query()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.ScalarResult = 4;
        using var cancellation = new CancellationTokenSource();

        var result = await repository.GetNextDisplayOrderAsync(8, cancellation.Token);

        Assert.Equal(4, result);
        Assert.Contains("COALESCE(MAX(DisplayOrder), 0) + 1", connection.LastCommandText);
        Assert.Contains("WHERE CompanyId = @CompanyId", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "CompanyId", 8);
    }

    [Fact]
    public async Task InsertAsync_emits_all_report_values_as_parameters_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.ScalarResult = 23;
        using var cancellation = new CancellationTokenSource();
        var report = Report();

        var id = await repository.InsertAsync(report, cancellation.Token);

        Assert.Equal(23, id);
        Assert.Contains("INSERT INTO PowerBiReport", connection.LastCommandText);
        Assert.Contains("VALUES", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "CompanyId", report.CompanyId);
        AssertParameter(connection, "Name", report.Name);
        AssertParameter(connection, "EmbedUrl", report.EmbedUrl);
        AssertParameter(connection, "DisplayOrder", report.DisplayOrder);
        AssertParameter(connection, "IsActive", report.IsActive);
        AssertParameter(connection, "CreatedDate", report.CreatedDate);
        AssertParameter(connection, "CreatedBy", report.CreatedBy);
        AssertParameter(connection, "ModifiedDate", report.ModifiedDate);
        AssertParameter(connection, "ModifiedBy", report.ModifiedBy);
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task InsertAsync_translates_only_the_power_bi_company_name_unique_index(int errorNumber)
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();
        var sqlException = CreateSqlException(errorNumber,
            "Duplicate key conflict on UX_PowerBiReport_CompanyId_Name.");
        connection.CommandException = sqlException;

        var exception = await Assert.ThrowsAsync<PowerBiReportDuplicateNameException>(() =>
            repository.InsertAsync(Report(), cancellation.Token));

        Assert.Same(sqlException, exception.InnerException);
        AssertCommandScope(connection, transaction, cancellation.Token);
    }

    [Fact]
    public async Task InsertAsync_does_not_translate_a_unique_violation_for_an_unrelated_index()
    {
        var (repository, connection, _) = CreatePowerBiRepository();
        var sqlException = CreateSqlException(2601, "Duplicate key conflict on UX_OtherEntity_Name.");
        connection.CommandException = sqlException;

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            repository.InsertAsync(Report(), CancellationToken.None));

        Assert.Same(sqlException, exception);
    }

    [Fact]
    public async Task UpdateAsync_emits_each_report_value_and_returns_true_for_an_affected_row()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.RowsAffected = 1;
        using var cancellation = new CancellationTokenSource();
        var report = Report();

        var updated = await repository.UpdateAsync(report, cancellation.Token);

        Assert.True(updated);
        Assert.Contains("UPDATE PowerBiReport", connection.LastCommandText);
        Assert.Contains("SET CompanyId = @CompanyId", connection.LastCommandText);
        Assert.Contains("ModifiedBy = @ModifiedBy", connection.LastCommandText);
        Assert.Contains("WHERE Id = @Id;", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "Id", report.Id);
        AssertParameter(connection, "CompanyId", report.CompanyId);
        AssertParameter(connection, "Name", report.Name);
        AssertParameter(connection, "EmbedUrl", report.EmbedUrl);
        AssertParameter(connection, "DisplayOrder", report.DisplayOrder);
        AssertParameter(connection, "IsActive", report.IsActive);
        AssertParameter(connection, "ModifiedDate", report.ModifiedDate);
        AssertParameter(connection, "ModifiedBy", report.ModifiedBy);
    }

    [Fact]
    public async Task UpdateAsync_returns_false_when_no_row_is_affected()
    {
        var (repository, connection, _) = CreatePowerBiRepository();
        connection.RowsAffected = 0;

        Assert.False(await repository.UpdateAsync(Report(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_translates_the_power_bi_company_name_unique_index()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        using var cancellation = new CancellationTokenSource();
        var sqlException = CreateSqlException(2601,
            "Duplicate key conflict on UX_PowerBiReport_CompanyId_Name.");
        connection.CommandException = sqlException;

        var exception = await Assert.ThrowsAsync<PowerBiReportDuplicateNameException>(() =>
            repository.UpdateAsync(Report(), cancellation.Token));

        Assert.Same(sqlException, exception.InnerException);
        AssertCommandScope(connection, transaction, cancellation.Token);
    }

    [Fact]
    public async Task UpdateAsync_does_not_translate_an_unrelated_database_error()
    {
        var (repository, connection, _) = CreatePowerBiRepository();
        var sqlException = CreateSqlException(547,
            "The UPDATE statement conflicted with a foreign key constraint UX_PowerBiReport_CompanyId_Name.");
        connection.CommandException = sqlException;

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            repository.UpdateAsync(Report(), CancellationToken.None));

        Assert.Same(sqlException, exception);
    }

    [Fact]
    public async Task SetActiveAsync_emits_state_audit_and_id_parameters_with_scope()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.RowsAffected = 1;
        using var cancellation = new CancellationTokenSource();
        var modifiedDate = new DateTime(2026, 8, 22, 9, 30, 0, DateTimeKind.Utc);

        var updated = await repository.SetActiveAsync(17, false, "admin", modifiedDate, cancellation.Token);

        Assert.True(updated);
        Assert.Contains("SET IsActive = @IsActive", connection.LastCommandText);
        Assert.Contains("ModifiedBy = @ModifiedBy", connection.LastCommandText);
        Assert.Contains("ModifiedDate = @ModifiedDate", connection.LastCommandText);
        Assert.Contains("WHERE Id = @Id;", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "Id", 17);
        AssertParameter(connection, "IsActive", false);
        AssertParameter(connection, "ModifiedBy", "admin");
        AssertParameter(connection, "ModifiedDate", modifiedDate);
    }

    [Fact]
    public async Task SetActiveAsync_returns_false_when_no_row_is_affected()
    {
        var (repository, connection, _) = CreatePowerBiRepository();
        connection.RowsAffected = 0;

        Assert.False(await repository.SetActiveAsync(17, false, "admin", DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_emits_an_exact_id_parameter_and_returns_true_for_an_affected_row()
    {
        var (repository, connection, transaction) = CreatePowerBiRepository();
        connection.RowsAffected = 1;
        using var cancellation = new CancellationTokenSource();

        var deleted = await repository.DeleteAsync(17, cancellation.Token);

        Assert.True(deleted);
        Assert.Equal("DELETE FROM PowerBiReport WHERE Id = @Id;", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
        AssertParameter(connection, "Id", 17);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_when_no_row_is_affected()
    {
        var (repository, connection, _) = CreatePowerBiRepository();
        connection.RowsAffected = 0;

        Assert.False(await repository.DeleteAsync(17, CancellationToken.None));
    }

    [Fact]
    public async Task Company_GetAllAsync_emits_name_ordering_with_transaction_and_cancellation()
    {
        var connection = new RecordingDbConnection();
        var transaction = new RecordingDbTransaction(connection);
        var repository = new CompanyRepository(connection, transaction);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAllAsync(cancellation.Token));

        Assert.Equal("SELECT Id, Name FROM Company ORDER BY Name;", connection.LastCommandText);
        AssertCommandScope(connection, transaction, cancellation.Token);
    }

    [Fact]
    public void Repository_source_rejects_all_csharp_interpolation_forms()
    {
        var source = ReadRepository();

        Assert.DoesNotMatch("(?:\\$+@?\"|@\\$+\")", source);
    }

    private static (PowerBiReportRepository Repository, RecordingDbConnection Connection,
        RecordingDbTransaction Transaction) CreatePowerBiRepository()
    {
        var connection = new RecordingDbConnection();
        var transaction = new RecordingDbTransaction(connection);
        return (new PowerBiReportRepository(connection, transaction), connection, transaction);
    }

    private static PowerBiReport Report() => new()
    {
        Id = 17,
        CompanyId = 8,
        Name = "Sales",
        EmbedUrl = "https://app.powerbi.com/view?r=report",
        DisplayOrder = 4,
        IsActive = true,
        CreatedDate = new DateTime(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc),
        CreatedBy = "creator",
        ModifiedDate = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc),
        ModifiedBy = "editor"
    };

    private static void AssertCommandScope(RecordingDbConnection connection, IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        Assert.Same(transaction, connection.LastTransaction);
        Assert.Equal(cancellationToken, connection.LastCancellationToken);
    }

    private static void AssertParameter(RecordingDbConnection connection, string name, object? expected)
    {
        Assert.True(connection.LastParameters.TryGetValue(name, out var actual), $"Parameter {name} was not recorded.");
        Assert.Equal(expected, actual);
    }

    private static SqlException CreateSqlException(int number, string message)
    {
        var errorConstructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(int)))
            .OrderBy(constructor => constructor.GetParameters().Length)
            .First();
        var error = (SqlError)errorConstructor.Invoke(errorConstructor.GetParameters()
            .Select(parameter => ConstructorArgument(parameter, number, message)).ToArray());
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(errors, [error]);

        var factory = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == "CreateException")
            .Where(method => method.GetParameters().FirstOrDefault()?.ParameterType == typeof(SqlErrorCollection))
            .OrderBy(method => method.GetParameters().Length)
            .First();
        return (SqlException)factory.Invoke(null, factory.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(SqlErrorCollection) ? errors : DefaultArgument(parameter.ParameterType))
            .ToArray())!;
    }

    private static object? ConstructorArgument(ParameterInfo parameter, int number, string message) => parameter.Name switch
    {
        "infoNumber" or "number" => number,
        "errorMessage" or "message" => message,
        _ => DefaultArgument(parameter.ParameterType)
    };

    private static object? DefaultArgument(Type type) => type == typeof(string)
        ? string.Empty
        : type.IsValueType ? Activator.CreateInstance(type) : null;

    private static string ReadRepository() => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "GiselX.Repository", "PowerBiReportRepository.cs"));

    private sealed class RecordingDbConnection : DbConnection
    {
        public string LastCommandText { get; private set; } = string.Empty;
        public IReadOnlyDictionary<string, object?> LastParameters { get; private set; } =
            new Dictionary<string, object?>();
        public IDbTransaction? LastTransaction { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public int RowsAffected { get; set; }
        public object? ScalarResult { get; set; }
        public Exception? CommandException { get; set; }

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
                .ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value is DBNull ? null : parameter.Value);
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
        public override int ExecuteNonQuery()
        {
            connection.Record(this, CancellationToken.None);
            if (connection.CommandException is not null) throw connection.CommandException;
            return connection.RowsAffected;
        }
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            connection.Record(this, cancellationToken);
            if (connection.CommandException is not null)
                return Task.FromException<int>(connection.CommandException);
            return Task.FromResult(connection.RowsAffected);
        }
        public override object? ExecuteScalar()
        {
            connection.Record(this, CancellationToken.None);
            if (connection.CommandException is not null) throw connection.CommandException;
            return connection.ScalarResult;
        }
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            connection.Record(this, cancellationToken);
            if (connection.CommandException is not null)
                return Task.FromException<object?>(connection.CommandException);
            return Task.FromResult(connection.ScalarResult);
        }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.Record(this, CancellationToken.None);
            throw new InvalidOperationException("Recorded query execution.");
        }
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            connection.Record(this, cancellationToken);
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
        public override object SyncRoot => ((ICollection)_values).SyncRoot;
        public override int Add(object value) { _values.Add((DbParameter)value); return _values.Count - 1; }
        public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_values).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _values.GetEnumerator();
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
}
