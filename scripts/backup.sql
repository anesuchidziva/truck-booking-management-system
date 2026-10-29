-- SQL Server Express backup template. Run with sqlcmd from Task Scheduler.
-- Store backups outside the database host's primary data directory.
BACKUP DATABASE [AmcottsWorkshop]
TO DISK = N'D:\AmcottsBackups\AmcottsWorkshop_FULL.bak'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;
RESTORE VERIFYONLY FROM DISK = N'D:\AmcottsBackups\AmcottsWorkshop_FULL.bak';
