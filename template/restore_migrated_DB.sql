USE [master]
GO

IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'perpetuumsa')
BEGIN
    ALTER DATABASE perpetuumsa SET SINGLE_USER WITH ROLLBACK IMMEDIATE
END
GO

RESTORE DATABASE perpetuumsa FROM DISK = '/data/perpetuumsa_migrated.bak' WITH 
MOVE 'perpetuumsa' TO '/data/psa.mdf',
MOVE 'perpetuumsa_log' TO '/data/psa_log.ldf', REPLACE
GO

ALTER DATABASE perpetuumsa SET MULTI_USER
GO
