using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using gestion_inventario;
using gestion_inventario.Data;
using gestion_inventario.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var configuredConnectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("Configura ConnectionStrings:Inventory.");
var connectionString = PostgresConnectionString.Normalize(configuredConnectionString);
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Configura Jwt:Secret con al menos 32 bytes (usa una variable de entorno en producción).");

builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "BodegaFlow API", Version = "v1", Description = "API para productos y movimientos de inventario." });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header, Description = "Pega el accessToken recibido en /api/auth/login."
    });
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5070", "https://localhost:7191"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "GestionInventario",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "GestionInventario.Client",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(error, "Error no controlado atendiendo {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await Results.Problem("Ocurrió un error inesperado. Intenta de nuevo.", statusCode: 500).ExecuteAsync(context);
}));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Neon"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "BodegaFlow API v1"));
}
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "gestion-inventario" })).AllowAnonymous();
app.MapPost("/api/auth/register", async (RegisterRequest request, InventoryDbContext db, IPasswordHasher<UserAccount> hasher) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    if (email.Length > 200 || !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Ingresa un correo válido."] });
    if (request.Password.Length is < 10 or > 128)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["La contraseña debe tener entre 10 y 128 caracteres."] });
    if (await db.Users.AnyAsync(x => x.Email == email)) return Results.Conflict(new { message = "Ya existe una cuenta con ese correo." });
    var user = new UserAccount { Email = email, CreatedAt = DateTime.UtcNow };
    user.PasswordHash = hasher.HashPassword(user, request.Password);
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created("/api/auth/me", new { user.Id, user.Email });
}).AllowAnonymous();

app.MapPost("/api/auth/login", async (LoginRequest request, InventoryDbContext db, IPasswordHasher<UserAccount> hasher, IConfiguration config) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
    if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    var issuer = config["Jwt:Issuer"] ?? "GestionInventario";
    var audience = config["Jwt:Audience"] ?? "GestionInventario.Client";
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddHours(8);
    var token = new JwtSecurityToken(issuer, audience, [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email)], expires: expires, signingCredentials: credentials);
    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = expires, user = new { user.Id, user.Email } });
}).AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/auth/me", (ClaimsPrincipal principal) => Results.Ok(new { email = principal.FindFirstValue(ClaimTypes.Email) }));
api.MapGet("/dashboard", async (InventoryService inventory, CancellationToken ct) => Results.Ok(await inventory.GetDashboardAsync(ct)));
api.MapGet("/products", async (string? search, InventoryDbContext db, CancellationToken ct) =>
{
    var query = db.Products.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%") || EF.Functions.ILike(p.Sku, $"%{search.Trim()}%"));
    return Results.Ok(await query.OrderBy(p => p.Name).Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.UnitPrice, p.MinimumStock, p.IsActive, p.CreatedAt)).ToListAsync(ct));
});
api.MapPost("/products", async (ProductRequest request, InventoryDbContext db, CancellationToken ct) =>
{
    var error = ValidateProduct(request);
    if (error is not null) return Results.ValidationProblem(error);
    var sku = request.Sku.Trim().ToUpperInvariant();
    if (await db.Products.AnyAsync(p => p.Sku == sku, ct)) return Results.Conflict(new { message = "El SKU ya está registrado." });
    var product = new Product { Sku = sku, Name = request.Name.Trim(), Description = request.Description?.Trim(), UnitPrice = request.UnitPrice, MinimumStock = request.MinimumStock, CreatedAt = DateTime.UtcNow };
    db.Products.Add(product);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/products/{product.Id}", new ProductDto(product.Id, product.Sku, product.Name, product.Description, product.UnitPrice, product.MinimumStock, product.IsActive, product.CreatedAt));
});
api.MapPut("/products/{id:guid}", async (Guid id, ProductRequest request, InventoryDbContext db, CancellationToken ct) =>
{
    var error = ValidateProduct(request);
    if (error is not null) return Results.ValidationProblem(error);
    var product = await db.Products.FindAsync([id], ct);
    if (product is null) return Results.NotFound();
    var sku = request.Sku.Trim().ToUpperInvariant();
    if (await db.Products.AnyAsync(p => p.Id != id && p.Sku == sku, ct)) return Results.Conflict(new { message = "El SKU ya está registrado." });
    product.Sku = sku; product.Name = request.Name.Trim(); product.Description = request.Description?.Trim(); product.UnitPrice = request.UnitPrice; product.MinimumStock = request.MinimumStock;
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
api.MapDelete("/products/{id:guid}", async (Guid id, InventoryDbContext db, CancellationToken ct) =>
{
    var product = await db.Products.FindAsync([id], ct);
    if (product is null) return Results.NotFound();
    product.IsActive = false;
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
api.MapGet("/inventory", async (InventoryService inventory, CancellationToken ct) => Results.Ok(await inventory.GetStockAsync(ct)));
api.MapGet("/movements", async (int? limit, InventoryDbContext db, CancellationToken ct) => Results.Ok(await db.Movements.AsNoTracking().OrderByDescending(m => m.CreatedAt).Take(Math.Clamp(limit ?? 100, 1, 500)).Select(m => new MovementDto(m.Id, m.ProductId, m.Product!.Name, m.Product.Sku, m.Type, m.Quantity, m.Note, m.CreatedAt)).ToListAsync(ct)));
api.MapPost("/movements", async (MovementRequest request, ClaimsPrincipal principal, InventoryService inventory, CancellationToken ct) =>
{
    if (request.Quantity <= 0 || request.Quantity > 1_000_000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["quantity"] = ["La cantidad debe ser mayor que cero y no superar 1.000.000."] });
    if (request.Type is not (MovementType.Entry or MovementType.Exit)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["El tipo debe ser Entry o Exit."] });
    try
    {
        var movement = await inventory.RegisterMovementAsync(request, Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return Results.Created($"/api/movements/{movement.Id}", movement);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
});

app.Run();

static Dictionary<string, string[]>? ValidateProduct(ProductRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Sku) || request.Sku.Trim().Length > 40) errors["sku"] = ["El SKU es obligatorio y debe tener máximo 40 caracteres."];
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) errors["name"] = ["El nombre es obligatorio y debe tener máximo 160 caracteres."];
    if (request.Description?.Length > 500) errors["description"] = ["La descripción admite máximo 500 caracteres."];
    if (request.UnitPrice < 0 || request.UnitPrice > 999999999) errors["unitPrice"] = ["El precio debe estar entre 0 y 999.999.999."];
    if (request.MinimumStock < 0 || request.MinimumStock > 1_000_000) errors["minimumStock"] = ["El mínimo debe estar entre 0 y 1.000.000."];
    return errors.Count == 0 ? null : errors;
}
