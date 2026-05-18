IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceDistrict'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceDistrict NVARCHAR(100) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceDistrict DEFAULT '';

    PRINT 'Added: SeaceDistrict';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceDistrict';
END
