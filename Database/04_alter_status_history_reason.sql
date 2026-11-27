-- 04_alter_status_history_reason.sql
-- UC1 (FR10, NFR5): a rejection, cancellation or failed delivery stores its reason, but OrderStatusHistory (02)
-- has no Reason column. Safe to re-run.
USE SaigonBite;
GO
IF COL_LENGTH('dbo.OrderStatusHistory', 'Reason') IS NULL
    ALTER TABLE OrderStatusHistory ADD Reason NVARCHAR(300) NULL;
GO
