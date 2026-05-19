-- Migration para guardar detalles visibles de tbFicha:pnlContenedorGral

IF COL_LENGTH('dbo.Opportunities', 'SelectionType') IS NULL
    ALTER TABLE dbo.Opportunities ADD SelectionType NVARCHAR(200) NOT NULL CONSTRAINT DF_Opportunities_SelectionType DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'ConvocationNumber') IS NULL
    ALTER TABLE dbo.Opportunities ADD ConvocationNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_Opportunities_ConvocationNumber DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'ApplicableRegulation') IS NULL
    ALTER TABLE dbo.Opportunities ADD ApplicableRegulation NVARCHAR(500) NOT NULL CONSTRAINT DF_Opportunities_ApplicableRegulation DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'SeaceVersion') IS NULL
    ALTER TABLE dbo.Opportunities ADD SeaceVersion NVARCHAR(50) NOT NULL CONSTRAINT DF_Opportunities_SeaceVersion DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'EntityLegalAddress') IS NULL
    ALTER TABLE dbo.Opportunities ADD EntityLegalAddress NVARCHAR(500) NOT NULL CONSTRAINT DF_Opportunities_EntityLegalAddress DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'EntityWebsite') IS NULL
    ALTER TABLE dbo.Opportunities ADD EntityWebsite NVARCHAR(300) NOT NULL CONSTRAINT DF_Opportunities_EntityWebsite DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'EntityPhone') IS NULL
    ALTER TABLE dbo.Opportunities ADD EntityPhone NVARCHAR(100) NOT NULL CONSTRAINT DF_Opportunities_EntityPhone DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'ContractObject') IS NULL
    ALTER TABLE dbo.Opportunities ADD ContractObject NVARCHAR(200) NOT NULL CONSTRAINT DF_Opportunities_ContractObject DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'ParticipationCost') IS NULL
    ALTER TABLE dbo.Opportunities ADD ParticipationCost NVARCHAR(100) NOT NULL CONSTRAINT DF_Opportunities_ParticipationCost DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'BasesReproductionCost') IS NULL
    ALTER TABLE dbo.Opportunities ADD BasesReproductionCost NVARCHAR(100) NOT NULL CONSTRAINT DF_Opportunities_BasesReproductionCost DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'SeaceDetailJson') IS NULL
    ALTER TABLE dbo.Opportunities ADD SeaceDetailJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Opportunities_SeaceDetailJson DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'SeaceScheduleJson') IS NULL
    ALTER TABLE dbo.Opportunities ADD SeaceScheduleJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Opportunities_SeaceScheduleJson DEFAULT '';

IF COL_LENGTH('dbo.Opportunities', 'SeaceDocumentsJson') IS NULL
    ALTER TABLE dbo.Opportunities ADD SeaceDocumentsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Opportunities_SeaceDocumentsJson DEFAULT '';

PRINT 'Migracion de detalles SEACE completada.';
