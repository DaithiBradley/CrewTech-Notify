# Testing with Real Devices

This guide explains how to test CrewTech-Notify with real WNS/FCM credentials on actual devices.

## Overview

By default, all tests use **FakeProvider** which doesn't require credentials. To test with real devices, you must explicitly enable real credential mode and provide your credentials.

## Configuration Methods

### Option 1: Local Configuration File (Recommended)

1. Create `appsettings.Test.Local.json` in `tests/CrewTech.Notify.Infrastructure.Tests/`:

```json
{
  "TestConfiguration": {
    "UseRealCredentials": true,
    "ApiBaseUrl": "http://localhost:5000"
  },
  "WNS": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-secret",
    "TenantId": "your-tenant"
  },
  "FCM": {
    "ProjectId": "your-project",
    "ServerKey": "your-key"
  },
  "TestDevices": {
    "WNS": {
      "DeviceToken": "your-windows-channel-uri"
    },
    "FCM": {
      "DeviceToken": "your-android-token"
    }
  }
}
```

2. Run tests:
```bash
cd tests/CrewTech.Notify.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~RealDeviceTests"
```

### Option 2: User Secrets (Most Secure)

```bash
cd tests/CrewTech.Notify.Infrastructure.Tests

dotnet user-secrets set "TestConfiguration:UseRealCredentials" "true"
dotnet user-secrets set "WNS:ClientId" "your-client-id"
dotnet user-secrets set "WNS:ClientSecret" "your-secret"
dotnet user-secrets set "WNS:TenantId" "your-tenant"
dotnet user-secrets set "TestDevices:WNS:DeviceToken" "your-device-token"

dotnet test --filter "FullyQualifiedName~RealDeviceTests"
```

### Option 3: Environment Variables (CI/CD)

```bash
# Linux/Mac
export CREWTECH_TEST_TestConfiguration__UseRealCredentials=true
export CREWTECH_TEST_WNS__ClientId=your-client-id
export CREWTECH_TEST_TestDevices__WNS__DeviceToken=your-token

# Windows PowerShell
$env:CREWTECH_TEST_TestConfiguration__UseRealCredentials="true"
$env:CREWTECH_TEST_WNS__ClientId="your-client-id"

dotnet test --filter "FullyQualifiedName~RealDeviceTests"
```

## Running on Separate Devices

To test from one machine (server) to another (mobile device):

1. **On your server** (where API runs):
   ```bash
   cd src/CrewTech.Notify.SenderApi
   dotnet run
   ```

2. **On your test machine** (where tests run):
   - Configure credentials as shown above
   - Ensure API is accessible (use server IP if different machine)
   - Update `ApiBaseUrl` in configuration: `"ApiBaseUrl": "http://192.168.1.100:5000"`

3. **On your mobile device**:
   - Install your app that registers for push notifications
   - Get the device token from your app
   - Add token to test configuration

4. **Run tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~RealDeviceTests" --logger "console;verbosity=detailed"
   ```

## Drawbacks vs Build-and-Install

### This Approach (dotnet test with real credentials)

**Pros:**
- ✅ Fast iteration - no build/deploy cycle
- ✅ Easy automation
- ✅ Multiple scenarios quickly
- ✅ Integrated with test framework

**Cons:**
- ❌ Doesn't test app integration
- ❌ No UI validation
- ❌ Doesn't test app lifecycle
- ❌ Requires managing device tokens manually
- ❌ Not true E2E (doesn't validate user experience)

### Build-and-Install Approach

**Pros:**
- ✅ True end-to-end testing
- ✅ Tests actual user experience
- ✅ Validates notification UI, actions, deep links
- ✅ Tests real environment

**Cons:**
- ❌ Slower (build, sign, deploy)
- ❌ More complex setup
- ❌ Device management overhead

## Recommendation

Use **both**:
1. **During development**: Use this approach for rapid validation
2. **Before release**: Use build-and-install with UI automation (Appium, Playwright)

## Security

🔒 **Never commit credentials!**

- ✅ Use `appsettings.Test.Local.json` (git-ignored)
- ✅ Use User Secrets locally
- ✅ Use CI/CD secret stores for automation
- ❌ Don't use production credentials
