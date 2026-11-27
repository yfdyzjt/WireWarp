@echo off
setlocal
rem Compatibility alias. RC3 retains RC2 validation.
call "%~dp0ValidateRC3.cmd" %*
exit /b %errorlevel%
