-- ==========================================
-- LicitIA - Base de Datos Completa
-- ==========================================
-- Script para crear toda la estructura de la base de datos
-- Ejecutar en SQL Server: sqlcmd -S localhost -d LicitIAAuthDb -i database/complete_database_schema.sql -E

-- ==========================================
-- 1. Crear Base de Datos Principal
-- ==========================================

IF DB_ID('LicitIAAuthDb') IS NULL
BEGIN
    CREATE DATABASE LicitIAAuthDb;
END;
GO

USE LicitIAAuthDb;
GO

-- ==========================================
-- 2. Tabla de Usuarios (AppUsers)
-- ==========================================

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        CompanyName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        RoleName NVARCHAR(50) NOT NULL,
        PasswordHash VARBINARY(64) NOT NULL,
        PasswordSalt VARBINARY(32) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AppUsers_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AppUsers_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

-- Índice único para email
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_AppUsers_Email'
      AND object_id = OBJECT_ID(N'dbo.AppUsers')
)
BEGIN
    CREATE UNIQUE INDEX UX_AppUsers_Email
        ON dbo.AppUsers (Email);
END;
GO

-- ==========================================
-- 3. Tabla de Perfiles de Empresa (CompanyProfile)
-- ==========================================

IF OBJECT_ID(N'dbo.CompanyProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyProfile
    (
        ProfileId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        PreferredCategories NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        PreferredLocations NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        PreferredModalities NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        MinAmount DECIMAL(18,2) NOT NULL DEFAULT 10000,
        MaxAmount DECIMAL(18,2) NOT NULL DEFAULT 500000,
        IdealAmount DECIMAL(18,2) NOT NULL DEFAULT 250000,
        FavoriteEntities NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        ExcludedEntities NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        PreferredKeywords NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        ExcludedKeywords NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        SeaceObjectDescription NVARCHAR(500) NOT NULL DEFAULT '',
        SeaceCallYear INT NOT NULL DEFAULT (YEAR(GETDATE())),
        SeaceContractObject NVARCHAR(50) NOT NULL DEFAULT '',
        MinDaysToClose INT NOT NULL DEFAULT 3,
        MaxDaysToClose INT NOT NULL DEFAULT 30,
        IdealDaysToClose INT NOT NULL DEFAULT 15,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_CompanyProfile_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_CompanyProfile_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        
        FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(UserId)
    );
END;
GO

-- Índices para CompanyProfile
CREATE INDEX IX_CompanyProfile_UserId ON dbo.CompanyProfile(UserId);
CREATE INDEX IX_CompanyProfile_CreatedAtUtc ON dbo.CompanyProfile(CreatedAtUtc);
GO

-- ==========================================
-- 4. Tabla de Oportunidades (Opportunities)
-- ==========================================

IF OBJECT_ID(N'dbo.Opportunities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Opportunities
    (
        OpportunityId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProcessCode NVARCHAR(50) NOT NULL,
        Title NVARCHAR(250) NOT NULL,
        EntityName NVARCHAR(200) NOT NULL,
        EstimatedAmount DECIMAL(18,2) NOT NULL,
        ClosingDate DATE NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Modality NVARCHAR(100) NOT NULL,
        MatchScore INT NOT NULL DEFAULT 0,
        MatchedKeywordsCount INT NOT NULL CONSTRAINT DF_Opportunities_MatchedKeywordsCount DEFAULT (0),
        Summary NVARCHAR(700) NOT NULL,
        Location NVARCHAR(120) NOT NULL,
        IsPriority BIT NOT NULL CONSTRAINT DF_Opportunities_IsPriority DEFAULT (0),
        PublishedDate DATE NULL,
        SeaceIndex INT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Opportunities_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Opportunities_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

-- Índices para Opportunities
CREATE INDEX IX_Opportunities_ClosingDate ON dbo.Opportunities(ClosingDate);
CREATE INDEX IX_Opportunities_MatchScore ON dbo.Opportunities(MatchScore);
CREATE INDEX IX_Opportunities_IsPriority ON dbo.Opportunities(IsPriority);
CREATE INDEX IX_Opportunities_CreatedAtUtc ON dbo.Opportunities(CreatedAtUtc);
CREATE INDEX IX_Opportunities_Category ON dbo.Opportunities(Category);
GO

-- ==========================================
-- 5. Tabla de Reglas de Alerta (AlertRule)
-- ==========================================

IF OBJECT_ID(N'dbo.AlertRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertRule (
        RuleId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        TriggerType NVARCHAR(50) NOT NULL, -- "alta_afinidad", "nueva_oportunidad", "cierre_proximo"
        ConditionsJson NVARCHAR(MAX) NULL, -- JSON con condiciones
        ChannelsJson NVARCHAR(MAX) NULL, -- JSON con canales: ["email", "panel", "slack"]
        RecipientsJson NVARCHAR(MAX) NULL, -- JSON con emails
        MessageTemplate NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        LastTriggeredAtUtc DATETIME2 NULL,
        TriggerCount INT NOT NULL DEFAULT 0,
        FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(UserId)
    );
END;
GO

-- Índices para AlertRule
CREATE INDEX IX_AlertRule_UserId ON dbo.AlertRule(UserId);
CREATE INDEX IX_AlertRule_IsActive ON dbo.AlertRule(IsActive);
CREATE INDEX IX_AlertRule_LastTriggeredAtUtc ON dbo.AlertRule(LastTriggeredAtUtc);
GO

-- ==========================================
-- 6. Tabla de Notificaciones de Panel (PanelNotification)
-- ==========================================

IF OBJECT_ID(N'dbo.PanelNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PanelNotification (
        NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        Type NVARCHAR(50) NOT NULL, -- "alert", "info", "warning", "success"
        OpportunityProcessCode NVARCHAR(50) NULL,
        OpportunityTitle NVARCHAR(500) NULL,
        AffinityScore INT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ReadAtUtc DATETIME2 NULL,
        FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(UserId)
    );
END;
GO

-- Índices para PanelNotification
CREATE INDEX IX_PanelNotification_UserId ON dbo.PanelNotification(UserId);
CREATE INDEX IX_PanelNotification_IsRead ON dbo.PanelNotification(IsRead);
CREATE INDEX IX_PanelNotification_CreatedAtUtc ON dbo.PanelNotification(CreatedAtUtc);
GO

-- ==========================================
-- 7. Datos Iniciales (Opcional)
-- ==========================================

-- Insertar usuario administrador por defecto
IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Email = 'admin@licitia.com')
BEGIN
    INSERT INTO dbo.AppUsers (UserId, FullName, CompanyName, Email, RoleName, PasswordHash, PasswordSalt, IsActive, CreatedAtUtc)
    VALUES (
        NEWID(),
        'Administrador',
        'LicitIA Admin',
        'admin@licitia.com',
        'Administrator',
        0x123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789, -- Hash temporal
        0x12345678901234567890123456789012345678, -- Salt temporal
        1,
        GETUTCDATE()
    );
END;
GO

-- Insertar perfil por defecto para sistema
IF NOT EXISTS (SELECT 1 FROM dbo.CompanyProfile WHERE CompanyName = 'Default')
BEGIN
    INSERT INTO dbo.CompanyProfile (UserId, CompanyName, PreferredCategories, PreferredLocations, PreferredModalities, MinAmount, MaxAmount, IdealAmount, FavoriteEntities, ExcludedEntities, PreferredKeywords, ExcludedKeywords, SeaceObjectDescription, SeaceCallYear, SeaceContractObject, MinDaysToClose, MaxDaysToClose, IdealDaysToClose, CreatedAtUtc, UpdatedAtUtc)
    VALUES (
        (SELECT TOP 1 UserId FROM dbo.AppUsers WHERE RoleName = 'Administrator'),
        'Default',
        '[]',
        '[]',
        '[]',
        10000,
        500000,
        250000,
        '[]',
        '[]',
        '[]',
        '[]',
        '',
        YEAR(GETDATE()),
        '',
        3,
        30,
        15,
        GETUTCDATE(),
        GETUTCDATE()
    );
END;
GO

-- ==========================================
-- 8. Datos de Ejemplo (Opcional)
-- ==========================================

-- Insertar oportunidades de ejemplo
IF NOT EXISTS (SELECT 1 FROM dbo.Opportunities)
BEGIN
    INSERT INTO dbo.Opportunities (ProcessCode, Title, EntityName, EstimatedAmount, ClosingDate, Category, Modality, MatchScore, MatchedKeywordsCount, Summary, Location, IsPriority, PublishedDate, SeaceIndex, CreatedAtUtc, UpdatedAtUtc)
    VALUES
    (
        N'LP-2026-014',
        N'Servicio de soporte y mantenimiento TI',
        N'Ministerio de Desarrollo e Inclusion Social',
        480000.00,
        '2026-04-25',
        N'Software',
        N'Licitacion publica',
        96,
        3,
        N'Alta coincidencia con experiencia previa en soporte TI y mesa de ayuda institucional.',
        N'Lima',
        1,
        '2026-04-20',
        1,
        GETUTCDATE(),
        GETUTCDATE()
    ),
    (
        N'AS-2026-087',
        N'Implementacion de plataforma documental',
        N'Gobierno Regional de Arequipa',
        310000.00,
        '2026-04-28',
        N'Transformacion digital',
        N'Adjudicacion simplificada',
        88,
        2,
        N'Proceso compatible con servicios de digitalizacion y gestion documental.',
        N'Regiones',
        0,
        '2026-04-15',
        2,
        GETUTCDATE(),
        GETUTCDATE()
    ),
    (
        N'AMC-2026-041',
        N'Servicio de mesa de ayuda institucional',
        N'Sunat',
        265000.00,
        '2026-04-30',
        N'Mesa de ayuda',
        N'Comparacion de precios',
        91,
        1,
        N'Coincidencia alta por experiencia en soporte operativo y atencion institucional.',
        N'Lima',
        1,
        '2026-04-10',
        3,
        GETUTCDATE(),
        GETUTCDATE()
    );
END;
GO

PRINT '';
PRINT '========================================';
PRINT 'Base de datos LicitIA creada exitosamente.';
PRINT '========================================';
PRINT '';
PRINT 'Tablas creadas:';
PRINT '  - AppUsers (Usuarios)';
PRINT '  - CompanyProfile (Perfiles de Empresa)';
PRINT '  - Opportunities (Oportunidades)';
PRINT '  - AlertRule (Reglas de Alerta)';
PRINT '  - PanelNotification (Notificaciones de Panel)';
PRINT '';
PRINT 'Índices creados para mejor rendimiento.';
PRINT 'Datos iniciales insertados (opcional).';
PRINT '========================================';
