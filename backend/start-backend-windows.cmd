@echo off
setlocal

cd /d "%~dp0"

if not exist node_modules (
  echo Installing backend dependencies...
  call npm.cmd install
  if errorlevel 1 (
    echo.
    echo Failed to install dependencies. Make sure Node.js LTS with npm is installed.
    pause
    exit /b 1
  )
)

echo Starting Appreciators TCG backend on http://localhost:3001
echo Press Ctrl+C to stop the server.
echo.

node src\index.js
