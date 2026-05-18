IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CompanyProfile')
      AND name = 'SeaceContractObject'
)
BEGIN
    ALTER TABLE dbo.CompanyProfile
    ADD SeaceContractObject NVARCHAR(50) NOT NULL
        CONSTRAINT DF_CompanyProfile_SeaceContractObject DEFAULT '';

    PRINT 'Added: SeaceContractObject';
END
ELSE
BEGIN
    PRINT 'Column already exists: SeaceContractObject';
END
