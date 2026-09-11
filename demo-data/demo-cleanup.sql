-- Removes everything demo-seed.sql created, including any uploads made against the demo company.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @CompanyId int = (SELECT Id FROM dbo.Company WHERE Name = N'Demo Distributor Nusantara');
IF @CompanyId IS NULL
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'No demo data found.';
    RETURN;
END

DELETE FROM dbo.StockStaging WHERE StockUploadBatchId IN (SELECT Id FROM dbo.StockUploadBatch WHERE CompanyId = @CompanyId);
DELETE FROM dbo.SelloutStaging WHERE SelloutUploadBatchId IN (SELECT Id FROM dbo.SelloutUploadBatch WHERE CompanyId = @CompanyId);
DELETE FROM dbo.Stock WHERE CompanyId = @CompanyId;
DELETE FROM dbo.SalesTransaction WHERE CompanyId = @CompanyId;
DELETE FROM dbo.StockUploadBatch WHERE CompanyId = @CompanyId;
DELETE FROM dbo.SelloutUploadBatch WHERE CompanyId = @CompanyId;
DELETE FROM dbo.SelloutUploadGroup WHERE CompanyId = @CompanyId;
DELETE FROM dbo.StockTemplateColumn WHERE StockTemplateMappingId IN (SELECT Id FROM dbo.StockTemplateMapping WHERE CompanyId = @CompanyId);
DELETE FROM dbo.StockTemplateMapping WHERE CompanyId = @CompanyId;
DELETE FROM dbo.DistributorTemplateColumn WHERE DistributorTemplateMappingId IN (SELECT Id FROM dbo.DistributorTemplateMapping WHERE CompanyId = @CompanyId);
DELETE FROM dbo.DistributorTemplateMapping WHERE CompanyId = @CompanyId;
DELETE FROM dbo.ItemMapping WHERE CompanyId = @CompanyId;
DELETE FROM [identity].AspNetUserRoles WHERE UserId IN (SELECT Id FROM [identity].AspNetUsers WHERE CompanyId = @CompanyId);
DELETE FROM [identity].AspNetUserClaims WHERE UserId IN (SELECT Id FROM [identity].AspNetUsers WHERE CompanyId = @CompanyId);
DELETE FROM [identity].AspNetUserLogins WHERE UserId IN (SELECT Id FROM [identity].AspNetUsers WHERE CompanyId = @CompanyId);
DELETE FROM [identity].AspNetUserTokens WHERE UserId IN (SELECT Id FROM [identity].AspNetUsers WHERE CompanyId = @CompanyId);
DELETE FROM [identity].AspNetUsers WHERE CompanyId = @CompanyId;
DELETE FROM dbo.Company WHERE Id = @CompanyId;

COMMIT TRANSACTION;
PRINT N'Demo data removed.';
