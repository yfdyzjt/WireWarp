@echo off
rem Helper called by parent scripts; intentionally does not use SETLOCAL.
if defined WWORLD goto check
set "WW_BASE=%USERPROFILE%\Documents\My Games\Terraria"
set "WW_STABLE=%WW_BASE%\tModLoader\Worlds\world"
set "WW_DEV=%WW_BASE%\tModLoader-dev\Worlds\world"
set "WW_HAS_STABLE="
set "WW_HAS_DEV="
if exist "%WW_STABLE%.wwir" if exist "%WW_STABLE%.wwio" set "WW_HAS_STABLE=1"
if exist "%WW_DEV%.wwir" if exist "%WW_DEV%.wwio" set "WW_HAS_DEV=1"
if defined WW_HAS_STABLE if defined WW_HAS_DEV goto ambiguous
if defined WW_HAS_DEV set "WWORLD=%WW_DEV%"
if defined WW_HAS_STABLE set "WWORLD=%WW_STABLE%"
if not defined WWORLD goto missing
:check
if not exist "%WWORLD%.wwir" goto missing
if not exist "%WWORLD%.wwio" goto missing
echo World export: "%WWORLD%"
exit /b 0
:ambiguous
echo ERROR: Both stable and dev world exports exist. Set WWORLD explicitly before building.
echo Stable: "%WW_STABLE%"
echo Dev:    "%WW_DEV%"
echo Use the actual live world's path WITHOUT an extension.
exit /b 1
:missing
echo ERROR: No complete .wwir/.wwio pair found for WWORLD="%WWORLD%".
echo Set WWORLD to the actual live world's path WITHOUT an extension.
exit /b 1
