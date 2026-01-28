# Quick Start Demo

This guide walks you through running CrewTech-Notify locally.

## Prerequisites

- .NET 8 SDK installed
- Terminal/Command Prompt

## Step 1: Build the Solution

```bash
cd CrewTech-Notify
./scripts/build.sh  # or build.bat on Windows
```

## Step 2: Start the API (Terminal 1)

```bash
cd src/CrewTech.Notify.SenderApi
dotnet run
```

You should see:
```
Now listening on: http://localhost:5000
```

Open Swagger UI in your browser: http://localhost:5000/swagger

## Step 3: Start the Worker (Terminal 2)

```bash
cd src/CrewTech.Notify.Worker
dotnet run
```

You should see:
```
Notification Worker started
Supported platforms: Fake, WNS, FCM
```

## Step 4: Send Notifications

### Option A: Using CLI

Open Terminal 3:

```bash
cd src/CrewTech.Notify.Cli

# Send a notification
dotnet run -- send \
  --platform Fake \
  --device-token "my-device-001" \
  --title "Hello World" \
  --body "My first notification!"

# Check its status (use the ID from previous command)
dotnet run -- status --id <notification-id>
```

### Option B: Using Windows Client

```bash
cd samples/CrewTech.Notify.WindowsClient

# Run demo (sends 4 test notifications)
dotnet run demo

# Or run interactive mode
dotnet run
```

### Option C: Using cURL

```bash
# Send notification
curl -X POST http://localhost:5000/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "targetPlatform": "Fake",
    "deviceToken": "test-device-123",
    "title": "Test Notification",
    "body": "Testing CrewTech-Notify",
    "tags": ["test"],
    "priority": "Normal"
  }'

# Check status (use the notificationId from response)
curl http://localhost:5000/api/notifications/<notification-id>
```

## Step 5: Watch the Magic! ✨

In Terminal 2 (Worker), you'll see logs like:
```
📱 Fake notification sent to test-device-123: Test Notification - Testing CrewTech-Notify
✓ Notification <guid> sent successfully
```

The notification status will change: `Pending` → `Processing` → `Sent`

## Understanding the Flow

```
Your Request → API → Database (Outbox) → Worker → Provider → External Service
                ↓                            ↓
            Status: Pending            Status: Sent
```

## Testing Retry Logic

The FakeProvider has a 5% random failure rate. If you send enough notifications, you'll see retries in action:

```
⚠ Notification <guid> failed, will retry in ~5s: Simulated transient failure
```

Watch as it automatically retries with exponential backoff!

## Next Steps

1. **Configure Real Providers**: Edit `appsettings.json` with your WNS/FCM credentials
2. **Test Idempotency**: Send the same request twice with an idempotencyKey
3. **Explore Swagger**: http://localhost:5000/swagger for interactive API docs
4. **Check Database**: Inspect `data/notifications.db` with SQLite tools
5. **Scale**: Run multiple Workers for higher throughput

## Troubleshooting

### "Connection refused" error
- Make sure the API is running on port 5000
- Check firewall settings

### Notifications stuck in "Pending"
- Ensure Worker is running
- Check Worker logs for errors
- Verify API and Worker use the same database (check `appsettings.json`)

### Build errors
- Verify .NET 8 SDK is installed: `dotnet --version`
- Run `dotnet restore` in solution root

## Production Considerations

Before deploying to production:

1. **Database**: Switch from SQLite to SQL Server/PostgreSQL
2. **Security**: Store credentials in Azure Key Vault or similar
3. **Monitoring**: Add Application Insights or similar
4. **Scale**: Deploy multiple API and Worker instances
5. **Configuration**: Use environment-specific appsettings

See [ARCHITECTURE.md](../docs/ARCHITECTURE.md) for more details.

## Have Fun! 🚀

You now have a fully functional enterprise notification platform running locally!
