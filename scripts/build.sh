#!/bin/bash
# Build script for CrewTech-Notify

set -e  # Exit on error

echo "🏗️  Building CrewTech-Notify..."
echo ""

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean --verbosity quiet

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build all projects
echo "Building solution..."
dotnet build --no-restore --configuration Release

# Run tests
echo ""
echo "🧪 Running tests..."
dotnet test --no-build --configuration Release --verbosity normal

echo ""
echo "✅ Build completed successfully!"
echo ""
echo "To run the services:"
echo "  API:    dotnet run --project src/CrewTech.Notify.SenderApi"
echo "  Worker: dotnet run --project src/CrewTech.Notify.Worker"
echo "  CLI:    dotnet run --project src/CrewTech.Notify.Cli -- send --help"
