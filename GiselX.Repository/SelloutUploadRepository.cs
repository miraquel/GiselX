using System.Data;
using System.Text.Json;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;

namespace GiselX.Repository;

public sealed class SelloutUploadRepository(IDbConnection connection, IDbTransaction transaction) : ISelloutUploadRepository
{
    public Task<long> InsertGroupAsync(SelloutUploadGroup group, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO SelloutUploadGroup (CompanyId, DistributorTemplateMappingId, OriginalFileName, FileSha256,
                Status, TotalRows, ValidRows, InvalidRows, IgnoredSummaryRowsJson, ValidationErrorsJson, ErrorSummary,
                UploadedBy, UploadedDate, CompletedDate)
            OUTPUT INSERTED.Id VALUES (@CompanyId, @DistributorTemplateMappingId, @OriginalFileName, @FileSha256,
                @Status, @TotalRows, @ValidRows, @InvalidRows, @IgnoredSummaryRowsJson, @ValidationErrorsJson, NULL,
                @UploadedBy, @UploadedDate, NULL)
            """, group, transaction, cancellationToken: cancellationToken));

    public Task SetArchivedFilePathAsync(long groupId, string archivedFilePath,
        CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition(
            "UPDATE SelloutUploadGroup SET ArchivedFilePath = @Path WHERE Id = @GroupId;",
            new { GroupId = groupId, Path = archivedFilePath }, transaction,
            cancellationToken: cancellationToken));

    public Task<long> InsertBatchAsync(SelloutUploadBatch batch, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO SelloutUploadBatch (CompanyId, DistributorTemplateMappingId, SelloutUploadGroupId,
                ReportingWeekStart, OriginalFileName, FileSha256, Status, TotalRows, ValidRows, InvalidRows,
                ErrorSummary, UploadedBy, UploadedDate, CompletedDate)
            OUTPUT INSERTED.Id VALUES (@CompanyId, @DistributorTemplateMappingId, @SelloutUploadGroupId,
                @ReportingWeekStart, @OriginalFileName, @FileSha256, @Status, @TotalRows, @ValidRows, @InvalidRows,
                NULL, @UploadedBy, @UploadedDate, NULL)
            """, batch, transaction, cancellationToken: cancellationToken));

    public async Task InsertStagingAsync(IReadOnlyCollection<SelloutStaging> rows, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO SelloutStaging (SelloutUploadBatchId, SourceRowNumber, CustomerId, CustomerName, CustomerAlias,
             CustomerAddress, ProductId, ProductBrand, ProductName, DistributorProductId, DistributorProductName,
             ItemMappingId, ProductPackaging, ProductPcsInCtn, BatchNumber,
             ExpiredDate, InvoiceNo, InvoiceDate, InvoiceQty, InvoiceUnit, UnitPrice, GrossValue, DiscountValue,
             DiscountPct, NetValue, InKg, GeisaPOId, ShipDate, SalesmanNameGMK, IsValid, ValidationErrorsJson)
            VALUES (@SelloutUploadBatchId, @SourceRowNumber, @CustomerId, @CustomerName, @CustomerAlias,
             @CustomerAddress, @ProductId, @ProductBrand, @ProductName, @DistributorProductId, @DistributorProductName,
             @ItemMappingId, @ProductPackaging, @ProductPcsInCtn, @BatchNumber,
             @ExpiredDate, @InvoiceNo, @InvoiceDate, @InvoiceQty, @InvoiceUnit, @UnitPrice, @GrossValue, @DiscountValue,
             @DiscountPct, @NetValue, @InKg, @GeisaPOId, @ShipDate, @SalesmanNameGMK, @IsValid, @ValidationErrorsJson)
            """;
        foreach (var chunk in rows.Chunk(500))
            await connection.ExecuteAsync(new CommandDefinition(sql, chunk, transaction, cancellationToken: cancellationToken));
    }

    public Task MarkGroupFailedAsync(long groupId, int totalRows, int invalidRows, string errorSummary,
        string validationErrorsJson, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("""
            UPDATE SelloutUploadGroup SET Status=1, TotalRows=@TotalRows, ValidRows=@TotalRows-@InvalidRows,
              InvalidRows=@InvalidRows, ErrorSummary=@ErrorSummary, ValidationErrorsJson=@ValidationErrorsJson,
              CompletedDate=SYSUTCDATETIME() WHERE Id=@GroupId AND Status=0
            """, new
            {
                GroupId = groupId,
                TotalRows = totalRows,
                InvalidRows = invalidRows,
                ErrorSummary = errorSummary,
                ValidationErrorsJson = validationErrorsJson
            }, transaction, cancellationToken: cancellationToken));

    /// <summary>
    /// Publishes a group's valid staging rows, replacing every reporting week the group covers. Invalid
    /// rows stay in staging as the record of what the workbook could not supply and are never published;
    /// the batch they belong to completes with warnings rather than cleanly.
    /// </summary>
    public async Task PromoteGroupAsync(long groupId, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @CompanyId int;
            SELECT @CompanyId=CompanyId FROM SelloutUploadGroup WITH (UPDLOCK,HOLDLOCK)
             WHERE Id=@GroupId AND Status=0;
            IF @CompanyId IS NULL THROW 50001, 'Upload group is missing or is not processing.', 1;
            IF NOT EXISTS (SELECT 1 FROM SelloutUploadBatch WHERE SelloutUploadGroupId=@GroupId AND Status=0)
             THROW 50002, 'Upload group contains no processing batches.', 1;

            -- Status 4 is CompletedWithWarnings: a batch that published its valid rows while excluding
            -- others. It is as much a live batch as status 2 and must be superseded the same way.
            SELECT prior.Id FROM SelloutUploadBatch prior WITH (UPDLOCK,HOLDLOCK)
            JOIN SelloutUploadBatch currentBatch ON currentBatch.SelloutUploadGroupId=@GroupId
             AND currentBatch.CompanyId=prior.CompanyId
             AND currentBatch.ReportingWeekStart=prior.ReportingWeekStart
            WHERE prior.Status IN (2,4);

            UPDATE prior SET Status=3
            FROM SelloutUploadBatch prior
            JOIN SelloutUploadBatch currentBatch ON currentBatch.SelloutUploadGroupId=@GroupId
             AND currentBatch.CompanyId=prior.CompanyId
             AND currentBatch.ReportingWeekStart=prior.ReportingWeekStart
            WHERE prior.Status IN (2,4);

            DELETE staging FROM SelloutStaging staging
            JOIN SelloutUploadBatch replaced ON replaced.Id=staging.SelloutUploadBatchId
            WHERE replaced.Status=3 AND EXISTS (
                SELECT 1 FROM SelloutUploadBatch currentBatch
                WHERE currentBatch.SelloutUploadGroupId=@GroupId
                  AND currentBatch.CompanyId=replaced.CompanyId
                  AND currentBatch.ReportingWeekStart=replaced.ReportingWeekStart);

            DELETE sales FROM SalesTransaction sales
            WHERE sales.CompanyId=@CompanyId AND EXISTS (
                SELECT 1 FROM SelloutUploadBatch currentBatch
                WHERE currentBatch.SelloutUploadGroupId=@GroupId AND
                 (sales.ReportingWeekStart=currentBatch.ReportingWeekStart OR
                  (sales.ReportingWeekStart IS NULL AND sales.InvoiceDate>=currentBatch.ReportingWeekStart
                   AND sales.InvoiceDate<DATEADD(day,7,currentBatch.ReportingWeekStart))));

            INSERT INTO SalesTransaction (CustomerId,CustomerName,CustomerAlias,CustomerAddress,ProductId,ProductBrand,
             ProductName,DistributorProductId,DistributorProductName,ItemMappingId,ProductPackaging,ProductPcsInCtn,BatchNumber,ExpiredDate,InvoiceNo,InvoiceDate,InvoiceQty,InvoiceUnit,
             UnitPrice,GrossValue,DiscountValue,DiscountPct,NetValue,InKg,GeisaPOId,ShipDate,SalesmanNameGMK,CompanyId,CreatedDate,
             SelloutUploadBatchId,ReportingWeekStart)
            SELECT COALESCE(s.CustomerId,''),COALESCE(s.CustomerName,''),COALESCE(s.CustomerAlias,''),COALESCE(s.CustomerAddress,''),
             COALESCE(s.ProductId,''),COALESCE(s.ProductBrand,''),COALESCE(s.ProductName,''),s.DistributorProductId,
             s.DistributorProductName,s.ItemMappingId,COALESCE(s.ProductPackaging,''),
             COALESCE(s.ProductPcsInCtn,0),COALESCE(s.BatchNumber,''),COALESCE(s.ExpiredDate,CONVERT(date,'17530101')),
             COALESCE(s.InvoiceNo,''),COALESCE(s.InvoiceDate,CONVERT(date,'17530101')),COALESCE(s.InvoiceQty,0),COALESCE(s.InvoiceUnit,''),
             COALESCE(s.UnitPrice,0),COALESCE(s.GrossValue,0),COALESCE(s.DiscountValue,0),COALESCE(s.DiscountPct,0),COALESCE(s.NetValue,0),
             COALESCE(s.InKg,0),COALESCE(s.GeisaPOId,''),COALESCE(s.ShipDate,CONVERT(date,'17530101')),COALESCE(s.SalesmanNameGMK,''),
             b.CompanyId,SYSUTCDATETIME(),b.Id,b.ReportingWeekStart
            FROM SelloutStaging s JOIN SelloutUploadBatch b ON b.Id=s.SelloutUploadBatchId
            WHERE b.SelloutUploadGroupId=@GroupId AND s.IsValid=1;

            UPDATE b SET
             Status=CASE WHEN (SELECT COUNT(*) FROM SelloutStaging s
                               WHERE s.SelloutUploadBatchId=b.Id AND s.IsValid=0) > 0 THEN 4 ELSE 2 END,
             TotalRows=(SELECT COUNT(*) FROM SelloutStaging s WHERE s.SelloutUploadBatchId=b.Id),
             ValidRows=(SELECT COUNT(*) FROM SelloutStaging s WHERE s.SelloutUploadBatchId=b.Id AND s.IsValid=1),
             InvalidRows=(SELECT COUNT(*) FROM SelloutStaging s WHERE s.SelloutUploadBatchId=b.Id AND s.IsValid=0),
             CompletedDate=SYSUTCDATETIME()
            FROM SelloutUploadBatch b WHERE b.SelloutUploadGroupId=@GroupId AND b.Status=0;

            -- The group keeps the counts the analysis produced. Recomputing them from staging would lose
            -- the rows that had no reporting week to be staged against, which the group still reports.
            UPDATE SelloutUploadGroup SET
             Status=CASE WHEN InvalidRows > 0 THEN 3 ELSE 2 END,
             CompletedDate=SYSUTCDATETIME()
            WHERE Id=@GroupId AND Status=0;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { GroupId = groupId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<(SelloutUploadBatch Batch, IReadOnlyList<SelloutStaging> Rows)?> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken)
    {
        using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT b.*, m.Name MappingName FROM SelloutUploadBatch b LEFT JOIN DistributorTemplateMapping m ON m.Id=b.DistributorTemplateMappingId
             WHERE b.Id=@BatchId AND (@CompanyId=0 OR b.CompanyId=@CompanyId);
            SELECT s.* FROM SelloutStaging s JOIN SelloutUploadBatch b ON b.Id=s.SelloutUploadBatchId
             WHERE b.Id=@BatchId AND (@CompanyId=0 OR b.CompanyId=@CompanyId) ORDER BY s.SourceRowNumber;
            """, new { BatchId = batchId, CompanyId = companyId }, transaction, cancellationToken: cancellationToken));
        var batch = await result.ReadSingleOrDefaultAsync<SelloutUploadBatch>();
        return batch is null ? null : (batch, (await result.ReadAsync<SelloutStaging>()).ToList());
    }

    public async Task<(SelloutUploadGroup Group, IReadOnlyList<SelloutUploadBatch> Batches, IReadOnlyList<SelloutStaging> Rows)?> GetGroupAsync(
        long groupId, int companyId, CancellationToken cancellationToken)
    {
        using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT g.* FROM SelloutUploadGroup g WHERE g.Id=@GroupId AND (@CompanyId=0 OR g.CompanyId=@CompanyId);
            SELECT b.* FROM SelloutUploadBatch b
             WHERE b.SelloutUploadGroupId=@GroupId AND (@CompanyId=0 OR b.CompanyId=@CompanyId) ORDER BY b.ReportingWeekStart;
            SELECT s.* FROM SelloutStaging s JOIN SelloutUploadBatch b ON b.Id=s.SelloutUploadBatchId
             WHERE b.SelloutUploadGroupId=@GroupId AND (@CompanyId=0 OR b.CompanyId=@CompanyId) ORDER BY s.SourceRowNumber;
            """, new { GroupId = groupId, CompanyId = companyId }, transaction, cancellationToken: cancellationToken));
        var group = await result.ReadSingleOrDefaultAsync<SelloutUploadGroup>();
        if (group is null) return null;
        var batches = (await result.ReadAsync<SelloutUploadBatch>()).ToList();
        var rows = (await result.ReadAsync<SelloutStaging>()).ToList();
        return (group, batches, rows);
    }
}
