# Architecture Overview

## System Components

### 1. Sender API (REST API)
- **Technology**: ASP.NET Core 8.0 Web API
- **Port**: 5000 (configurable)
- **Responsibility**: Accepts notification requests and stores them in the outbox
- **Features**:
  - RESTful endpoints for notification management
  - Swagger/OpenAPI documentation
  - Idempotency support
  - Input validation
  - Health check endpoint

### 2. Worker Service
- **Technology**: .NET Background Service
- **Responsibility**: Processes notifications from the outbox
- **Features**:
  - Polling-based notification processing
  - Retry logic with exponential backoff and jitter
  - Multi-provider support
  - Dead-letter queue management
  - Graceful shutdown

### 3. Outbox Pattern (Database)
- **Technology**: SQLite (default) / SQL Server
- **Responsibility**: Durable storage for notifications
- **Benefits**:
  - Transactional consistency
  - Guaranteed delivery
  - Audit trail
  - Retry support

## Data Flow

```
┌─────────┐
│ Client  │
└────┬────┘
     │ 1. POST /api/notifications
     ▼
┌─────────────────┐
│   Sender API    │
└────┬────────────┘
     │ 2. Save to Outbox (Status: Pending)
     ▼
┌─────────────────────────────────┐
│          Database               │
│  ┌───────────────────────────┐  │
│  │  NotificationMessages     │  │
│  │  - Id                     │  │
│  │  - IdempotencyKey         │  │
│  │  - Status (Pending)       │  │
│  │  - TargetPlatform         │  │
│  │  - DeviceToken            │  │
│  │  - Title, Body            │  │
│  │  - RetryCount             │  │
│  └───────────────────────────┘  │
└────┬────────────────────────────┘
     │ 3. Worker polls (every 5s)
     ▼
┌─────────────────┐
│  Worker Service │
└────┬────────────┘
     │ 4. Mark as Processing
     │ 5. Send via Provider
     ▼
┌────────────────────┐
│ Provider (WNS/FCM) │
└────┬───────────────┘
     │ 6. Success/Failure
     ▼
┌─────────────────────────────────┐
│          Database               │
│  Status: Sent or Failed         │
│  SentAt or ErrorMessage         │
└─────────────────────────────────┘
```

## Notification States

```
┌─────────┐
│ Pending │  ← Initial state
└────┬────┘
     │
     ▼
┌────────────┐
│ Processing │  ← Worker picked up
└─────┬──────┘
      │
      ├───► Success ──► ┌──────┐
      │                 │ Sent │
      │                 └──────┘
      │
      └───► Failure ──► ┌────────┐       ┌──────────────┐
                        │ Failed │ ────► │ DeadLettered │
                        └────────┘       └──────────────┘
                        (Retry if < MaxRetries)
```

## Retry Strategy

### Exponential Backoff with Jitter

**Formula**: `delay = min(maxDelay, baseDelay * 2^retryCount) ± jitter`

- **Base Delay**: 5 seconds
- **Max Delay**: 300 seconds
- **Jitter**: ±30% randomization
- **Max Retries**: 5

**Example Schedule**:
| Attempt | Base Delay | With Jitter (30%) | Actual Range |
|---------|-----------|-------------------|--------------|
| 1       | 5s        | 3.5s - 6.5s      | ~5s          |
| 2       | 10s       | 7s - 13s         | ~10s         |
| 3       | 20s       | 14s - 26s        | ~20s         |
| 4       | 40s       | 28s - 52s        | ~40s         |
| 5       | 80s       | 56s - 104s       | ~80s         |
| 6       | 160s      | 112s - 208s      | ~160s        |

**Benefits of Jitter**:
- Prevents thundering herd problem
- Distributes load on external services
- Reduces spike in retry attempts

## Provider Architecture

### Provider Interface

```csharp
public interface INotificationProvider
{
    string Platform { get; }
    
    Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
```

### Provider Factory

The `NotificationProviderFactory` maintains a registry of all providers and routes notifications to the appropriate provider based on `TargetPlatform`.

### Extensibility

Adding a new provider:
1. Implement `INotificationProvider`
2. Register in DI container
3. No changes needed to Worker or API

## Idempotency

**Purpose**: Prevent duplicate notifications if client retries

**Mechanism**:
- Client provides `IdempotencyKey` (or auto-generated)
- API checks database for existing notification with same key
- If exists, returns 409 Conflict with existing notification ID
- If not exists, creates new notification

**Example**:
```bash
# First request - creates notification
curl -X POST http://localhost:5000/api/notifications \
  -d '{"idempotencyKey": "order-123-payment-success", ...}'
# Response: 202 Accepted, notificationId: guid-1

# Retry request - returns existing
curl -X POST http://localhost:5000/api/notifications \
  -d '{"idempotencyKey": "order-123-payment-success", ...}'
# Response: 409 Conflict, notificationId: guid-1 (same)
```

## Dead-Letter Queue

**When notifications are dead-lettered**:
- Exceeded max retries (default: 5)
- Non-retryable error (invalid device token, platform not found)
- Manual move via API (future feature)

**Management**:
- Query dead-lettered notifications via database
- Investigate error messages
- Fix issues and requeue manually
- Future: Auto-retry after configured time

## Security Considerations

### API Security
- HTTPS in production
- API key authentication (future)
- Rate limiting (future)
- Input validation
- SQL injection prevention (EF Core parameterized queries)

### Provider Credentials
- Stored in configuration (appsettings.json)
- Production: Use Azure Key Vault or similar
- Never commit credentials to source control

### Data Privacy
- Device tokens stored encrypted (future)
- PII handling compliance
- Message retention policies

## Scalability

### Current Architecture
- Single API instance
- Single Worker instance
- SQLite database

### Scale-Out Strategy
1. **Horizontal API Scaling**
   - Multiple API instances behind load balancer
   - Shared database (migrate to SQL Server)
   - No state in API layer

2. **Horizontal Worker Scaling**
   - Multiple Worker instances
   - Each polls for different notifications
   - Use row-level locking or distributed lock

3. **Database Scaling**
   - SQLite → SQL Server → Azure SQL
   - Read replicas for status queries
   - Partition by date or platform

4. **Message Queue Option**
   - Replace polling with Azure Service Bus / RabbitMQ
   - Push-based notification processing
   - Better scale characteristics

## Monitoring & Observability

### Metrics (Future)
- Notifications sent per second
- Success/failure rates by platform
- Retry rates
- Dead-letter queue depth
- API latency
- Worker processing time

### Logging
- Structured logging (JSON)
- Log levels: Information, Warning, Error
- Correlation IDs for tracing

### Health Checks
- API: GET /health
- Database connectivity
- Provider availability (future)

## Performance Characteristics

### API Latency
- Avg: < 50ms (database write)
- P99: < 200ms

### Worker Throughput
- Current: 10 notifications per batch
- Processing: ~1-2 seconds per notification
- Max throughput: ~300-600 notifications/minute per worker

### Database Performance
- SQLite: Good for < 10K messages/day
- SQL Server recommended for production scale
