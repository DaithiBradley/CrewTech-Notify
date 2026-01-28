# CrewTech-Notify

[![CI/CD Pipeline](https://github.com/DaithiBradley/CrewTech-Notify/actions/workflows/ci.yml/badge.svg)](https://github.com/DaithiBradley/CrewTech-Notify/actions/workflows/ci.yml)

**Enterprise-grade unified notifications platform** built with .NET 8, providing reliable push notification delivery for Windows (WNS), Android (FCM), and extensible to iOS and team collaboration platforms.

## 🎯 Features

### Core Capabilities
- **Multi-Platform Support**: Windows Push Notification Service (WNS), Firebase Cloud Messaging (FCM), and extensible architecture for iOS, Slack, Zulip, and Mattermost
- **Durable Outbox Pattern**: Guarantees notification delivery using transactional outbox with SQLite/SQL Server
- **Intelligent Retry Logic**: Exponential backoff with jitter to handle transient failures gracefully
- **Idempotency**: Prevents duplicate notifications using idempotency keys
- **Dead-Letter Queue**: Failed notifications are tracked and moved to dead-letter for investigation
- **Message Tagging**: Filter and categorize notifications with custom tags
- **Priority Levels**: Low, Normal, High priority support
- **Scheduled Delivery**: Queue notifications for future delivery

### Architecture
- **Sender API**: REST API for queuing notifications (ASP.NET Core)
- **Worker Service**: Background processor with retry logic and multi-provider support
- **Fake Provider**: Local development and testing without real credentials
- **CLI Tool**: Command-line interface for sending notifications
- **Windows Client Sample**: Demo application showing integration

## 🏗️ Architecture

```
┌─────────────────┐
│   Sender API    │  ← REST API for queueing notifications
│   (Port 5000)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Outbox (DB)    │  ← Durable storage (SQLite/SQL Server)
│  ┌───────────┐  │
│  │ Pending   │  │
│  │ Failed    │  │
│  │ Sent      │  │
│  │ Dead-Lett │  │
│  └───────────┘  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Worker Service │  ← Background processor with retry
└────────┬────────┘
         │
    ┌────┴─────┬──────────┬──────────┐
    ▼          ▼          ▼          ▼
┌───────┐  ┌───────┐  ┌───────┐  ┌────────┐
│  WNS  │  │  FCM  │  │ Fake  │  │ Future │
│Windows│  │Android│  │ Local │  │iOS/Etc │
└───────┘  └───────┘  └───────┘  └────────┘
```

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQLite (included) or SQL Server

### 1. Clone and Build
```bash
git clone https://github.com/DaithiBradley/CrewTech-Notify.git
cd CrewTech-Notify
./scripts/build.sh  # or build.bat on Windows
```

### 2. Start Services

**Terminal 1 - Start API:**
```bash
cd src/CrewTech.Notify.SenderApi
dotnet run
```
Access Swagger UI at: http://localhost:5000/swagger

**Terminal 2 - Start Worker:**
```bash
cd src/CrewTech.Notify.Worker
dotnet run
```

### 3. Send Test Notification

**Using CLI:**
```bash
cd src/CrewTech.Notify.Cli
dotnet run -- send \
  --platform Fake \
  --device-token "test-device-001" \
  --title "Hello CrewTech" \
  --body "Your first notification!"
```

**Using cURL:**
```bash
curl -X POST http://localhost:5000/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "targetPlatform": "Fake",
    "deviceToken": "test-device-001",
    "title": "Hello CrewTech",
    "body": "Your first notification!",
    "tags": ["test", "demo"],
    "priority": "Normal"
  }'
```

**Using Windows Client:**
```bash
cd samples/CrewTech.Notify.WindowsClient
dotnet run demo
```

## 📚 Documentation

### API Endpoints

#### POST /api/notifications
Queue a new notification for delivery.

**Request:**
```json
{
  "idempotencyKey": "unique-key-123",  // Optional
  "targetPlatform": "WNS",            // WNS, FCM, Fake
  "deviceToken": "channel-uri-or-token",
  "title": "Notification Title",
  "body": "Notification message body",
  "data": {                           // Optional
    "action": "open-app",
    "url": "https://example.com"
  },
  "tags": ["urgent", "billing"],      // Optional
  "priority": "High",                 // Low, Normal, High
  "scheduledFor": "2024-01-20T10:00:00Z"  // Optional
}
```

**Response (202 Accepted):**
```json
{
  "notificationId": "guid",
  "status": "Accepted",
  "message": "Notification queued for delivery"
}
```

#### GET /api/notifications/{id}
Get notification status.

**Response:**
```json
{
  "notificationId": "guid",
  "status": "Sent",
  "targetPlatform": "Fake",
  "retryCount": 0,
  "createdAt": "2024-01-20T09:00:00Z",
  "sentAt": "2024-01-20T09:00:05Z",
  "errorMessage": null
}
```

### Configuration

#### Windows Push Notification Service (WNS)

1. Register your app in Azure AD
2. Get Client ID, Client Secret, and Tenant ID
3. Configure in `appsettings.json`:

```json
{
  "WNS": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id"
  }
}
```

#### Firebase Cloud Messaging (FCM)

1. Create Firebase project
2. Get Server Key from Firebase Console
3. Configure in `appsettings.json`:

```json
{
  "FCM": {
    "ProjectId": "your-project-id",
    "ServerKey": "your-server-key"
  }
}
```

### Retry Policy

The system uses exponential backoff with jitter:
- **Base Delay**: 5 seconds
- **Max Delay**: 300 seconds (5 minutes)
- **Jitter Factor**: 30% to prevent thundering herd
- **Max Retries**: 5 (configurable per notification)

**Retry Schedule Example:**
- Attempt 1: Immediate
- Attempt 2: ~5 seconds
- Attempt 3: ~10 seconds
- Attempt 4: ~20 seconds
- Attempt 5: ~40 seconds
- Attempt 6: ~80 seconds
- After max retries → Dead Letter Queue

### Dead-Letter Queue

Notifications are moved to dead-letter when:
- Max retries exceeded
- Non-retryable errors (invalid device token, etc.)
- Platform not supported

Query dead-lettered notifications:
```sql
SELECT * FROM NotificationMessages 
WHERE Status = 'DeadLettered'
ORDER BY UpdatedAt DESC;
```

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/CrewTech.Notify.Core.Tests
dotnet test tests/CrewTech.Notify.Infrastructure.Tests
```

### Test Coverage
The solution includes:
- **Unit Tests**: Core domain logic, retry policies, entities
- **Integration Tests**: Repository operations, database interactions
- **Provider Tests**: Notification provider implementations

## 🔧 Project Structure

```
CrewTech-Notify/
├── src/
│   ├── CrewTech.Notify.Core/              # Domain models, interfaces
│   ├── CrewTech.Notify.Infrastructure/    # EF Core, repositories, providers
│   ├── CrewTech.Notify.SenderApi/         # REST API
│   ├── CrewTech.Notify.Worker/            # Background worker
│   └── CrewTech.Notify.Cli/               # CLI tool
├── tests/
│   ├── CrewTech.Notify.Core.Tests/
│   ├── CrewTech.Notify.Infrastructure.Tests/
│   └── CrewTech.Notify.Integration.Tests/
├── samples/
│   └── CrewTech.Notify.WindowsClient/     # Windows client demo
├── scripts/
│   ├── build.sh                           # Build script (Linux/Mac)
│   ├── build.bat                          # Build script (Windows)
│   └── start.sh                           # Quick start script
└── docs/                                  # Additional documentation
```

## 🛠️ Development

### Adding a New Provider

1. Implement `INotificationProvider`:
```csharp
public class SlackNotificationProvider : INotificationProvider
{
    public string Platform => "Slack";
    
    public async Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        // Implementation here
    }
}
```

2. Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<INotificationProvider, SlackNotificationProvider>();
```

### Database Migrations

Using Entity Framework Core Migrations:
```bash
# Add migration
dotnet ef migrations add InitialCreate \
  --project src/CrewTech.Notify.Infrastructure \
  --startup-project src/CrewTech.Notify.SenderApi

# Apply migration
dotnet ef database update \
  --project src/CrewTech.Notify.Infrastructure \
  --startup-project src/CrewTech.Notify.SenderApi
```

## 📦 Deployment

### Docker (Coming Soon)
```bash
docker-compose up
```

### Azure App Service
1. Publish API: `dotnet publish src/CrewTech.Notify.SenderApi -c Release`
2. Deploy Worker as Azure Function or Container Instance
3. Configure connection string to Azure SQL Database

## 🔐 Security Considerations

- ✅ Secrets stored in configuration (use Azure Key Vault in production)
- ✅ HTTPS enforced in production
- ✅ Input validation on all API endpoints
- ✅ Rate limiting recommended for public APIs
- ✅ Idempotency prevents replay attacks

## 🎯 Roadmap

- [ ] iOS APNS Provider
- [ ] Slack Webhook Provider
- [ ] Zulip Integration
- [ ] Mattermost Integration
- [ ] Web Push Notifications
- [ ] Email Provider
- [ ] SMS Provider (Twilio)
- [ ] Notification Templates
- [ ] User Preferences & Opt-out
- [ ] Analytics Dashboard
- [ ] Docker Support
- [ ] Kubernetes Helm Charts

## 📄 License

MIT License - See LICENSE file for details

## 🤝 Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/DaithiBradley/CrewTech-Notify/issues)
- **Discussions**: [GitHub Discussions](https://github.com/DaithiBradley/CrewTech-Notify/discussions)

## 🌟 Credits

Built with ❤️ using .NET 8, Entity Framework Core, and modern software engineering practices.

