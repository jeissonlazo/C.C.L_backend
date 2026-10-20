using gestion_inventario.Models;
using Microsoft.EntityFrameworkCore;

namespace gestion_inventario.Data;

public sealed class InventoryService(InventoryDbContext db)
{
    public Task<List<StockDto>> GetStockAsync(CancellationToken ct) => db.Database
        .SqlQuery<StockDto>($"SELECT product_id AS \"ProductId\", sku AS \"Sku\", name AS \"Name\", quantity AS \"Quantity\", minimum_stock AS \"MinimumStock\", unit_price AS \"UnitPrice\", stock_value AS \"StockValue\" FROM get_current_inventory()").ToListAsync(ct);

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var stock = await GetStockAsync(ct);
        var lowStock = stock.Where(p => p.Quantity <= p.MinimumStock).OrderBy(p => p.Quantity).ToList();
        return new DashboardDto(stock.Count, stock.Sum(p => p.Quantity), stock.Sum(p => p.StockValue), lowStock.Count, lowStock.Take(5).ToList());
    }

    public async Task<MovementDto> RegisterMovementAsync(MovementRequest request, Guid userId, CancellationToken ct)
    {
        if (request.Note?.Length > 500) throw new InvalidOperationException("La nota admite máximo 500 caracteres.");
        var type = request.Type == MovementType.Entry ? "entry" : "exit";
        var rows = await db.Database.SqlQuery<MovementResult>($"SELECT id AS \"Id\", product_id AS \"ProductId\", product_name AS \"ProductName\", sku AS \"Sku\", type AS \"Type\", quantity AS \"Quantity\", note AS \"Note\", created_at AS \"CreatedAt\", error AS \"Error\" FROM register_inventory_movement({request.ProductId}, CAST({type} AS varchar), {request.Quantity}, {userId}, {request.Note})").ToListAsync(ct);
        if (rows.Count == 0) throw new KeyNotFoundException("No se encontró el producto o está inactivo.");
        if (rows[0].Error is not null) throw new InvalidOperationException(rows[0].Error);
        var result = rows[0];
        return new MovementDto(result.Id!.Value, result.ProductId!.Value, result.ProductName ?? "", result.Sku ?? "", result.Type == "entry" ? MovementType.Entry : MovementType.Exit, result.Quantity ?? 0, result.Note, result.CreatedAt ?? DateTime.UtcNow);
    }

    // La función devuelve una fila con id NULL cuando rechaza la operación (por ejemplo,
    // existencias insuficientes), por eso las columnas se mapean como nullable.
    private sealed class MovementResult
    {
        public Guid? Id { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
        public string? Type { get; set; }
        public int? Quantity { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Error { get; set; }
    }
}
