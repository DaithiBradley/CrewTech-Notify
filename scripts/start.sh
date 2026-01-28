#!/bin/bash
# Quick start script - runs API and Worker in background

set -e

echo "🚀 Starting CrewTech-Notify services..."
echo ""

# Kill any existing instances
pkill -f "CrewTech.Notify.SenderApi" || true
pkill -f "CrewTech.Notify.Worker" || true

# Build first
echo "Building..."
dotnet build --verbosity quiet

# Start API
echo "Starting Sender API on http://localhost:5000..."
dotnet run --project src/CrewTech.Notify.SenderApi --urls "http://localhost:5000" > logs/api.log 2>&1 &
API_PID=$!
echo "API PID: $API_PID"

# Wait for API to start
sleep 3

# Start Worker
echo "Starting Worker..."
dotnet run --project src/CrewTech.Notify.Worker > logs/worker.log 2>&1 &
WORKER_PID=$!
echo "Worker PID: $WORKER_PID"

echo ""
echo "✅ Services started!"
echo ""
echo "API:    http://localhost:5000"
echo "Swagger: http://localhost:5000/swagger"
echo ""
echo "API logs:    tail -f logs/api.log"
echo "Worker logs: tail -f logs/worker.log"
echo ""
echo "To stop services:"
echo "  kill $API_PID $WORKER_PID"
