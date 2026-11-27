@echo off
rem SPDX-License-Identifier: GPL-3.0-only
rem Stable entry point for the validated RC3 regression suite.
call "%~dp0ValidateRC3.cmd" %*
exit /b %errorlevel%
