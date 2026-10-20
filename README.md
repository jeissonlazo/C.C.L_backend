# BodegaFlow · Gestión de inventario (API)

Aplicación web adaptable con API REST en ASP.NET Core 10, Entity Framework Core y PostgreSQL. La interfaz Angular vive en el repositorio hermano `inventario-front-end` y consume esta API por `/api`.

## Requisitos

- .NET SDK 10
- Una base PostgreSQL: por defecto la de **Neon**, o el PostgreSQL local del equipo

## Configuración local (una vez)

`appsettings.json` **no se versiona**: guarda la cadena de conexión y el secreto JWT. El repositorio trae la plantilla `appsettings.example.json`:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

Después edita en `appsettings.json`:

1. `ConnectionStrings:Inventory` con la URL de Neon (o la cadena `Host=…` de tu PostgreSQL). Deja `ConnectionStrings:Local` si quieres alternar entre bases.
2. `Jwt:Secret` con un valor aleatorio de al menos 32 bytes:

   ```powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   ```

Si falta cualquiera de los dos, la API no arranca y lo indica con un mensaje explícito.

## Inicio rápido

```powershell
dotnet restore
dotnet run --launch-profile http     # http://localhost:5070
```

Comprueba el estado en <http://localhost:5070/api/health> y la documentación interactiva en <http://localhost:5070/swagger>.

La cadena activa es `ConnectionStrings:Inventory` (formato `postgresql://…` de Neon o `Host=…`). Para usar otra base sin tocar el archivo, sobrescríbela por entorno:

```powershell
$env:ConnectionStrings__Inventory = 'Host=localhost;Port=5432;Database=gestion_inventario;Username=postgres;Password=postgres'
dotnet run --launch-profile http
```

## Base de datos

El esquema está en `database/init.sql`: tablas `users`, `products` y `inventory_movements`, más las funciones `register_inventory_movement` (transacción + `FOR UPDATE`, evita saldos negativos) y `get_current_inventory` (existencias y valor por producto activo).

Para una base nueva:

```powershell
psql "$env:ConnectionStrings__Inventory" -f database/init.sql
psql "$env:ConnectionStrings__Inventory" -f database/seed-demo.sql   # catálogo de demostración, idempotente
```

`database/seed-demo.sql` incluye la cuenta `demo@bodegaflow.dev` / `BodegaFlow2026!` y cinco productos con movimientos, para que el frontend tenga datos al abrirlo. `database/seed-fix.sql` limpia datos de prueba de la UI y ajusta el mínimo de `TAS-004` para ver el aviso de bajo stock.

### PostgreSQL local con Docker (opcional)

`docker-compose.yml` levanta un PostgreSQL 17 y aplica `database/init.sql` la primera vez:

```powershell
docker compose up -d
```

Si cambias el esquema después de crear el volumen, aplica el SQL actualizado o recrea el volumen con `docker compose down -v` (esto elimina sus datos).

## Configuración

| Clave | Uso |
| --- | --- |
| `ConnectionStrings:Inventory` | Cadena activa. Admite `Host=…` o la URL `postgresql://…` de Neon (`sslmode`, `channel_binding`, `connect_timeout`, `application_name`). Se puede sobrescribir con la variable `ConnectionStrings__Inventory`. |
| `ConnectionStrings:Local` | Cadena del PostgreSQL del equipo, como referencia para cambiar de base. |
| `Jwt:Secret` | Secreto HMAC de al menos 32 bytes. En despliegues usa `Jwt__Secret`. |
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y audiencia que se validan en cada token. |
| `Cors:AllowedOrigins` | Orígenes permitidos: `5070`, `7191`, `4200` y `127.0.0.1:4200`. |

### Archivos de configuración

| Archivo | ¿Versionado? | Contenido |
| --- | --- | --- |
| `appsettings.json` | No (`.gitignore`) | Configuración de la máquina: cadena de conexión y secreto JWT. Se crea copiando el ejemplo. |
| `appsettings.example.json` | Sí | Plantilla con los valores vacíos o de relleno. |
| `appsettings.Development.json` | Sí | Solo niveles de log; no tiene secretos. |
| `appsettings.Production.json`, `appsettings.*.local.json` | No (`.gitignore`) | Ajustes locales o de despliegue.

Al clonar el repositorio hay que ejecutar `Copy-Item appsettings.example.json appsettings.json` y rellenar `ConnectionStrings:Inventory` y `Jwt:Secret`. Si además prefieres no escribir secretos en disco, configura `ConnectionStrings__Inventory` y `Jwt__Secret` como variables de entorno y deja la plantilla tal cual (el valor de entorno tiene prioridad).

El frontend se sirve con `ng serve` en el puerto `4200` y su `proxy.conf.json` reenvía `/api` a `http://localhost:5070`, así que en desarrollo no hay CORS de por medio. La lista de orígenes queda como respaldo para llamadas directas.

## Perfiles de arranque

| Perfil | Comando | URL |
| --- | --- | --- |
| `http` | `dotnet run --launch-profile http` | <http://localhost:5070> |
| `https` | `dotnet run --launch-profile https` | <https://localhost:7191> y <http://localhost:5070> |
| `neon` | `dotnet run --launch-profile neon` | <http://localhost:5071> (entorno `Neon`, Swagger activo) |

Swagger se publica en desarrollo y en el entorno `Neon`.

## API

Todos los endpoints salvo salud y autenticación requieren `Authorization: Bearer <token>`.

| Método | Ruta | Uso |
|---|---|---|
| GET | `/api/health` | Estado del servicio |
| POST | `/api/auth/register` | Crear cuenta (`email`, `password`) |
| POST | `/api/auth/login` | Obtener token JWT |
| GET | `/api/auth/me` | Correo del token actual |
| GET | `/api/dashboard` | Resumen y alertas de stock |
| GET, POST | `/api/products` | Consultar y crear productos |
| PUT, DELETE | `/api/products/{id}` | Editar o desactivar producto |
| GET | `/api/inventory` | Existencias actuales mediante función SQL |
| GET | `/api/movements?limit=100` | Historial reciente |
| POST | `/api/movements` | Registrar entrada o salida |

Ejemplo de movimiento:

```json
{
  "productId": "uuid-del-producto",
  "type": "Entry",
  "quantity": 12,
  "note": "Recepción de proveedor"
}
```

El enumerado `type` viaja como texto (`Entry`/`Exit`) y las validaciones responden `ProblemDetails`. `DELETE /api/products/{id}` es una baja lógica (`is_active = false`).

## Seguridad

Las contraseñas se almacenan con `PasswordHasher` de ASP.NET Core Identity. Los JWT vencen en ocho horas y se firman con HMAC-SHA256.

`appsettings.json` ya no se versiona (quedó en `.gitignore`) y solo se conserva la plantilla `appsettings.example.json`. Ojo con el historial: versiones anteriores del archivo incluían un secreto JWT de ejemplo, así que **rota** `Jwt:Secret` (y cualquier credencial que haya estado en un commit) y no lo reutilices en despliegues. En producción, configura `ConnectionStrings__Inventory` y `Jwt__Secret` como variables de entorno o desde un gestor de secretos.

El registro público (`POST /api/auth/register`) crea cuentas sin restricción; protégelo o desactívalo en despliegues públicos según la política de tu organización.

