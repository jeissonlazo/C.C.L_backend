# BodegaFlow · Gestión de inventario

Aplicación web adaptable con API REST en ASP.NET Core 10, Entity Framework Core y PostgreSQL. La interfaz está servida por la misma aplicación.

## Requisitos

- .NET SDK 10
- Docker Desktop con Docker Compose para usar PostgreSQL localmente, o una base PostgreSQL accesible

## Inicio rápido

1. Levanta PostgreSQL y crea el esquema y las funciones almacenadas:

   ```powershell
   docker compose up -d
   ```

2. Restaura dependencias e inicia la aplicación:

   ```powershell
   dotnet restore
   dotnet run
   ```

3. Abre <http://localhost:5070>, crea la primera cuenta e inicia sesión.

La primera ejecución de PostgreSQL carga `database/init.sql`. Si cambias el esquema después de crear el volumen, aplica el SQL manualmente o recrea el volumen de desarrollo con `docker compose down -v` (esto elimina sus datos).

## Configuración

### Usar Neon (perfil preparado)

1. Abre `appsettings.Neon.json` y pega la URL completa de conexión PostgreSQL de Neon en `ConnectionStrings:Inventory`. La aplicación acepta el formato `postgresql://...` de Neon y traduce sus opciones `sslmode` y `channel_binding`. Este archivo está ignorado por Git para no publicar credenciales.
2. Reemplaza el secreto JWT por uno propio de al menos 32 bytes. En PowerShell puedes generar uno con:

   ```powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   ```

3. Ejecuta `database/init.sql` una vez desde el SQL Editor de Neon. También puedes ejecutarlo con `psql` usando la cadena de conexión de Neon.
4. Inicia con el perfil Neon:

   ```powershell
   dotnet run --launch-profile neon
   ```

5. Abre <http://localhost:5071/swagger> para ver y probar la API.

La cadena usa el host y la base indicados, TLS obligatorio y `Channel Binding=Require`. El texto de contraseña recibido estaba enmascarado, por eso quedó como campo para completar. La conexión local Docker sigue disponible con `dotnet run`.

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
