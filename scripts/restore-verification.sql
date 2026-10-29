-- Restore into an isolated verification database, never the live workshop database.
RESTORE DATABASE [AmcottsWorkshop_Verification]
FROM DISK = N'D:\AmcottsBackups\AmcottsWorkshop_FULL.bak'
WITH MOVE 'AmcottsWorkshop' TO 'D:\SQLData\AmcottsWorkshop_Verification.mdf',
MOVE 'AmcottsWorkshop_log' TO 'D:\SQLData\AmcottsWorkshop_Verification_log.ldf', REPLACE, RECOVERY;
DBCC CHECKDB ([AmcottsWorkshop_Verification]) WITH NO_INFOMSGS;
