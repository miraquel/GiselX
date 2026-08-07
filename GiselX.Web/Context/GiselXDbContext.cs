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

    public virtual DbSet<ServelDeliveryEx> ServelDeliveryEx { get; set; }

    public virtual DbSet<ServelReceiptEx> ServelReceiptEx { get; set; }

    public virtual DbSet<TransDist> TransDist { get; set; }

    public virtual DbSet<Stock> Stock { get; set; }

    public virtual DbSet<SalesTransaction> SalesTransaction { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set Identity schema for ASP.NET Identity tables
        modelBuilder.Entity<AppIdentityUser>().ToTable("AspNetUsers", "identity");
        modelBuilder.Entity<AppIdentityRole>().ToTable("AspNetRoles", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("AspNetUserRoles", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("AspNetUserClaims", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("AspNetUserLogins", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", "identity");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("AspNetUserTokens", "identity");

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(100);

            // New reminder fields
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.ReminderLeadDays).HasDefaultValue(3);
            entity.Property(e => e.DeadlineDaysOfWeek).HasConversion<int?>();
            // DeadlineDayOfMonth is int? — EF maps to INT NULL automatically
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
            entity.Property(e => e.ProductPackaging).HasMaxLength(100);
            entity.Property(e => e.ProductUnit).HasMaxLength(50);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.ProductNetto).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SaldoAwal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SaldoMasukPO).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SaldoAkhir).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");

            // GiselStock_* SPs filter by CompanyId and pick the latest CreatedDate batch
            entity.HasIndex(e => new { e.CompanyId, e.CreatedDate });

            entity.HasOne(d => d.Company).WithMany(p => p.Stock)
                .HasForeignKey(d => d.CompanyId)
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
            entity.Property(e => e.ProductPackaging).HasMaxLength(100);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.InvoiceNo).HasMaxLength(100);
            entity.Property(e => e.InvoiceUnit).HasMaxLength(50);
            entity.Property(e => e.GeisaPOId).HasMaxLength(100);
            entity.Property(e => e.SalesmanNameGMK).HasMaxLength(200);
            entity.Property(e => e.InvoiceQty).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GrossValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DiscountPct).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InKg).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpiredDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.ShipDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");

            // GiselSales_* SPs filter by CompanyId and pick the latest CreatedDate batch
            entity.HasIndex(e => new { e.CompanyId, e.CreatedDate });

            entity.HasOne(d => d.Company).WithMany(p => p.SalesTransaction)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        OnModelCreatingPartial(modelBuilder);
    }
}
