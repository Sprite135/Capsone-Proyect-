-- Migration: Add Panel Notifications table
-- Date: 2026-05-08

IF OBJECT_ID(N'dbo.PanelNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PanelNotification (
        NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        Type NVARCHAR(50) NOT NULL, -- "alert", "info", "warning", "success"
        OpportunityProcessCode NVARCHAR(50) NULL,
        OpportunityTitle NVARCHAR(500) NULL,
        AffinityScore INT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ReadAtUtc DATETIME2 NULL,
        FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(UserId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PanelNotification_UserId' AND object_id = OBJECT_ID(N'dbo.PanelNotification'))
BEGIN
    CREATE INDEX IX_PanelNotification_UserId ON dbo.PanelNotification(UserId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PanelNotification_IsRead' AND object_id = OBJECT_ID(N'dbo.PanelNotification'))
BEGIN
    CREATE INDEX IX_PanelNotification_IsRead ON dbo.PanelNotification(IsRead);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PanelNotification_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.PanelNotification'))
BEGIN
    CREATE INDEX IX_PanelNotification_CreatedAtUtc ON dbo.PanelNotification(CreatedAtUtc);
END;
