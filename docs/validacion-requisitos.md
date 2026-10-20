# Validación de requisitos · Backend BodegaFlow

Auditoría del backend `gestion_inventario` contra los cuatro requisitos del enunciado. Cada punto se
comprobó en el código y en la instancia real en ejecución (API en `http://localhost:5070`, base
PostgreSQL 18.6 de Neon, .NET SDK 10.0.302, EF Core 10.0.10, Npgsql 10.0.0).

| # | Requisito | Estado |
| --- | --- | --- |
| 1 | Base de datos PostgreSQL para productos y movimientos de inventario | ✅ Cumple |
| 2 | API RESTful en C# para registrar entradas/salidas y consultar el inventario actual | ✅ Cumple |
| 3 | Entity Framework (o Dapper) para interactuar con la base de datos | ✅ Cumple |
| 4 | Stored procedures en PostgreSQL para registrar movimientos y calcular el inventario | ✅ Cumple, con matiz (son `FUNCTION`, no `PROCEDURE`) |

---

## 1. Base de datos PostgreSQL con productos y movimientos

**Implementación.** El esquema está versionado en [`database/init.sql`](../database/init.sql) y
aplicado en la base `neondb`:

- `products` — catálogo: `id` uuid, `sku` único, `name`, `description`, `unit_price`,
  `minimum_stock`, `is_active`, `created_at`.
- `inventory_movements` — movimientos: `id` uuid, `product_id` (FK → `products`), `type`
  (`entry`/`exit`), `quantity`, `note`, `user_id` (FK → `users`), `created_at`.
- `users` — cuentas para la autenticación JWT (tabla adicional necesaria para el API).
- Restricciones en la base: `CHECK (type IN ('entry','exit'))`, `CHECK (quantity > 0)`,
  `CHECK (unit_price >= 0)`, `CHECK (minimum_stock >= 0)`, `UNIQUE (sku)`, `UNIQUE (email)` e
  índices sobre `created_at` y `(product_id, created_at)`.
- `CREATE EXTENSION IF NOT EXISTS pgcrypto` aporta `gen_random_uuid()` para las claves.

**Evidencia ejecutada** (`psql` contra la base `neondb`):

```
     table_name      |  columnas | restricciones
---------------------+-----------+-----------------------------------
 inventory_movements |     7     | PK, FK product_id, FK user_id, CHECK type, CHECK quantity
 products            |     8     | PK, UNIQUE sku, CHECK unit_price, CHECK minimum_stock
 users               |     4     | PK, UNIQUE email
```

```
      tablename        |            indexname
-----------------------+---------------------------------
 inventory_movements   | ix_inventory_movements_created_at
 inventory_movements   | ix_inventory_movements_product_created
 inventory_movements   | inventory_movements_pkey
 products              | products_sku_key
 products              | products_pkey
 users                 | users_email_key
```

**Conclusión:** la base existe, es PostgreSQL y guarda productos y movimientos con integridad
referencial y validaciones a nivel de esquema.

---

## 2. API RESTful en C# para entradas, salidas y consulta de inventario

**Implementación.** APIs mínimas de ASP.NET Core en [`Program.cs`](../Program.cs), con autenticación
JWT y autorización por token en todo el grupo `/api`:

| Método | Ruta | Línea | Función |
| --- | --- | --- | --- |
| GET | `/api/health` | 78 | Estado del servicio (anónimo) |
| POST | `/api/auth/register` | 79 | Alta de cuenta |
| POST | `/api/auth/login` | 94 | Token JWT |
| GET | `/api/dashboard` | 111 | Resumen y alertas |
| GET, POST | `/api/products` | 112, 118 | Catálogo |
| PUT, DELETE | `/api/products/{id}` | 129, 141 | Edición / baja lógica |
| GET | `/api/inventory` | 149 | **Inventario actual** |
| GET | `/api/movements` | 150 | Historial |
| POST | `/api/movements` | 151 | **Registrar entrada o salida** |

**Evidencia ejecutada contra el API en marcha** (cuenta `demo@bodegaflow.dev`):

```
GET  /api/inventory            -> 200, 4 filas activas, TAS-004 con 7 unidades
POST /api/movements {type:Entry, quantity:1} -> 201, id devuelto
POST /api/movements {type:Exit,  quantity:1} -> 201, id devuelto
GET  /api/inventory            -> 200, TAS-004 vuelve a 7 unidades (entrada y salida aplicadas)
POST /api/movements {type:Exit,  quantity:999999} -> 400 «Existencias insuficientes para realizar la salida.»
POST /api/auth/login           -> 200, accessToken JWT de 452 caracteres
```

Los movimientos de prueba se eliminaron después de la validación; la base quedó con su catálogo
original (5 productos y 11 movimientos) y las cuentas de usuario intactas.

**Conclusión:** sí existe una API REST en C# que registra entradas y salidas y consulta el inventario
actual; además valida stock insuficiente, tipo de movimiento, cantidades y SKU duplicado.

---

## 3. Entity Framework para interactuar con la base de datos

**Implementación.** EF Core 10 con el proveedor `Npgsql.EntityFrameworkCore.PostgreSQL` 10:

- Registro: `builder.Services.AddDbContext<InventoryDbContext>(o => o.UseNpgsql(connectionString))`
  ([`Program.cs`](../Program.cs) línea 26).
- Modelo: [`Data/InventoryDbContext.cs`](../Data/InventoryDbContext.cs) define `DbSet<Product>`,
  `DbSet<InventoryMovement>` y `DbSet<UserAccount>`, con mapeo explícito de tablas y columnas
  (`e.ToTable("products")`, `e.Property(x => x.Sku).HasColumnName("sku")…`), claves, relaciones,
  índices y precisión decimal.
- Consultas LINQ: productos (`db.Products.AsNoTracking()…Where(ILike)…OrderBy…Select`), usuarios
  (`db.Users.SingleOrDefaultAsync`), movimientos (`db.Movements…OrderByDescending…Take`), altas y
  bajas con `SaveChangesAsync`.
- Las funciones de PostgreSQL se invocan a través de EF con `db.Database.SqlQuery<T>(...)`
  ([`Data/InventoryService.cs`](../Data/InventoryService.cs) líneas 8-9 y 22).

**Evidencia ejecutada.** El log de EF (`Microsoft.EntityFrameworkCore.Database.Command`) muestra el
SQL que genera y ejecuta la aplicación:

```
SELECT u.id, u.created_at, u.email, u.password_hash
FROM users AS u WHERE u.email = @email LIMIT 2                -- LINQ: Users.SingleOrDefaultAsync

SELECT product_id AS "ProductId", sku AS "Sku", name AS "Name", quantity AS "Quantity",
       minimum_stock AS "MinimumStock", unit_price AS "UnitPrice", stock_value AS "StockValue"
FROM get_current_inventory()                                   -- Database.SqlQuery<StockDto>

SELECT id AS "Id", product_id AS "ProductId", product_name AS "ProductName", sku AS "Sku",
       type AS "Type", quantity AS "Quantity", note AS "Note", created_at AS "CreatedAt", error AS "Error"
FROM register_inventory_movement(@p0, CAST(@p1 AS varchar), @p2, @p3, @p4)
                                                               -- Database.SqlQuery<MovementResult>
```

**Conclusión:** EF Core es la única vía de acceso a datos; no hay ADO.NET crudo ni SQL concatenado
(las funciones se invocan con parámetros, no con interpolación de cadenas).

**Nota:** el esquema se crea con `database/init.sql`, no con migraciones de EF. El requisito no exige
migraciones; si la rúbrica las pide, habría que convertir `init.sql` a `dotnet ef migrations`.

---

## 4. Stored procedures para registrar movimientos y calcular el inventario

**Implementación.** Dos rutinas definidas en [`database/init.sql`](../database/init.sql) y presentes
en la base:

| Rutina | Lenguaje | Qué hace |
| --- | --- | --- |
| `register_inventory_movement(p_product_id, p_type, p_quantity, p_user_id, p_note)` | `plpgsql` | Valida tipo y cantidad, bloquea el producto con `FOR UPDATE`, calcula el saldo, rechaza salidas sin stock y, si todo está bien, inserta el movimiento y devuelve la fila creada. |
| `get_current_inventory()` | `sql` (STABLE) | Calcula por producto activo: suma de entradas menos salidas, stock mínimo, precio y valor del inventario. |

La lógica de negocio del inventario vive en la base: el API no suma ni resta existencias en C#, solo
invoca las funciones y traduce su resultado a HTTP.

```
proname                     | prokind | lanname | argumentos
----------------------------+---------+---------+-------------------------------------------
get_current_inventory       | f (fn)  | sql     | (sin argumentos)
register_inventory_movement | f (fn)  | plpgsql | p_product_id uuid, p_type varchar, ...
```

**Evidencia ejecutada.** Cada llamada al API disparó la función correspondiente (log de EF, ver
sección 3):

- `POST /api/movements` → `SELECT … FROM register_inventory_movement(@p0, …)` (3 veces: una salida
  rechazada, una entrada y una salida válidas).
- `GET /api/inventory` → `SELECT … FROM get_current_inventory()`.
- La salida de 999.999 unidades devolvió `400` con el mensaje generado **dentro** de la función y no
  insertó ninguna fila (el conteo de movimientos quedó igual).

**Matiz técnico.** En PostgreSQL una *stored procedure* es `CREATE PROCEDURE` (`prokind = 'p'`), y no
puede devolver un conjunto de filas; para devolver resultados se usan funciones (`prokind = 'f'`),
que es lo que hace este proyecto. Es la forma idiomática de implementar el requisito «procedimientos
almacenados que registren movimientos y calculen el inventario» y es lo que se invoca con `SELECT`.
Si la rúbrica exige literalmente `CREATE PROCEDURE`, basta añadir un envoltorio:

```sql
CREATE OR REPLACE PROCEDURE registrar_movimiento(
    p_product_id uuid, p_type varchar, p_quantity integer, p_user_id uuid, p_note varchar DEFAULT NULL)
LANGUAGE plpgsql AS $$
BEGIN
    -- Procedimiento: ejecuta la función y descarta el resultado (o lo escribe en una tabla de salida).
    PERFORM * FROM register_inventory_movement(p_product_id, p_type, p_quantity, p_user_id, p_note);
END;
$$;
```

El cálculo del inventario seguiría siendo una función, porque un `PROCEDURE` no puede devolver la
consulta que consume el API.

---

## Hallazgos menores (no afectan los requisitos)

| Hallazgo | Impacto | Sugerencia |
| --- | --- | --- |
| Un cuerpo JSON malformado responde `500` en lugar de `400` (el `UseExceptionHandler` convierte toda excepción en 500, incluida `BadHttpRequestException`). | Bajo, solo clientes con peticiones inválidas. | En el manejador, usar `error is BadHttpRequestException bad ? bad.StatusCode : 500`. |
| El esquema se aplica a mano con `init.sql`; no hay migraciones. | Medio si el esquema evoluciona. | Adoptar `dotnet ef migrations` o documentar el versionado del SQL. |
| `get_current_inventory()` no admite filtro por producto ni devuelve inactivos. | Ninguno para el alcance actual. | Añadir parámetro opcional `p_product_id uuid DEFAULT NULL`. |
| `register_inventory_movement` devuelve el error como texto en una columna (`error`), en vez de lanzar excepción. | Ninguno; simplifica el manejo en el API. | Mantener: evita transacciones abortadas y permite mensajes claros. |
| El registro público no tiene límite de intentos. | Bajo en local. | Rate limiting (`builder.Services.AddRateLimiter`). |

## Cómo reproducir esta validación

```powershell
# 1) Esquema y objetos de la base
psql "$env:ConnectionStrings__Inventory" -c "\dt"
psql "$env:ConnectionStrings__Inventory" -c "SELECT proname, prokind, lanname FROM pg_proc WHERE proname LIKE '%inventory%';"

# 2) API en marcha
cd D:\projectos\bodegapp\gestion_inventario
dotnet run --launch-profile http

# 3) Endpoints (token de la cuenta demo)
$token = (Invoke-RestMethod http://localhost:5070/api/auth/login -Method Post -ContentType 'application/json' `
    -Body (@{ email='demo@bodegaflow.dev'; password='BodegaFlow2026!' } | ConvertTo-Json)).accessToken
$h = @{ Authorization = "Bearer $token" }
Invoke-RestMethod http://localhost:5070/api/inventory -Headers $h | Format-Table
```

Registrar un movimiento requiere enviar el JSON en UTF-8 (ver «Problemas frecuentes» del README raíz).
