namespace gestion_inventario.Models;

public enum MovementType { Entry = 1, Exit = 2 }

public sealed class Product
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public int MinimumStock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

public sealed class InventoryMovement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public MovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record ProductRequest(string Sku, string Name, string? Description, decimal UnitPrice, int MinimumStock);
public sealed record MovementRequest(Guid ProductId, MovementType Type, int Quantity, string? Note);
public sealed record ProductDto(Guid Id, string Sku, string Name, string? Description, decimal UnitPrice, int MinimumStock, bool IsActive, DateTime CreatedAt);
public sealed record MovementDto(Guid Id, Guid ProductId, string ProductName, string Sku, MovementType Type, int Quantity, string? Note, DateTime CreatedAt);
public sealed record StockDto(Guid ProductId, string Sku, string Name, long Quantity, int MinimumStock, decimal UnitPrice, decimal StockValue);
public sealed record DashboardDto(int ProductCount, long TotalUnits, decimal InventoryValue, int LowStockCount, IReadOnlyList<StockDto> LowStockProducts);
