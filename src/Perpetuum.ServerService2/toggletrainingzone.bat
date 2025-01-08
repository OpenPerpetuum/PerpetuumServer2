@echo off

sqlcmd -S "localhost\PERPSQL" -d perpetuumsa -i %~dp0toggletrainingzone.sql