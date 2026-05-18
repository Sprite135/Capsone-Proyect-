IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceEntityAcronym'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceEntityAcronym NVARCHAR(100) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceEntityAcronym DEFAULT '';

    PRINT 'Added: SeaceEntityAcronym';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceEntityAcronym';
END
