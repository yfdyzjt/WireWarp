@echo off
setlocal
call "%~dp0BenchmarkV41.cmd" %*
exit /b %errorlevel%
