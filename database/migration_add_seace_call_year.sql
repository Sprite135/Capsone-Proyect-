IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceCallYear'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceCallYear INT NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceCallYear DEFAULT (YEAR(GETDATE()));

    PRINT 'Added: SeaceCallYear';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceCallYear';
END

