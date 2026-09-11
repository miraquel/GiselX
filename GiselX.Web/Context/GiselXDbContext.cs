using GiselX.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Web.Context;

public partial class GiselXDbContext : IdentityDbContext<AppIdentityUser, AppIdentityRole, string>
{
    public GiselXDbContext(DbContextOptions<GiselXDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Company> Company { get; set; }

    public virtual DbSet<BusinessUnit> BusinessUnit { get; set; }

    public virtual DbSet<ServelDeliveryEx> ServelDeliveryEx { get; set; }

    public virtual DbSet<ServelReceiptEx> ServelReceiptEx { get; set; }

    public virtual DbSet<TransDist> TransDist { get; set; }

    public virtual DbSet<Stock> Stock { get; set; }

    public virtual DbSet<SalesTransaction> SalesTransaction { get; set; }

    public virtual DbSet<ItemMapping> ItemMapping { get; set; }

    public virtual DbSet<Channel> Channel { get; set; }

    public virtual DbSet<Salesman> Salesman { get; set; }

    public virtual DbSet<PowerBiReport> PowerBiReport { get; set; }

    public virtual DbSet<DistributorTemplateMapping> DistributorTemplateMapping { get; set; }

    public virtual DbSet<DistributorTemplateColumn> DistributorTemplateColumn { get; set; }

    public virtual DbSet<SelloutUploadBatch> SelloutUploadBatch { get; set; }

    public virtual DbSet<SelloutUploadGroup> SelloutUploadGroup { get; set; }

    public virtual DbSet<SelloutStaging> SelloutStaging { get; set; }

    public virtual DbSet<StockTemplateMapping> StockTemplateMapping { get; set; }

    public virtual DbSet<StockTemplateColumn> StockTemplateColumn { get; set; }

    public virtual DbSet<StockUploadBatch> StockUploadBatch { get; set; }

    public virtual DbSet<StockStaging> StockStaging { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set Identity schema for ASP.NET Identity tables
        modelBuilder.Entity<AppIdentityUser>().ToTable("AspNetUsers", "identity");
        modelBuilder.Entity<AppIdentityRole>().ToTable("AspNetRoles", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("AspNetUserRoles", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("AspNetUserClaims", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("AspNetUserLogins", "identity");
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.ProviderKey).HasMaxLength(128);
        });
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("AspNetUserTokens", "identity");
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(100);

            // New reminder fields
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.ReminderLeadDays).HasDefaultValue(3);
            entity.Property(e => e.ItemMappingEnabled).HasDefaultValue(false);
            entity.Property(e => e.DeadlineDaysOfWeek).HasConversion<int?>();
            entity.HasOne(e => e.BusinessUnit).WithMany(e => e.Companies)
                .HasForeignKey(e => e.BusinessUnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Channel).WithMany(e => e.Companies)
                .HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Salesman).WithMany(e => e.Companies)
                .HasForeignKey(e => e.SalesmanId).OnDelete(DeleteBehavior.Restrict);
            // DeadlineDayOfMonth is int? — EF maps to INT NULL automatically
        });

        modelBuilder.Entity<BusinessUnit>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<ItemMapping>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.DistributorItemId }).IsUnique();
            entity.Property(e => e.GmkItemId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.GmkItemName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DistributorItemId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DistributorItemName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
            entity.HasOne(e => e.Company).WithMany(e => e.ItemMappings)
                .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_Channel_Code");
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
        });

        modelBuilder.Entity<Salesman>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_Salesman_Code");
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
        });

        modelBuilder.Entity<PowerBiReport>(entity =>
        {
            entity.Property(value => value.Name).HasMaxLength(150).IsRequired();
            entity.Property(value => value.EmbedUrl).HasMaxLength(2048).IsRequired();
            entity.Property(value => value.IsActive).HasDefaultValue(true);
            entity.Property(value => value.CreatedDate).HasColumnType("datetime2");
            entity.Property(value => value.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(value => value.ModifiedDate).HasColumnType("datetime2");
            entity.Property(value => value.ModifiedBy).HasMaxLength(256);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PowerBiReport_DisplayOrder_NonNegative",
                "[DisplayOrder] >= 0"));
            entity.HasIndex(value => new { value.CompanyId, value.Name })
                .IsUnique().HasDatabaseName("UX_PowerBiReport_CompanyId_Name");
            entity.HasIndex(value => new { value.CompanyId, value.IsActive, value.DisplayOrder, value.Name })
                .HasDatabaseName("IX_PowerBiReport_CompanyId_IsActive_DisplayOrder_Name");
            entity.HasOne(value => value.Company).WithMany(company => company.PowerBiReports)
                .HasForeignKey(value => value.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DistributorTemplateMapping>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.WorksheetName).HasMaxLength(128);
            entity.Property(e => e.HeaderSignature).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_DistributorTemplateMapping_HeaderRow", "[HeaderRowNumber] > 0");
                table.HasCheckConstraint("CK_DistributorTemplateMapping_FirstDataRow", "[FirstDataRowNumber] > [HeaderRowNumber]");
            });

            entity.HasOne(e => e.Company).WithMany(e => e.DistributorTemplateMappings)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DistributorTemplateColumn>(entity =>
        {
            entity.HasIndex(e => new { e.DistributorTemplateMappingId, e.SourceColumnNumber });
            entity.HasIndex(e => new { e.DistributorTemplateMappingId, e.CanonicalField }).IsUnique();
            entity.Property(e => e.SourceHeader).HasMaxLength(500);
            entity.Property(e => e.CanonicalField).HasConversion<int>();

            entity.HasOne(e => e.DistributorTemplateMapping).WithMany(e => e.Columns)
                .HasForeignKey(e => e.DistributorTemplateMappingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockTemplateMapping>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.WorksheetName).HasMaxLength(128);
            entity.Property(e => e.HeaderSignature).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.ModifiedBy).HasMaxLength(256);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime2");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_StockTemplateMapping_HeaderRow", "[HeaderRowNumber] > 0");
                table.HasCheckConstraint("CK_StockTemplateMapping_FirstDataRow", "[FirstDataRowNumber] > [HeaderRowNumber]");
            });
            entity.HasOne(e => e.Company).WithMany(e => e.StockTemplateMappings)
                .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockTemplateColumn>(entity =>
        {
            entity.HasIndex(e => new { e.StockTemplateMappingId, e.SourceColumnNumber });
            entity.HasIndex(e => new { e.StockTemplateMappingId, e.CanonicalField }).IsUnique();
            entity.Property(e => e.SourceHeader).HasMaxLength(500);
            entity.Property(e => e.CanonicalField).HasConversion<int>();
            entity.HasOne(e => e.StockTemplateMapping).WithMany(e => e.Columns)
                .HasForeignKey(e => e.StockTemplateMappingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockUploadBatch>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.CompletedDate, e.Id });
            entity.HasIndex(e => new { e.CompanyId, e.StockDate });
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.StockDate).HasColumnType("date");
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.FileSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.IgnoredSummaryRowsJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");
            entity.Property(e => e.ValidationErrorsJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");
            entity.Property(e => e.ErrorSummary).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UploadedBy).HasMaxLength(256);
            entity.Property(e => e.UploadedDate).HasColumnType("datetime2");
            entity.Property(e => e.CompletedDate).HasColumnType("datetime2");
            entity.HasOne(e => e.Company).WithMany(e => e.StockUploadBatches)
                .HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StockTemplateMapping).WithMany(e => e.UploadBatches)
                .HasForeignKey(e => e.StockTemplateMappingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockStaging>(entity =>
        {
            entity.HasIndex(e => new { e.StockUploadBatchId, e.SourceRowNumber });
            entity.Property(e => e.SourceWorksheetName).HasMaxLength(255);
            entity.Property(e => e.ProductId).HasMaxLength(100);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.DistributorProductId).HasMaxLength(100);
            entity.Property(e => e.DistributorProductName).HasMaxLength(200);
            entity.Property(e => e.ProductPackaging).HasMaxLength(100);
            entity.Property(e => e.ProductUnit).HasMaxLength(50);
            entity.Property(e => e.SmallestUnit).HasMaxLength(50);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ProductPcsInCtn).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductNetto).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.PriceUnit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SmallestUnitQty).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoAwal).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoMasukPO).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoAkhir).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.ValidationErrorsJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]");
            entity.HasOne(e => e.StockUploadBatch).WithMany(e => e.StagingRows)
                .HasForeignKey(e => e.StockUploadBatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ItemMapping>().WithMany()
                .HasForeignKey(e => e.ItemMappingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StockTemplateMapping>().WithMany()
                .HasForeignKey(e => e.StockTemplateMappingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SelloutUploadBatch>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.ReportingWeekStart })
                .IsUnique()
                .HasFilter("[Status] = 2");
            entity.Property(e => e.ReportingWeekStart).HasColumnType("date");
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.FileSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ErrorSummary).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UploadedBy).HasMaxLength(256);
            entity.Property(e => e.UploadedDate).HasColumnType("datetime2");
            entity.Property(e => e.CompletedDate).HasColumnType("datetime2");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SelloutUploadBatch_ReportingWeekMonday",
                "DATEDIFF(day, '19000101', [ReportingWeekStart]) % 7 = 0"));

            entity.HasOne(e => e.Company).WithMany(e => e.SelloutUploadBatches)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DistributorTemplateMapping).WithMany(e => e.UploadBatches)
                .HasForeignKey(e => e.DistributorTemplateMappingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SelloutUploadGroup).WithMany(e => e.Batches)
                .HasForeignKey(e => e.SelloutUploadGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SelloutUploadGroup>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.UploadedDate });
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.FileSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.IgnoredSummaryRowsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ValidationErrorsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorSummary).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UploadedBy).HasMaxLength(256);
            entity.Property(e => e.UploadedDate).HasColumnType("datetime2");
            entity.Property(e => e.CompletedDate).HasColumnType("datetime2");

            entity.HasOne(e => e.Company).WithMany(e => e.SelloutUploadGroups)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DistributorTemplateMapping).WithMany(e => e.UploadGroups)
                .HasForeignKey(e => e.DistributorTemplateMappingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SelloutStaging>(entity =>
        {
            entity.HasIndex(e => new { e.SelloutUploadBatchId, e.SourceRowNumber });
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.ShipDate).HasColumnType("datetime");
            entity.Property(e => e.ProductPcsInCtn).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceQty).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GrossValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountPct).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InKg).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.DistributorProductId).HasMaxLength(100);
            entity.Property(e => e.DistributorProductName).HasMaxLength(200);
            entity.Property(e => e.ValidationErrorsJson).HasColumnType("nvarchar(max)");

            entity.HasOne(e => e.SelloutUploadBatch).WithMany(e => e.StagingRows)
                .HasForeignKey(e => e.SelloutUploadBatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ItemMapping>().WithMany()
                .HasForeignKey(e => e.ItemMappingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServelDeliveryEx>(entity =>
        {
            entity.Property(e => e.DoDate).HasColumnType("datetime");
            entity.Property(e => e.DoDateMin).HasColumnType("datetime");
            entity.Property(e => e.DoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.DoQtyCalc).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.ItemId).HasMaxLength(20);
            entity.Property(e => e.ItemName).HasMaxLength(4000);
            entity.Property(e => e.QuadranServel).HasMaxLength(20);
            entity.Property(e => e.ReceiptDate).HasColumnType("datetime");
            entity.Property(e => e.ReceiptDateRequest).HasColumnType("datetime");
            entity.Property(e => e.ReceiptDateRequestCalc).HasColumnType("datetime");
            entity.Property(e => e.ShippingDateRequest).HasColumnType("datetime");
            entity.Property(e => e.ShippingDateRequestCalc).HasColumnType("datetime");
            entity.Property(e => e.SoCreateDate).HasColumnType("datetime");
            entity.Property(e => e.SoId).HasMaxLength(20);
            entity.Property(e => e.SoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.SoQtyCalc).HasColumnType("numeric(32, 16)");

            entity.HasOne(d => d.Company).WithMany(p => p.ServelDeliveryEx)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServelReceiptEx>(entity =>
        {
            entity.Property(e => e.DoDate).HasColumnType("datetime");
            entity.Property(e => e.DoDateMin).HasColumnType("datetime");
            entity.Property(e => e.DoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.DoQtyCalc).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.ItemId).HasMaxLength(20);
            entity.Property(e => e.ItemName).HasMaxLength(4000);
            entity.Property(e => e.QuadranServel).HasMaxLength(20);
            entity.Property(e => e.ReceiptDate).HasColumnType("datetime");
            entity.Property(e => e.ReceiptDateRequest).HasColumnType("datetime");
            entity.Property(e => e.ReceiptDateRequestCalc).HasColumnType("datetime");
            entity.Property(e => e.ShippingDateRequest).HasColumnType("datetime");
            entity.Property(e => e.ShippingDateRequestCalc).HasColumnType("datetime");
            entity.Property(e => e.SoCreateDate).HasColumnType("datetime");
            entity.Property(e => e.SoId).HasMaxLength(20);
            entity.Property(e => e.SoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.SoQtyCalc).HasColumnType("numeric(32, 16)");

            entity.HasOne(d => d.Company).WithMany(p => p.ServelReceiptEx)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransDist>(entity =>
        {
            entity.Property(e => e.CreatedBy).HasMaxLength(60);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.DlvDateRequest).HasColumnType("datetime");
            entity.Property(e => e.DoDate).HasColumnType("datetime");
            entity.Property(e => e.DoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.ItemId).HasMaxLength(20);
            entity.Property(e => e.ItemName).HasMaxLength(70);
            entity.Property(e => e.KgPerUnit).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.RctDateRequest).HasColumnType("datetime");
            entity.Property(e => e.ReceiptDate).HasColumnType("datetime");
            entity.Property(e => e.SoCreateDate).HasColumnType("datetime");
            entity.Property(e => e.SoId).HasMaxLength(20);
            entity.Property(e => e.SoQty).HasColumnType("numeric(32, 16)");
            entity.Property(e => e.Unit).HasMaxLength(20);

            entity.HasOne(d => d.Company).WithMany(p => p.TransDist)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.Property(e => e.ProductId).HasMaxLength(100);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.DistributorProductId).HasMaxLength(100);
            entity.Property(e => e.DistributorProductName).HasMaxLength(200);
            entity.Property(e => e.ProductPackaging).HasMaxLength(100);
            entity.Property(e => e.ProductUnit).HasMaxLength(50);
            entity.Property(e => e.SmallestUnit).HasMaxLength(50);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ProductPcsInCtn).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductNetto).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.PriceUnit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SmallestUnitQty).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoAwal).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoMasukPO).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.SaldoAkhir).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.StockDate).HasColumnType("date");

            // GiselStock_* SPs filter by CompanyId and pick the latest CreatedDate batch
            entity.HasIndex(e => new { e.CompanyId, e.CreatedDate });
            entity.HasIndex(e => e.StockUploadBatchId);

            entity.HasOne(d => d.Company).WithMany(p => p.Stock)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.StockUploadBatch).WithMany(p => p.Stocks)
                .HasForeignKey(d => d.StockUploadBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ItemMapping).WithMany(p => p.Stocks)
                .HasForeignKey(d => d.ItemMappingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesTransaction>(entity =>
        {
            entity.Property(e => e.CustomerId).HasMaxLength(100);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.CustomerAlias).HasMaxLength(200);
            entity.Property(e => e.CustomerAddress).HasMaxLength(500);
            entity.Property(e => e.ProductId).HasMaxLength(100);
            entity.Property(e => e.ProductBrand).HasMaxLength(200);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.DistributorProductId).HasMaxLength(100);
            entity.Property(e => e.DistributorProductName).HasMaxLength(200);
            entity.Property(e => e.ProductPackaging).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.InvoiceNo).HasMaxLength(100);
            entity.Property(e => e.InvoiceUnit).HasMaxLength(50);
            entity.Property(e => e.GeisaPOId).HasMaxLength(100);
            entity.Property(e => e.SalesmanNameGMK).HasMaxLength(200);
            entity.Property(e => e.ProductPcsInCtn).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceQty).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GrossValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountPct).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InKg).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.ShipDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ReportingWeekStart).HasColumnType("date");

            // GiselSales_* SPs filter by CompanyId and pick the latest CreatedDate batch
            entity.HasIndex(e => new { e.CompanyId, e.CreatedDate });
            entity.HasIndex(e => new { e.CompanyId, e.ReportingWeekStart });

            entity.HasOne(d => d.Company).WithMany(p => p.SalesTransaction)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.SelloutUploadBatch).WithMany(p => p.SalesTransactions)
                .HasForeignKey(d => d.SelloutUploadBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ItemMapping).WithMany(p => p.SalesTransactions)
                .HasForeignKey(d => d.ItemMappingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        OnModelCreatingPartial(modelBuilder);
    }
}
