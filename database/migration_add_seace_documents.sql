-- Agrega almacenamiento JSON para documentos extraidos desde tbFicha:dtDocumentos

IF COL_LENGTH('dbo.Opportunities', 'SeaceDocumentsJson') IS NULL
BEGIN
    ALTER TABLE dbo.Opportunities
        ADD SeaceDocumentsJson NVARCHAR(MAX) NOT NULL
            CONSTRAINT DF_Opportunities_SeaceDocumentsJson DEFAULT '';
END;
