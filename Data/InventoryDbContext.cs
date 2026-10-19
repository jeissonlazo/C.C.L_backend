using gestion_inventario.Models;
using Microsoft.EntityFrameworkCore;

namespace gestion_inventario.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryMovement> Movements => Set<InventoryMovement>();
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            e.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
            e.Property(x => x.MinimumStock).HasColumnName("minimum_stock"); e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at"); e.HasIndex(x => x.Sku).IsUnique();
        });
        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("inventory_movements"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id"); e.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Quantity).HasColumnName("quantity"); e.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            e.Property(x => x.UserId).HasColumnName("user_id"); e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Product).WithMany(x => x.Movements).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CreatedAt); e.HasIndex(x => new { x.ProductId, x.CreatedAt });
        });
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("users"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at"); e.HasIndex(x => x.Email).IsUnique();
        });
    }
}
