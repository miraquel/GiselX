using Microsoft.Data.SqlClient;

namespace GiselX.Tests;

public sealed class SalesManualSqlScriptTests
{
    [Fact]
    public void Script_is_rerunnable_and_preserves_historical_sales_identity()
    {
        var sql = ReadScript();

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COL_LENGTH(N'dbo.SalesTransaction', N'DistributorProductId')", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DistributorProductId = ProductId", sql, StringComparison.Ordinal);
        Assert.Contains("DistributorProductName = ProductName", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SET ProductId = DistributorProductId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script_installs_required_nullable_identity_columns_and_restrict_foreign_keys()
    {
        var sql = ReadScript();

        Assert.Contains("CREATE TABLE dbo.ItemMapping", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GmkItemId nvarchar(100)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DistributorItemName nvarchar(200)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ItemMappingId int NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON DELETE NO ACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_ItemMapping_CompanyId_DistributorItemId", sql, StringComparison.OrdinalIgnoreCase);
    }

    [LocalDbFact]
    [Trait("Category", "Integration")]
    public async Task Script_can_be_applied_twice_and_preserves_historical_gmk_identity()
    {
        var databaseName = $"GiselXItemMapping_{Guid.NewGuid():N}";
        const string master = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;TrustServerCertificate=true";

        try
        {
            await using var masterConnection = new SqlConnection(master);
            await masterConnection.OpenAsync();
            await using (var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", masterConnection))
                await create.ExecuteNonQueryAsync();

            await using var database = new SqlConnection(
                $"Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog={databaseName};TrustServerCertificate=true");
            await database.OpenAsync();
            await CreateLegacySchemaAsync(database);

            var script = ReadScript();
            await using (var first = new SqlCommand(script, database))
                await first.ExecuteNonQueryAsync();
            await using (var second = new SqlCommand(script, database))
                await second.ExecuteNonQueryAsync();

            await using var verify = new SqlCommand("""
                SELECT TYPE_NAME(c.user_type_id), c.precision, c.scale, c.is_nullable,
                       (SELECT DistributorProductId FROM dbo.Stock WHERE Id = 1),
                       (SELECT DistributorProductName FROM dbo.SalesTransaction WHERE Id = 1),
                       (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN
                           (N'FK_Stock_ItemMapping_ItemMappingId', N'FK_StockStaging_ItemMapping_ItemMappingId',
                            N'FK_SalesTransaction_ItemMapping_ItemMappingId', N'FK_SelloutStaging_ItemMapping_ItemMappingId'))
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.Stock', N'U') AND c.name = N'PriceUnit';
                """, database);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("decimal", reader.GetString(0));
            Assert.Equal((byte)18, reader.GetByte(1));
            Assert.Equal((byte)2, reader.GetByte(2));
            Assert.True(reader.GetBoolean(3));
            Assert.Equal("GMK-STOCK", reader.GetString(4));
            Assert.Equal("GMK-SALES", reader.GetString(5));
            Assert.Equal(4, reader.GetInt32(6));
        }
        finally
        {
            await using var masterConnection = new SqlConnection(master);
            try
            {
                await masterConnection.OpenAsync();
                await using var drop = new SqlCommand($"""
                    IF DB_ID(N'{databaseName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{databaseName}];
                    END
                    """, masterConnection);
                await drop.ExecuteNonQueryAsync();
            }
            catch (SqlException)
            {
                // LocalDB may have failed before the test database was created.
            }
        }
    }

    private static string ReadScript() => File.ReadAllText(FindFromSolutionRoot(
        "GiselX.Web", "DatabaseScripts", "20260811180000_AddCompanyItemIdentityMapping.sql"));

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }

    private static async Task CreateLegacySchemaAsync(SqlConnection connection)
    {
        const string schema = """
            CREATE TABLE dbo.__EFMigrationsHistory (MigrationId nvarchar(150) NOT NULL PRIMARY KEY, ProductVersion nvarchar(32) NOT NULL);
            CREATE TABLE dbo.Company (Id int IDENTITY PRIMARY KEY, Name nvarchar(100) NOT NULL, Address nvarchar(255) NULL, ContactEmail nvarchar(255) NULL, DeadlineDayOfMonth int NULL, DeadlineDaysOfWeek int NULL, ReminderLeadDays int NOT NULL);
            CREATE TABLE dbo.Stock (Id int IDENTITY PRIMARY KEY, ProductId nvarchar(100) NOT NULL, ProductName nvarchar(200) NOT NULL, PriceUnit int NULL, ItemMappingId int NULL);
            CREATE TABLE dbo.StockStaging (Id bigint IDENTITY PRIMARY KEY, ProductId nvarchar(100) NULL, ProductName nvarchar(200) NULL, PriceUnit int NULL, ItemMappingId int NULL);
            CREATE TABLE dbo.SelloutStaging (Id bigint IDENTITY PRIMARY KEY, ItemMappingId int NULL);
            CREATE TABLE dbo.SalesTransaction (
                Id int IDENTITY PRIMARY KEY, CustomerId nvarchar(100) NOT NULL, CustomerName nvarchar(200) NOT NULL,
                CustomerAlias nvarchar(200) NOT NULL, CustomerAddress nvarchar(500) NOT NULL, ProductId nvarchar(100) NOT NULL,
                ProductBrand nvarchar(200) NOT NULL, ProductName nvarchar(200) NOT NULL, ProductPackaging nvarchar(100) NOT NULL,
                ProductPcsInCtn int NOT NULL, BatchNumber nvarchar(100) NOT NULL, ExpiredDate datetime NOT NULL,
                InvoiceNo nvarchar(100) NOT NULL, InvoiceDate datetime NOT NULL, InvoiceQty decimal(18,2) NOT NULL,
                InvoiceUnit nvarchar(50) NOT NULL, UnitPrice decimal(18,2) NOT NULL, GrossValue decimal(18,2) NOT NULL,
                DiscountValue decimal(18,2) NOT NULL, DiscountPct decimal(18,2) NOT NULL, NetValue decimal(18,2) NOT NULL,
                InKg decimal(18,2) NOT NULL, GeisaPOId nvarchar(100) NOT NULL, ShipDate datetime NOT NULL,
                SalesmanNameGMK nvarchar(200) NOT NULL, CompanyId int NOT NULL, CreatedDate datetime NOT NULL, ItemMappingId int NULL);
            INSERT INTO dbo.Company (Name, ReminderLeadDays) VALUES (N'Test', 3);
            INSERT INTO dbo.Stock (ProductId, ProductName, PriceUnit) VALUES (N'GMK-STOCK', N'GMK Stock', 10);
            INSERT INTO dbo.SalesTransaction (CustomerId,CustomerName,CustomerAlias,CustomerAddress,ProductId,ProductBrand,ProductName,ProductPackaging,ProductPcsInCtn,BatchNumber,ExpiredDate,InvoiceNo,InvoiceDate,InvoiceQty,InvoiceUnit,UnitPrice,GrossValue,DiscountValue,DiscountPct,NetValue,InKg,GeisaPOId,ShipDate,SalesmanNameGMK,CompanyId,CreatedDate)
            VALUES (N'C1',N'Customer',N'',N'',N'GMK-SALES',N'',N'GMK Sales',N'',0,N'', '2026-01-01',N'INV-1','2026-08-01',1,N'',0,0,0,0,0,0,N'','2026-08-01',N'',1,'2026-08-11');
            """;
        await using var command = new SqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
    }
}
