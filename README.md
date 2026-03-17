# sebt-self-service-portal-co-connector

A repository containing the implementation for the Colorado backend connector.

## CI

The GitHub Actions workflow (`.github/workflows/ci.yml`) builds and tests the connector on every push and PR to `main`.

### Required credentials

The CI workflow uses `GITHUB_TOKEN` (provided automatically) to check out the [state-connector](https://github.com/codeforamerica/sebt-self-service-portal-state-connector) repo for the interface package. No additional secrets are required for CI to pass — all tests that need external credentials skip gracefully when they are not configured.

### Optional: CBMS sandbox integration tests

The CI workflow passes CBMS API credentials from repository secrets. To enable these tests, add these as GitHub Actions **repository secrets**:

| Secret | Description |
|--------|-------------|
| `CBMS_CLIENT_ID` | OAuth client ID for the CBMS environment |
| `CBMS_CLIENT_SECRET` | OAuth client secret for the CBMS environment |

For local development, use .NET user secrets instead:

```bash
cd src/SEBT.Portal.StatePlugins.CO.Tests
dotnet user-secrets set "Cbms:ClientId" "<id>"
dotnet user-secrets set "Cbms:ClientSecret" "<secret>"
```
