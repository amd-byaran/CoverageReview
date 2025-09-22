@echo off
REM Test script for HvpHtmlParser console application

echo ================================================
echo HvpHtmlParser Console Test
echo ================================================
echo.

REM Set the default URL if you have one from your project
REM Replace this with your actual HVP file URL
set DEFAULT_URL=

if "%DEFAULT_URL%"=="" (
    echo No default URL set in script.
    echo.
    echo Usage:
    echo   test_hvp.bat "https://server.com/path/to/file.hvp"
    echo.
    echo Or edit this script to set DEFAULT_URL variable
    echo.
    
    if "%1"=="" (
        echo No URL provided as argument.
        pause
        exit /b 1
    )
    
    set TEST_URL=%1
) else (
    if "%1"=="" (
        set TEST_URL=%DEFAULT_URL%
        echo Using default URL: %TEST_URL%
    ) else (
        set TEST_URL=%1
        echo Using provided URL: %TEST_URL%
    )
)

echo.
echo Running HvpParserTest...
echo.

cd /d "%~dp0"
dotnet run --project HvpParserTest "%TEST_URL%"

echo.
echo Test completed.
pause