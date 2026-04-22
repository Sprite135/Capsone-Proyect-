-- Script de creación de base de datos para LicitIA
-- Ejecutar en SQL Server: sqlcmd -S localhost -d LicitIAAuthDb -i database/schema.sql -E

-- Crear tabla AppUsers
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppUsers')
BEGIN
    CREATE TABLE dbo.AppUsers (
        UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        FullName NVARCHAR(100) NOT NULL,
        CompanyName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(256) NOT NULL UNIQUE,
        RoleName NVARCHAR(50) NOT NULL,
        PasswordHash VARBINARY(MAX) NOT NULL,
        PasswordSalt VARBINARY(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        PasswordResetToken NVARCHAR(50) NULL,
        PasswordResetTokenExpiryUtc DATETIME2 NULL
    );
    
    CREATE INDEX IX_AppUsers_Email ON dbo.AppUsers(Email);
    PRINT 'Tabla AppUsers creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'Tabla AppUsers ya existe.';
END

-- Crear tabla Opportunities
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Opportunities')
BEGIN
    CREATE TABLE dbo.Opportunities (
        OpportunityId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessCode NVARCHAR(50) NOT NULL,
        Title NVARCHAR(500) NOT NULL,
        EntityName NVARCHAR(200) NOT NULL,
        EstimatedAmount DECIMAL(18,2) NOT NULL,
        ClosingDate DATETIME2 NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Modality NVARCHAR(100) NOT NULL,
        MatchScore INT NOT NULL,
        Summary NVARCHAR(2000) NOT NULL,
        Location NVARCHAR(100) NOT NULL,
        IsPriority BIT NOT NULL DEFAULT 0
    );
    
    CREATE INDEX IX_Opportunities_MatchScore ON dbo.Opportunities(MatchScore DESC);
    CREATE INDEX IX_Opportunities_ClosingDate ON dbo.Opportunities(ClosingDate ASC);
    PRINT 'Tabla Opportunities creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'Tabla Opportunities ya existe.';
END

-- Insertar datos de ejemplo en Opportunities
IF NOT EXISTS (SELECT * FROM dbo.Opportunities)
BEGIN
    INSERT INTO dbo.Opportunities (ProcessCode, Title, EntityName, EstimatedAmount, ClosingDate, Category, Modality, MatchScore, Summary, Location, IsPriority)
    VALUES
    ('LIC-2024-001', 'Servicios de desarrollo de software', 'MINEDU', 250000, DATEADD(DAY, 15, GETUTCDATE()), 'Software', 'Licitación Pública', 92, 'Desarrollo de plataforma educativa con módulos de gestión académica y seguimiento de estudiantes.', 'Lima', 1),
    ('LIC-2024-002', 'Transformación digital de procesos', 'SUNAT', 450000, DATEADD(DAY, 20, GETUTCDATE()), 'Transformación digital', 'Concurso Público', 85, 'Migración de sistemas legados a arquitectura cloud con implementación de microservicios.', 'Lima', 1),
    ('LIC-2024-003', 'Mesa de ayuda y soporte técnico', 'ESSALUD', 180000, DATEADD(DAY, 10, GETUTCDATE()), 'Mesa de ayuda', 'Licitación Pública', 78, 'Servicio de mesa de ayuda 24/7 para atención de afiliados y prestadores de salud.', 'Lima', 0),
    ('LIC-2024-004', 'Infraestructura de red y seguridad', 'MTC', 320000, DATEADD(DAY, 25, GETUTCDATE()), 'Software', 'Concurso Público', 88, 'Implementación de red segura con firewall, VPN y monitoreo de seguridad.', 'Lima', 0);
    
    PRINT 'Datos de ejemplo insertados en Opportunities.';
END
ELSE
BEGIN
    PRINT 'La tabla Opportunities ya tiene datos.';
END

PRINT '';
PRINT '========================================';
PRINT 'Base de datos configurada exitosamente.';
PRINT '========================================';
