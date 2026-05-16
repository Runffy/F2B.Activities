@echo off
setlocal

cd /d "%~dp0"

set PORT=%1
if "%PORT%"=="" set PORT=8000

echo [MockSite] 准备启动，端口: %PORT%

python --version >nul 2>&1
if %errorlevel%==0 (
  python "server.py" --port %PORT%
  goto :end
)

py --version >nul 2>&1
if %errorlevel%==0 (
  py "server.py" --port %PORT%
  goto :end
)

echo [MockSite] 未检测到 Python，请先安装 Python 3。
pause
exit /b 1

:end
endlocal
