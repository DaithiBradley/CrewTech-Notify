#!/bin/bash
set -e

echo "🐳 Building test Docker images..."

# Build API image
echo "Building API image..."
docker build -f src/CrewTech.Notify.SenderApi/Dockerfile.test -t crewtech-notify-api:test .
echo "✓ API image built"

# Build Worker image
echo "Building Worker image..."
docker build -f src/CrewTech.Notify.Worker/Dockerfile.test -t crewtech-notify-worker:test .
echo "✓ Worker image built"

echo ""
echo "🎉 Test images ready!"
docker images | grep crewtech-notify
