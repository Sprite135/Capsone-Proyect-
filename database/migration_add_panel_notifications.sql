-- Migration: Add Panel Notifications table
-- Date: 2026-05-08

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

-- Create index for faster queries on UserId
CREATE INDEX IX_PanelNotification_UserId ON dbo.PanelNotification(UserId);
CREATE INDEX IX_PanelNotification_IsRead ON dbo.PanelNotification(IsRead);
CREATE INDEX IX_PanelNotification_CreatedAtUtc ON dbo.PanelNotification(CreatedAtUtc);
