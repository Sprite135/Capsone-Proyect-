IF DB_ID(N'LicitIAAuthDb') IS NULL
BEGIN
    CREATE DATABASE LicitIAAuthDb;
END;
GO

USE LicitIAAuthDb;
GO

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
        MatchScore INT NOT NULL,
        Summary NVARCHAR(700) NOT NULL,
        Location NVARCHAR(120) NOT NULL,
        IsPriority BIT NOT NULL CONSTRAINT DF_Opportunities_IsPriority DEFAULT (0),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Opportunities_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Opportunities)
BEGIN
    INSERT INTO dbo.Opportunities
    (
        ProcessCode,
        Title,
        EntityName,
        EstimatedAmount,
        ClosingDate,
        Category,
        Modality,
        MatchScore,
        Summary,
        Location,
        IsPriority
    )
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
        N'Alta coincidencia con experiencia previa en soporte TI y mesa de ayuda institucional.',
        N'Lima',
        1
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
        N'Proceso compatible con servicios de digitalizacion y gestion documental.',
        N'Regiones',
        1
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
        N'Coincidencia alta por experiencia en soporte operativo y atencion institucional.',
        N'Lima',
        1
    ),
    (
        N'CP-2026-115',
        N'Implementacion de tablero de monitoreo presupuestal',
        N'EsSalud',
        195000.00,
        '2026-05-03',
        N'Transformacion digital',
        N'Concurso publico',
        79,
        N'Oportunidad interesante para analitica, con menor prioridad que las coincidencias principales.',
        N'Nacional',
        0
    );
END;
GO
