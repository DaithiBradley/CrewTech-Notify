# Retry Strategy

## Overview
CrewTech-Notify uses exponential backoff with jitter to handle transient failures gracefully. The system automatically retries failed notifications based on the failure category, ensuring reliable delivery while respecting rate limits and avoiding unnecessary retries for terminal errors.

## Failure Classification

The system classifies all notification failures into categories that determine retry behavior:

| Failure Category       | HTTP Status | Retryable | Dead-Letter Behavior        | Examples                        |
|------------------------|-------------|-----------|-----------------------------|---------------------------------|
| **NetworkError**       | N/A         | ✅ Yes     | After 5 retries             | Connection timeout, DNS failure |
| **ServiceUnavailable** | 503, 500    | ✅ Yes     | After 5 retries             | Service down, maintenance       |
| **RateLimited**        | 429         | ✅ Yes     | After 5 retries             | Too many requests               |
| **Unknown**            | Other       | ✅ Yes     | After 5 retries             | Unclassified errors             |
| **InvalidToken**       | 404         | ❌ No      | Immediate dead-letter       | Device unregistered             |
| **Unauthorized**       | 401         | ❌ No      | Immediate dead-letter       | Invalid credentials             |
| **InvalidPayload**     | 400         | ❌ No      | Immediate dead-letter       | Malformed notification          |
| **PlatformNotSupported** | N/A       | ❌ No      | Immediate dead-letter       | Unknown platform                |

## Retry Logic

### Exponential Backoff Formula
```
delay = min(baseDelay * 2^retryCount * (1 ± jitterFactor * random()), maxDelay)
```

**Default Configuration:**
- `baseDelay`: 5 seconds
- `maxDelay`: 300 seconds (5 minutes)
- `jitterFactor`: 0.3 (30% random variance)
- `maxRetries`: 5

### Retry Schedule Example
For a notification with default settings:

| Attempt | Approximate Delay | Time Since First Attempt | Status          |
|---------|-------------------|--------------------------|-----------------|
| 1       | Immediate         | 0s                       | Processing      |
| 2       | ~5s               | 5s                       | Retry 1         |
| 3       | ~10s              | 15s                      | Retry 2         |
| 4       | ~20s              | 35s                      | Retry 3         |
| 5       | ~40s              | 75s                      | Retry 4         |
| 6       | ~80s              | 155s                     | Retry 5 (final) |
| -       | -                 | -                        | Dead-Letter     |

*Note: Actual delays vary due to jitter (±30%), which prevents thundering herd.*

## Configuration

### Application Settings
Configure retry behavior in `appsettings.json`:

```json
{
  "RetryPolicy": {
    "BaseDelaySeconds": 5,
    "MaxDelaySeconds": 300,
    "JitterFactor": 0.3,
    "MaxRetries": 5
  }
}
```

### Per-Notification Configuration
Override max retries when queuing a notification:

```json
{
  "targetPlatform": "FCM",
  "deviceToken": "device-token-123",
  "title": "Critical Alert",
  "body": "This requires more retries",
  "maxRetries": 10
}
```

## Testing Retry Logic

### Using FakeProvider
The FakeProvider simulates transient failures with a 5% random failure rate, perfect for testing retry logic locally.

**Send 100 test notifications:**
```bash
for i in {1..100}; do
  curl -X POST http://localhost:5000/api/notifications \
    -H "Content-Type: application/json" \
    -d '{
      "targetPlatform": "Fake",
      "deviceToken": "test-device",
      "title": "Test Notification '$i'",
      "body": "Testing retry logic"
    }'
done
```

**Check retry statistics:**
```bash
# View failed notifications with retry counts
sqlite3 notifications.db "
  SELECT 
    Id, 
    Title, 
    RetryCount, 
    Status, 
    ErrorMessage,
    UpdatedAt
  FROM NotificationMessages 
  WHERE Status IN ('Failed', 'DeadLettered')
  ORDER BY UpdatedAt DESC
  LIMIT 20;
"
```

**Monitor retry progression:**
```bash
# Watch notifications being retried
watch -n 2 "sqlite3 notifications.db '
  SELECT 
    Status, 
    COUNT(*) as Count,
    AVG(RetryCount) as AvgRetries
  FROM NotificationMessages 
  GROUP BY Status;
'"
```

## Provider-Specific Retry Behavior

### Windows Push Notification Service (WNS)
- **503 Service Unavailable**: Retried (WNS infrastructure issue)
- **500 Internal Server Error**: Retried (transient WNS error)
- **429 Too Many Requests**: Retried with backoff
- **404 Not Found**: Dead-lettered (invalid channel URI)
- **401 Unauthorized**: Dead-lettered (authentication failure)
- **400 Bad Request**: Dead-lettered (invalid XML payload)

### Firebase Cloud Messaging (FCM)
- **503 Service Unavailable**: Retried (FCM down)
- **500 Internal Server Error**: Retried (transient error)
- **429 Too Many Requests**: Retried with backoff
- **404 Not Found**: Dead-lettered (invalid token)
- **401 Unauthorized**: Dead-lettered (invalid server key)
- **400 Bad Request**: Dead-lettered (malformed JSON)

### FakeProvider (Development)
- **5% random failure rate**: Simulates transient errors
- **Error code**: `FAKE_TRANSIENT`
- **Category**: `ServiceUnavailable`
- **Always retryable**: Yes

## Dead-Letter Queue

### When Notifications Are Dead-Lettered
1. **Max retries exceeded**: After 5 failed attempts (configurable)
2. **Terminal errors**: Immediate dead-letter for non-retryable failures
3. **Platform not supported**: No registered provider for platform

### Querying Dead-Lettered Notifications
```sql
-- Get all dead-lettered notifications
SELECT 
  Id,
  TargetPlatform,
  DeviceToken,
  Title,
  RetryCount,
  ErrorMessage,
  CreatedAt,
  UpdatedAt
FROM NotificationMessages
WHERE Status = 'DeadLettered'
ORDER BY UpdatedAt DESC;

-- Get dead-letter reasons summary
SELECT 
  ErrorMessage,
  COUNT(*) as Count
FROM NotificationMessages
WHERE Status = 'DeadLettered'
GROUP BY ErrorMessage
ORDER BY Count DESC;

-- Get dead-lettered by platform
SELECT 
  TargetPlatform,
  COUNT(*) as Count,
  AVG(RetryCount) as AvgRetriesBeforeFailing
FROM NotificationMessages
WHERE Status = 'DeadLettered'
GROUP BY TargetPlatform;
```

### Handling Dead-Lettered Notifications
1. **Review**: Investigate common error patterns
2. **Fix**: Update device tokens, credentials, or notification format
3. **Requeue**: Manually reset status to `Pending` to retry:
   ```sql
   UPDATE NotificationMessages
   SET Status = 0, RetryCount = 0, ErrorMessage = NULL
   WHERE Id = 'notification-guid-here';
   ```

## Monitoring and Alerts

### Key Metrics to Monitor
- **Dead-letter rate**: Percentage of notifications dead-lettered
- **Average retry count**: How many retries before success
- **Retry delay distribution**: Are notifications backing off properly?
- **Error category distribution**: Which failure types are most common?

### Sample Monitoring Query
```sql
-- Notification health summary (last 24 hours)
SELECT 
  Status,
  COUNT(*) as Count,
  AVG(RetryCount) as AvgRetries,
  COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() as Percentage
FROM NotificationMessages
WHERE CreatedAt >= datetime('now', '-1 day')
GROUP BY Status
ORDER BY Count DESC;
```

### Recommended Alerts
- Alert if dead-letter rate > 5% (indicates systemic issue)
- Alert if any notification exceeds 4 retries (manual review needed)
- Alert if rate-limited errors > 10% (adjust sending rate)

## Best Practices

### When to Use Retries
✅ **Do retry:**
- Transient network errors
- Service temporarily unavailable (503)
- Rate limiting (with backoff)
- Timeout errors

❌ **Don't retry:**
- Invalid device tokens (update token instead)
- Authentication failures (fix credentials first)
- Malformed payloads (fix code bug)
- Platform not supported (add provider)

### Optimizing Retry Strategy
1. **Set appropriate maxRetries**: Balance between reliability and timeliness
2. **Monitor failure categories**: Identify and fix root causes
3. **Use jitter**: Prevents thundering herd when many notifications fail simultaneously
4. **Adjust baseDelay**: Faster for critical notifications, slower for non-urgent
5. **Clean up dead-letters**: Periodically review and remove old dead-lettered notifications

## Troubleshooting

### Notification Stuck in Retry Loop
**Symptom:** Notification keeps failing and retrying
**Cause:** Retryable error persists (e.g., service down)
**Solution:** Check provider status, verify credentials, review error logs

### All Notifications Immediately Dead-Lettered
**Symptom:** No retries attempted
**Cause:** Terminal error on first attempt
**Solution:** Check error category, fix root cause (credentials, tokens, format)

### Retries Not Backing Off
**Symptom:** Retries happen too quickly
**Cause:** Worker polling interval too short, or backoff calculation issue
**Solution:** Verify `RetryPolicy` configuration, check worker logs

### Too Many Retries
**Symptom:** Notifications retrying beyond maxRetries
**Cause:** Retry count not incrementing properly
**Solution:** Check repository `MarkAsFailedAsync` implementation

## See Also
- [Provider Extension Guide](PROVIDER_EXTENSION.md)
- [Architecture Documentation](../README.md#architecture-guarantees)
