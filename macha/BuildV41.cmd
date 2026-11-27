@echo off
setlocal
call "%~dp0BuildV42.cmd" v4.1 %*
exit /b %errorlevel%
