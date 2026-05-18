IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceDepartment'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceDepartment NVARCHAR(100) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceDepartment DEFAULT '';

    PRINT 'Added: SeaceDepartment';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceDepartment';
END
