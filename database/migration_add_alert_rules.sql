-- Migration para agregar tabla AlertRule
-- Ejecutar en SQL Server: sqlcmd -S localhost -d LicitIAAuthDb -i database/migration_add_alert_rules.sql -E

-- Crear tabla AlertRule
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AlertRule')
BEGIN
    CREATE TABLE dbo.AlertRule (
        RuleId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Trigger NVARCHAR(50) NOT NULL, -- "alta_afinidad", "nueva_oportunidad", "cierre_proximo"
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
    
    CREATE INDEX IX_AlertRule_UserId ON dbo.AlertRule(UserId);
    CREATE INDEX IX_AlertRule_IsActive ON dbo.AlertRule(IsActive);
    CREATE INDEX IX_AlertRule_LastTriggeredAtUtc ON dbo.AlertRule(LastTriggeredAtUtc);
    PRINT 'Tabla AlertRule creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'Tabla AlertRule ya existe.';
END

PRINT '';
PRINT '========================================';
PRINT 'Migración de AlertRule completada.';
PRINT '========================================';
