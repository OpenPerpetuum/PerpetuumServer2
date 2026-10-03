-- Altered restore DB SQL script that can run inside a container
-- Assumes the 'perpetuumsa.bak' is available at the path '/data'

USE [master]
GO


ALTER DATABASE perpetuumsa
SET SINGLE_USER WITH
ROLLBACK IMMEDIATE

RESTORE DATABASE perpetuumsa FROM DISK = '/data/perpetuumsa.bak' WITH 
MOVE 'perpetuumsa' TO '/data/psa.mdf',
MOVE 'perpetuumsa_log' TO '/data/psa_log.ldf', REPLACE

ALTER DATABASE perpetuumsa SET MULTI_USER
GO