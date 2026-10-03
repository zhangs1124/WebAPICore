@echo off
title WebAPICore System
echo ========================================================
echo   Starting WebAPICore Inventory System...
echo ========================================================
echo.
echo   [Dashboard] http://localhost:5092/
echo   [Scalar API] http://localhost:5092/scalar/v1
echo.
timeout /t 2 > nul
start http://localhost:5092/
dotnet run --project WebAPICore.Api