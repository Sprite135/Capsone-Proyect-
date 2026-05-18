IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceProvince'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceProvince NVARCHAR(100) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceProvince DEFAULT '';

    PRINT 'Added: SeaceProvince';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceProvince';
END
