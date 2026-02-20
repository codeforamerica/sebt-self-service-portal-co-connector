# MyColorado OIDC Test Host

Minimal web app to test the real MyColorado (PingOne) OIDC flow locally.

## Requirements to run

1. **Development environment:** `ASPNETCORE_ENVIRONMENT=Development`
2. **Opt-in flag:** `TestHost:Enabled=true` in configuration (or env `TestHost__Enabled=true`)
3. **MyColorado credentials:** `MyColorado:ClientId` and `MyColorado:ClientSecret` (e.g. in `appsettings.Development.json`)

Without the flag, the app will not start and will throw: *"MyColoradoOidc.TestHost is disabled. Set TestHost:Enabled=true in configuration to run."*

## Configure (e.g. appsettings.Development.json, gitignored)

```json
{
  "TestHost": {
    "Enabled": true
  },
  "MyColorado": {
    "ClientId": "your-pingone-client-id",
    "ClientSecret": "your-pingone-client-secret",
    "Scopes": "openid profile"
  }
}
```

Redirect URI must be `http://localhost:8080/callback` in PingOne. Then open http://localhost:8080/login to start the flow.
