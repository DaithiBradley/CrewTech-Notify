@echo off
REM Build script for Windows

echo Building CrewTech-Notify...
echo.

echo Cleaning previous builds...
dotnet clean --verbosity quiet

echo Restoring dependencies...
dotnet restore

echo Building solution...
dotnet build --no-restore --configuration Release

echo.
echo Running tests...
dotnet test --no-build --configuration Release --verbosity normal

echo.
echo Build completed successfully!
echo.
echo To run the services:
echo   API:    dotnet run --project src\CrewTech.Notify.SenderApi
echo   Worker: dotnet run --project src\CrewTech.Notify.Worker
echo   CLI:    dotnet run --project src\CrewTech.Notify.Cli -- send --help
