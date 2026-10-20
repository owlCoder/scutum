@echo off
setlocal

rem Windows entry point for the canonical Bash script.
where bash >nul 2>&1
if not errorlevel 1 (
  bash "%~dp0new-service.sh" %*
  exit /b %errorlevel%
)

where wsl >nul 2>&1
if errorlevel 1 (
  echo Greska: nije pronadjen Git Bash niti WSL.
  echo Instalirajte Docker Desktop sa WSL 2 backend-om, pa ponovite komandu.
  exit /b 1
)

set "SCRIPT_DIR=%~dp0"
for /f "delims=" %%I in ('wsl wslpath -a "%SCRIPT_DIR%"') do set "WSL_SCRIPT_DIR=%%I"
if not defined WSL_SCRIPT_DIR (
  echo Greska: WSL nije uspeo da prevede putanju repozitorijuma.
  echo Proverite da je repozitorijum na lokalnom Windows disku.
  exit /b 1
)

wsl bash "%WSL_SCRIPT_DIR%/new-service.sh" %*
exit /b %errorlevel%
