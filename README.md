# BodegaFlow · Gestión de inventario

Aplicación web adaptable con API REST en ASP.NET Core 10, Entity Framework Core y PostgreSQL. La interfaz está servida por la misma aplicación.

## Requisitos

- .NET SDK 10
- Docker Desktop con Docker Compose para usar PostgreSQL localmente, o una base PostgreSQL accesible

## Inicio rápido

1. `appsettings.json` apunta a un PostgreSQL local en `localhost:5432`, base `gestion_inventario` (usuario y contraseña `postgres`). Necesitas un servidor PostgreSQL escuchando en ese puerto: el servicio instalado en el equipo, o Docker con `docker compose up -d`, que crea la base y aplica `database/init.sql` la primera vez.
2. Restaura dependencias e inicia la aplicación:

   ```powershell
   dotnet restore
   dotnet run
   ```

3. Abre <http://localhost:5070>, crea la primera cuenta e inicia sesión.

Para una base nueva, ejecuta `database/init.sql` una vez. Si cambias el esquema después de crear la base local, aplica el SQL actualizado o recrea el volumen de desarrollo con `docker compose down -v` (esto elimina sus datos).

## Configuración

### Usar Neon (perfil preparado)

1. Coloca la URL completa de conexión PostgreSQL de Neon en `ConnectionStrings:Inventory` dentro de `appsettings.json` (o en la variable de entorno `ConnectionStrings__Inventory`). La aplicación acepta el formato `postgresql://...` de Neon y traduce sus opciones `sslmode` y `channel_binding`.
2. Configura un secreto JWT propio de al menos 32 bytes. En PowerShell puedes generar uno con:

   ```powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   ```

3. Ejecuta `database/init.sql` una vez desde el SQL Editor de Neon. También puedes ejecutarlo con `psql` usando la cadena de conexión de Neon.
4. Inicia con el perfil Neon:

   ```powershell
   dotnet run --launch-profile neon
   ```

5. Abre <http://localhost:5071/swagger> para ver y probar la API.

La cadena usa el host y la base indicados, TLS obligatorio y `Channel Binding=Require`. `appsettings.json` contiene credenciales de conexión; mantenlo fuera de commits públicos. Para desplegar la aplicación, inyecta los secretos mediante variables de entorno o un gestor de secretos en lugar de incluirlos en el archivo.

En despliegues configura `ConnectionStrings__Inventory` y `Jwt__Secret` mediante variables de entorno o un gestor de secretos. Genera un secreto aleatorio de al menos 32 bytes; no uses la clave de ejemplo en producción. Configura `Cors__AllowedOrigins` con los orígenes del frontend si se publica por separado.

## API

Todos los endpoints salvo salud y autenticación requieren `Authorization: Bearer <token>`.

| Método | Ruta | Uso |
|---|---|---|
| GET | `/api/health` | Estado del servicio |
| POST | `/api/auth/register` | Crear cuenta (`email`, `password`) |
| POST | `/api/auth/login` | Obtener token JWT |
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

Las funciones PostgreSQL `register_inventory_movement` y `get_current_inventory` aplican y consultan el stock. La primera bloquea el producto al validar una salida y guardar el movimiento, evitando saldos negativos por operaciones simultáneas. Swagger UI está disponible en desarrollo en `/swagger` e incluye autenticación Bearer para probar los endpoints protegidos.

## Seguridad

Las contraseñas se almacenan con `PasswordHasher` de ASP.NET Core Identity. Los JWT vencen en ocho horas y se firman con HMAC-SHA256. El registro público crea cuentas para una instalación nueva; protege el alta de usuarios en despliegues públicos según la política de tu organización.
