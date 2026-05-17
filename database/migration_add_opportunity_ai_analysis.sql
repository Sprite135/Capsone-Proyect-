-- Migration para agregar analisis IA opcional por oportunidad
-- Ejecutar en SQL Server: sqlcmd -S localhost -d LicitIAAuthDb -i database/migration_add_opportunity_ai_analysis.sql -E

IF OBJECT_ID(N'dbo.OpportunityAiAnalysis', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OpportunityAiAnalysis (
        AnalysisId INT IDENTITY(1,1) PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        OpportunityId INT NOT NULL,
        ModelName NVARCHAR(120) NOT NULL,
        Recommendation NVARCHAR(200) NOT NULL,
        Summary NVARCHAR(MAX) NOT NULL,
        Risks NVARCHAR(MAX) NOT NULL,
        Requirements NVARCHAR(MAX) NOT NULL,
        NextSteps NVARCHAR(MAX) NOT NULL,
        RawResponse NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_OpportunityAiAnalysis_User_Opportunity UNIQUE (UserId, OpportunityId)
    );

    PRINT 'Tabla OpportunityAiAnalysis creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'Tabla OpportunityAiAnalysis ya existe.';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OpportunityAiAnalysis_UserId_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.OpportunityAiAnalysis'))
BEGIN
    CREATE INDEX IX_OpportunityAiAnalysis_UserId_CreatedAtUtc ON dbo.OpportunityAiAnalysis(UserId, CreatedAtUtc);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OpportunityAiAnalysis_OpportunityId' AND object_id = OBJECT_ID(N'dbo.OpportunityAiAnalysis'))
BEGIN
    CREATE INDEX IX_OpportunityAiAnalysis_OpportunityId ON dbo.OpportunityAiAnalysis(OpportunityId);
END

PRINT 'Migracion de OpportunityAiAnalysis completada.';
