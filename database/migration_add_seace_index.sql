-- Agregar columna SeaceIndex para mantener el orden original de SEACE
USE LicitIAAuthDb;
GO

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Opportunities') 
    AND name = N'SeaceIndex'
)
BEGIN
    ALTER TABLE dbo.Opportunities
    ADD SeaceIndex INT NULL;
END;
GO

-- Actualizar registros existentes con un índice basado en OpportunityId
UPDATE dbo.Opportunities
SET SeaceIndex = OpportunityId
WHERE SeaceIndex IS NULL;
GO
