# LicitIA

Sistema de gestión de licitaciones con IA para empresas peruanas. Analiza oportunidades del SEACE, calcula afinidad comercial y proporciona recomendaciones inteligentes.

## Características

- **Autenticación completa:** Login tradicional con email/contraseña y Google OAuth
- **Recuperación de contraseña:** Flujo completo con envío de email
- **Recordarme:** JWT con expiración extendida (30 días)
- **Email de bienvenida:** Envío automático al registro
- **Dashboard:** Estadísticas de oportunidades en tiempo real
- **Oportunidades:** Listado dinámico con filtros y búsqueda
- **Seguimiento:** Gestión de procesos y responsables

## Requisitos Previos

- .NET 8.0 SDK
- SQL Server 2019+ (o SQL Server Express)
- Node.js (opcional, para desarrollo frontend)
- Cuenta de Google (para OAuth)
- Cuenta de Gmail (para envío de emails)

## Instalación

### 1. Clonar el repositorio

```bash
git clone https://github.com/Sprite135/Capsone-Proyect.git
cd Capsone-Proyect
```

### 2. Configurar base de datos

#### Crear la base de datos en SQL Server

```sql
CREATE DATABASE LicitIAAuthDb;
GO

USE LicitIAAuthDb;
GO
```

#### Ejecutar los scripts SQL

Ejecuta el archivo `database/schema.sql` para crear las tablas necesarias:

```bash
sqlcmd -S localhost -d LicitIAAuthDb -i database/schema.sql -E
```

### 3. Configurar Google OAuth

1. Ve a [Google Cloud Console](https://console.cloud.google.com/)
2. Crea un nuevo proyecto o selecciona uno existente
3. Habilita la API de Google+ y Google Identity
4. Crea credenciales OAuth 2.0:
   - Tipo: Aplicación web
   - URI de redirección autorizada: `http://localhost:5153/api/auth/google/callback`
5. Copia el Client ID y Client Secret

### 4. Configurar SMTP Gmail

1. Habilita la autenticación de 2 pasos en tu cuenta de Gmail
2. Genera una [Contraseña de aplicación](https://myaccount.google.com/apppasswords)
3. Copia la contraseña generada (16 caracteres)

### 5. Configurar la aplicación

Copia el archivo de configuración de ejemplo:

```bash
cp LicitIA.Api/appsettings.json.example LicitIA.Api/appsettings.json
```

Edita `LicitIA.Api/appsettings.json` con tus credenciales:

```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Database=LicitIAAuthDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
  },
  "Jwt": {
    "Key": "TU_CLAVE_SECRETA_JWT_MINIMO_32_CARACTERES",
    "Issuer": "LicitIA",
    "Audience": "LicitIAUsers",
    "ExpirationMinutes": 1440,
    "RememberMeExpirationDays": 30
  },
  "GoogleAuth": {
    "ClientId": "TU_GOOGLE_CLIENT_ID",
    "ClientSecret": "TU_GOOGLE_CLIENT_SECRET",
    "RedirectUri": "http://localhost:5153/api/auth/google/callback"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUser": "tu-email@gmail.com",
    "SmtpPassword": "TU_APP_PASSWORD_DE_GMAIL",
    "FromEmail": "tu-email@gmail.com",
    "FromName": "LicitIA"
  }
}
```

### 6. Instalar dependencias y ejecutar

```bash
cd LicitIA.Api
dotnet restore
dotnet run
```

La API estará disponible en `http://localhost:5153`

### 7. Acceder a la aplicación

Abre tu navegador en:
- **Dashboard:** `http://localhost:5153/index.html`
- **Registro:** `http://localhost:5153/registro.html`
- **Login:** `http://localhost:5153/login.html`

## Estructura del Proyecto

```
LicitIA/
├── LicitIA.Api/              # Backend API en ASP.NET Core
│   ├── Configuration/       # Opciones de configuración
│   ├── Contracts/            # DTOs y requests
│   ├── Data/                 # Repositorios y acceso a datos
│   ├── Models/               # Modelos de dominio
│   ├── Security/             # Servicios de seguridad
│   ├── Services/             # Servicios (Email, etc.)
│   └── Program.cs            # Punto de entrada
├── database/                 # Scripts SQL
│   └── schema.sql            # Esquema de base de datos
├── *.html                    # Páginas frontend
├── *.css                     # Estilos
└── *.js                      # JavaScript
```

## Endpoints de la API

### Autenticación

- `POST /api/auth/register` - Registro de usuario
- `POST /api/auth/login` - Login tradicional
- `GET /api/auth/google/login` - Iniciar flujo Google OAuth
- `GET /api/auth/google/callback` - Callback de Google OAuth
- `POST /api/auth/complete-profile` - Completar perfil (usuarios de Google)
- `POST /api/auth/forgot-password` - Solicitar recuperación de contraseña
- `POST /api/auth/reset-password` - Restablecer contraseña

### Oportunidades

- `GET /api/opportunities` - Listar todas las oportunidades
- `GET /api/opportunities/{id}` - Obtener detalle de una oportunidad

## Seguridad

- **JWT Tokens:** Autenticación basada en tokens JWT
- **Hash de contraseñas:** PBKDF2 con salt
- **Google OAuth:** Flujo OAuth 2.0 seguro
- **Protección de secretos:** `appsettings.json` excluido del repositorio

## Desarrollo

### Ejecutar en modo desarrollo

```bash
cd LicitIA.Api
dotnet run --environment Development
```

### Ejecutar pruebas

```bash
dotnet test
```

## Base de Datos

### Tablas principales

- **AppUsers:** Usuarios del sistema
- **Opportunities:** Oportunidades de licitación

### Scripts SQL

Los scripts SQL están en el directorio `database/`:
- `schema.sql` - Creación de tablas y relaciones

## Troubleshooting

### Error de conexión a SQL Server

Verifica que SQL Server esté ejecutándose y que la connection string sea correcta:
```json
"ConnectionString": "Server=localhost;Database=LicitIAAuthDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

### Error de Google OAuth

- Verifica que el Client ID y Client Secret sean correctos
- Confirma que el Redirect URI coincida en Google Console y appsettings.json
- Asegúrate de que la pantalla de consentimiento esté configurada

### Error de envío de emails

- Verifica que la contraseña de aplicación de Gmail sea correcta
- Confirma que la autenticación de 2 pasos esté habilitada
- Revisa el puerto SMTP (587 para TLS)

## Contribución

Este es un proyecto académico de capstone. Para contribuir:
1. Fork el repositorio
2. Crea una rama para tu feature
3. Commit tus cambios
4. Push a la rama
5. Abre un Pull Request

## Licencia

Este proyecto es propiedad de Emdersoft S.A.C.

## Contacto

- **Empresa:** Emdersoft S.A.C.
- **Proyecto:** LicitIA
- **Año:** 2024
