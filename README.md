# sebt-self-service-portal-co-connector

A repository containing the implementation for the Colorado backend connector.

## CI

The GitHub Actions workflow (`.github/workflows/ci.yml`) builds and tests the connector on every push and PR to `main`.

### Required credentials

The CI workflow uses `GITHUB_TOKEN` (provided automatically) to check out the [state-connector](https://github.com/codeforamerica/sebt-self-service-portal-state-connector) repo for the interface package. No additional secrets are required for CI to pass — all tests that need external credentials skip gracefully when they are not configured.

### Optional: CBMS sandbox integration tests

CI runs CBMS integration tests with mock responses by default, so no secrets are required for the build to pass (health checks runs are skipped!). To test against the real sandbox, add these as GitHub Actions **repository secrets** and set `Cbms__UseMockResponses=false` in the workflow:

| Secret | Description |
|--------|-------------|
| `CBMS_SANDBOX_CLIENT_ID` | OAuth client ID for the CBMS sandbox (UAT) environment |
| `CBMS_SANDBOX_CLIENT_SECRET` | OAuth client secret for the CBMS sandbox (UAT) environment |

For local development, use .NET user secrets instead:

```bash
cd src/SEBT.Portal.StatePlugins.CO.Tests
dotnet user-secrets set "Cbms:SandboxClientId" "<id>"
dotnet user-secrets set "Cbms:SandboxClientSecret" "<secret>"
```

### Optional: Run integration tests with mock responses

To run the CBMS integration tests without real credentials or network access, enable mock responses:

```bash
cd src/SEBT.Portal.StatePlugins.CO.Tests
dotnet user-secrets set "Cbms:UseMockResponses" "true"
```

Or use an environment variable: `Cbms__UseMockResponses=true`