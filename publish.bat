@echo off
echo ========================================
echo   LogAnalyzerApp - Building standalone
echo ========================================
echo.

dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "bin\Publish\LogAnalyzerApp"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED! Make sure you have .NET 8 SDK installed.
    pause
    exit /b 1
)

echo.
echo ========================================
echo   DONE!
echo ========================================
echo.
echo Output folder: bin\Publish\LogAnalyzerApp\
echo.
echo To distribute:
echo   1. ZIP the "bin\Publish\LogAnalyzerApp" folder
echo   2. Send the ZIP to the user
echo   3. User extracts, double-clicks LogAnalyzerApp.exe
echo.
pause
