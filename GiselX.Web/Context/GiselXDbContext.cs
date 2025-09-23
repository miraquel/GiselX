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

        OnModelCreatingPartial(modelBuilder);
    }
}
