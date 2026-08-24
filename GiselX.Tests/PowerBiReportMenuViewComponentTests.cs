using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using GiselX.Domain;
using GiselX.Repository;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class PowerBiReportMenuViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_passes_only_service_menu_items_to_the_view()
    {
        var items = new[]
        {
            new PowerBiReportMenuItemDto(2, "Stock Dashboard"),
            new PowerBiReportMenuItemDto(3, "Sales Dashboard")
        };
        var component = CreateComponent(MenuService.Returning(items));

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
        Assert.Equal(items, Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(result.ViewData!.Model));
    }

    [Fact]
    public void Component_has_no_company_id_input()
    {
        var method = typeof(PowerBiReportMenuViewComponent).GetMethod(nameof(PowerBiReportMenuViewComponent.InvokeAsync))!;
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_without_calling_the_service_for_anonymous_users()
    {
        var service = MenuService.Throwing(new InvalidOperationException("service must not be called"));
        var component = CreateComponent(service, authenticated: false);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
        Assert.Equal(0, service.ListMenuCallCount);
    }

    [Fact]
    public async Task InvokeAsync_discards_stale_data_when_the_service_response_is_unsuccessful()
    {
        var staleItems = new[] { new PowerBiReportMenuItemDto(99, "Stale Dashboard") };
        var service = MenuService.Returning(staleItems, errors: ["sensitive service failure"]);
        var component = CreateComponent(service);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_when_the_service_throws()
    {
        var exception = new InvalidOperationException("sensitive exception detail");
        var service = MenuService.Throwing(exception);
        var logger = new CapturingLogger<PowerBiReportMenuViewComponent>();
        var component = CreateComponent(service, logger: logger);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
        Assert.Equal(LogLevel.Warning, logger.Level);
        Assert.Same(exception, logger.Exception);
    }

    [Fact]
    public async Task Component_activation_does_not_open_the_database_and_open_failure_isolated_to_an_empty_menu()
    {
        var openException = new InvalidOperationException("Database open failed.");
        var connection = new MenuDbConnection(openException: openException);
        var logger = new CapturingLogger<PowerBiReportMenuViewComponent>();
        var component = CreateRepositoryBackedComponent(connection, logger);

        Assert.Equal(0, connection.OpenCount);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
        Assert.Equal(1, connection.OpenCount);
        Assert.True(connection.IsDisposed);
        Assert.Same(openException, logger.Exception);
    }

    [Fact]
    public async Task InvokeAsync_isolates_a_dedicated_connection_query_failure_and_logs_the_exception()
    {
        var queryException = new InvalidOperationException("Database query failed.");
        var connection = new MenuDbConnection(queryException: queryException);
        var logger = new CapturingLogger<PowerBiReportMenuViewComponent>();
        var component = CreateRepositoryBackedComponent(connection, logger);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
        Assert.True(connection.IsDisposed);
        Assert.Same(queryException, logger.Exception);
    }

    [Fact]
    public async Task InvokeAsync_rethrows_a_dedicated_query_cancellation_when_the_request_is_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancellationException = new OperationCanceledException(cancellation.Token);
        var connection = new MenuDbConnection(queryException: cancellationException);
        var component = CreateRepositoryBackedComponent(connection,
            NullLogger<PowerBiReportMenuViewComponent>.Instance, cancellation.Token);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => component.InvokeAsync());

        Assert.Same(cancellationException, exception);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task InvokeAsync_does_not_use_the_request_connection_with_a_pending_local_transaction()
    {
        using var cancellation = new CancellationTokenSource();
        var requestConnection = new MenuDbConnection();
        var menuConnection = new MenuDbConnection(rows:
        [
            new PowerBiReport
            {
                Id = 11,
                CompanyId = 7,
                Name = "Sales Dashboard",
                EmbedUrl = "https://app.powerbi.com/view?r=sales",
                IsActive = true
            }
        ]);
        var connectionFactory = new MenuConnectionFactory(() => menuConnection);
        var services = new ServiceCollection();
        services.AddGiselXRepository();
        services.AddScoped<IDbConnection>(_ => requestConnection);
        services.AddScoped<IPowerBiReportMenuConnectionFactory>(_ => connectionFactory);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var requestTransaction = scope.ServiceProvider.GetRequiredService<IDbTransaction>();
        var repository = scope.ServiceProvider.GetRequiredService<IPowerBiReportMenuRepository>();
        const string username = "user@example.test";
        var user = new AppIdentityUser
        {
            Id = "user-1",
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            CompanyId = 7
        };
        var service = new PowerBiReportMenuService(
            repository,
            new UserClaimDto { Username = username },
            CreateUserManager(user));
        var component = new PowerBiReportMenuViewComponent(service,
            NullLogger<PowerBiReportMenuViewComponent>.Instance)
        {
            ViewComponentContext = ViewComponentContext(authenticated: true, requestAborted: cancellation.Token)
        };

        var model = MenuModel(await component.InvokeAsync());

        Assert.Equal(0, requestConnection.TransactionConflictCount);
        Assert.Equal(1, menuConnection.OpenCount);
        Assert.Equal(cancellation.Token, menuConnection.OpenCancellationToken);
        Assert.Equal(cancellation.Token, menuConnection.QueryCancellationToken);
        Assert.True(menuConnection.IsDisposed);
        Assert.Equal([new PowerBiReportMenuItemDto(11, "Sales Dashboard")], model);
    }

    [Fact]
    public async Task InvokeAsync_rethrows_cancellation_when_the_request_is_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = MenuService.Throwing(new OperationCanceledException(cancellation.Token));
        var component = CreateComponent(service, requestAborted: cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => component.InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_returns_an_empty_menu_for_an_unrelated_operation_cancellation()
    {
        var service = MenuService.Throwing(new OperationCanceledException("transient downstream cancellation"));
        var component = CreateComponent(service);

        Assert.Empty(MenuModel(await component.InvokeAsync()));
    }

    [Fact]
    public async Task InvokeAsync_forwards_the_request_cancellation_token_to_the_service()
    {
        using var cancellation = new CancellationTokenSource();
        var service = MenuService.Returning([]);
        var component = CreateComponent(service, requestAborted: cancellation.Token);

        await component.InvokeAsync();

        Assert.Equal(cancellation.Token, service.ListMenuToken);
    }

    private static PowerBiReportMenuViewComponent CreateComponent(MenuService service, bool authenticated = true,
        CancellationToken requestAborted = default, ILogger<PowerBiReportMenuViewComponent>? logger = null)
    {
        var component = new PowerBiReportMenuViewComponent(service,
            logger ?? NullLogger<PowerBiReportMenuViewComponent>.Instance)
        {
            ViewComponentContext = ViewComponentContext(authenticated, requestAborted)
        };
        return component;
    }

    private static PowerBiReportMenuViewComponent CreateRepositoryBackedComponent(MenuDbConnection connection,
            ILogger<PowerBiReportMenuViewComponent> logger, CancellationToken requestAborted = default)
    {
        const string username = "user@example.test";
        var user = new AppIdentityUser
        {
            Id = "user-1",
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            CompanyId = 7
        };
        var connectionFactory = new MenuConnectionFactory(() => connection);
        var service = new PowerBiReportMenuService(
            new PowerBiReportMenuRepository(connectionFactory),
            new UserClaimDto { Username = username },
            CreateUserManager(user));
        var component = new PowerBiReportMenuViewComponent(service, logger)
        {
            ViewComponentContext = ViewComponentContext(authenticated: true, requestAborted: requestAborted)
        };
        return component;
    }

    private static IReadOnlyList<PowerBiReportMenuItemDto> MenuModel(IViewComponentResult result) =>
        Assert.IsAssignableFrom<IReadOnlyList<PowerBiReportMenuItemDto>>(
            Assert.IsType<ViewViewComponentResult>(result).ViewData!.Model);

    private static ViewComponentContext ViewComponentContext(bool authenticated, CancellationToken requestAborted)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: authenticated ? "test" : null)),
            RequestAborted = requestAborted
        };
        return new ViewComponentContext
        {
            ViewContext = new ViewContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            }
        };
    }

    private static UserManager<AppIdentityUser> CreateUserManager(AppIdentityUser user) => new(
        new SingleUserStore(user), Options.Create(new IdentityOptions()), new PasswordHasher<AppIdentityUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppIdentityUser>>.Instance);

    private sealed class MenuService(Func<CancellationToken,
        Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>>> listMenu) : IPowerBiReportMenuService
    {
        public int ListMenuCallCount { get; private set; }
        public CancellationToken ListMenuToken { get; private set; }

        public static MenuService Returning(IReadOnlyList<PowerBiReportMenuItemDto> items, List<string>? errors = null) =>
            new(_ => Task.FromResult(new ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>
            {
                Data = items,
                Errors = errors ?? []
            }));

        public static MenuService Throwing(Exception exception) => new(_ => Task.FromException<
            ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>>(exception));

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token)
        {
            ListMenuCallCount++;
            ListMenuToken = token;
            return listMenu(token);
        }

    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public LogLevel? Level { get; private set; }
        public Exception? Exception { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
        }
    }

    private sealed class SingleUserStore(AppIdentityUser user) : IUserStore<AppIdentityUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.Id);
        public Task<string?> GetUserNameAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.UserName);
        public Task SetUserNameAsync(AppIdentityUser value, string? userName, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<string?> GetNormalizedUserNameAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(AppIdentityUser value, string? normalizedName,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<AppIdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<AppIdentityUser?>(user.Id == userId ? user : null);
        public Task<AppIdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<AppIdentityUser?>(string.Equals(user.NormalizedUserName, normalizedUserName,
                StringComparison.OrdinalIgnoreCase) ? user : null);
    }

    private sealed class MenuDbConnection(
        Exception? openException = null,
        Exception? queryException = null,
        IReadOnlyList<PowerBiReport>? rows = null) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        private bool _hasPendingLocalTransaction;

        public int OpenCount { get; private set; }
        public int TransactionConflictCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public CancellationToken OpenCancellationToken { get; private set; }
        public CancellationToken QueryCancellationToken { get; private set; }
        [AllowNull] public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Menu";
        public override string DataSource => "Menu";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open()
        {
            OpenCount++;
            if (openException is not null) throw openException;
            _state = ConnectionState.Open;
        }
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            OpenCancellationToken = cancellationToken;
            if (openException is not null) return Task.FromException(openException);
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            _hasPendingLocalTransaction = true;
            return new MenuDbTransaction(this);
        }
        protected override DbCommand CreateDbCommand() => new MenuDbCommand(this);

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public DbDataReader ExecuteReader(DbCommand command, CancellationToken cancellationToken)
        {
            QueryCancellationToken = cancellationToken;
            if (_hasPendingLocalTransaction && command.Transaction is null)
            {
                TransactionConflictCount++;
                throw new InvalidOperationException(
                    "ExecuteReader requires the command to have a transaction when the connection assigned to the command is in a pending local transaction.");
            }
            if (queryException is not null) throw queryException;

            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("CompanyId", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("EmbedUrl", typeof(string));
            table.Columns.Add("DisplayOrder", typeof(int));
            table.Columns.Add("IsActive", typeof(bool));
            table.Columns.Add("CreatedDate", typeof(DateTime));
            table.Columns.Add("CreatedBy", typeof(string));
            table.Columns.Add("ModifiedDate", typeof(DateTime));
            table.Columns.Add("ModifiedBy", typeof(string));
            foreach (var report in rows ?? [])
            {
                table.Rows.Add(report.Id, report.CompanyId, report.Name, report.EmbedUrl, report.DisplayOrder,
                    report.IsActive, report.CreatedDate, report.CreatedBy, report.ModifiedDate ?? (object)DBNull.Value,
                    report.ModifiedBy ?? (object)DBNull.Value);
            }

            return table.CreateDataReader();
        }
    }

    private sealed class MenuConnectionFactory(Func<DbConnection> createConnection)
        : IPowerBiReportMenuConnectionFactory
    {
        public DbConnection CreateConnection() => createConnection();
    }

    private sealed class MenuDbTransaction(DbConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class MenuDbCommand(MenuDbConnection connection) : DbCommand
    {
        [AllowNull] public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new TestDbParameterCollection();
        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new TestDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            connection.ExecuteReader(this, CancellationToken.None);
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,
            CancellationToken cancellationToken) => Task.FromResult(connection.ExecuteReader(this, cancellationToken));
    }

    private sealed class TestDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class TestDbParameterCollection : DbParameterCollection
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
        public override int IndexOf(string parameterName) =>
            _values.FindIndex(value => value.ParameterName == parameterName);
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
