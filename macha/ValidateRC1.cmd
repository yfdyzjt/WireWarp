@echo off
setlocal
rem Compatibility alias. RC2 includes the tested RC1 validation layout.
call "%~dp0ValidateRC2.cmd" %*
exit /b %errorlevel%
