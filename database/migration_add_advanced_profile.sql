-- Migration: Add advanced profile fields for enhanced affinity
-- Similar to LicitaLAB AI features

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'PreferredModalities')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD PreferredModalities NVARCHAR(MAX) NOT NULL DEFAULT '[]';
    PRINT 'Added: PreferredModalities';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'IdealAmount')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD IdealAmount DECIMAL(18, 2) NOT NULL DEFAULT 0;
    PRINT 'Added: IdealAmount';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'ExcludedEntities')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD ExcludedEntities NVARCHAR(MAX) NOT NULL DEFAULT '[]';
    PRINT 'Added: ExcludedEntities';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'PreferredKeywords')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD PreferredKeywords NVARCHAR(MAX) NOT NULL DEFAULT '[]';
    PRINT 'Added: PreferredKeywords';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'ExcludedKeywords')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD ExcludedKeywords NVARCHAR(MAX) NOT NULL DEFAULT '[]';
    PRINT 'Added: ExcludedKeywords';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'MinDaysToClose')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD MinDaysToClose INT NOT NULL DEFAULT 3;
    PRINT 'Added: MinDaysToClose';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'MaxDaysToClose')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD MaxDaysToClose INT NOT NULL DEFAULT 30;
    PRINT 'Added: MaxDaysToClose';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'IdealDaysToClose')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD IdealDaysToClose INT NOT NULL DEFAULT 15;
    PRINT 'Added: IdealDaysToClose';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'SeaceObjectDescription')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD SeaceObjectDescription NVARCHAR(500) NOT NULL DEFAULT '';
    PRINT 'Added: SeaceObjectDescription';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'SeaceCallYear')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD SeaceCallYear INT NOT NULL DEFAULT (YEAR(GETDATE()));
    PRINT 'Added: SeaceCallYear';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CompanyProfile') AND name = 'SeaceContractObject')
BEGIN
    ALTER TABLE dbo.CompanyProfile ADD SeaceContractObject NVARCHAR(50) NOT NULL DEFAULT '';
    PRINT 'Added: SeaceContractObject';
END

PRINT 'Migration completed successfully.';
