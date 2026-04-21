# LicitIA.Api

Backend minimo en ASP.NET Core para `login`, `registro` y oportunidades, pensado para trabajar con SQL Server desde SSMS.

## 1. Crear la base

Ejecuta el script [database/LicitIAAuthDb.sql](../database/LicitIAAuthDb.sql) en SQL Server Management Studio.

## 2. Revisar la conexion

La API usa por defecto:

```json
"Database": {
  "ConnectionString": "Server=localhost;Database=LicitIAAuthDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
}
```

Si tu instancia usa otro nombre, por ejemplo `localhost\\SQLEXPRESS`, ajusta [appsettings.json](./appsettings.json).

## 3. Ejecutar la API

Desde la carpeta raiz del proyecto:

```powershell
.\build-api.ps1
.\run-api.ps1
```

La API queda en `http://localhost:5153`.

Si prefieres usar `dotnet` manualmente, usa estas variables antes:

```powershell
$env:DOTNET_CLI_HOME="C:\Users\sprit\Documents\Codex\2026-04-20-hola\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:APPDATA="C:\Users\sprit\Documents\Codex\2026-04-20-hola\.appdata"
$env:NUGET_PACKAGES="$env:USERPROFILE\.nuget\packages"
```

## 4. Endpoints disponibles

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/opportunities`
- `GET /api/opportunities/{id}`
- `GET /api/health`
