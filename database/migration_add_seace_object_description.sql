IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceObjectDescription'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceObjectDescription NVARCHAR(500) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceObjectDescription DEFAULT '';

    PRINT 'Added: SeaceObjectDescription';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceObjectDescription';
END

