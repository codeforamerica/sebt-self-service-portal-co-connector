# SEBT.Portal.StatePlugins.CO.CbmsApi

Kiota-generated C# client for the Colorado CBMS SEBT API. This project provides strongly-typed request/response models and request builders for four API endpoints:

- **Ping** — Health check to verify the CBMS API is reachable
- **Check Enrollment** — Look up student enrollment eligibility
- **Get Account Details** — Retrieve household and student details by phone number
- **Update Student Details** — Update address, guardian info, and notification preferences

## Setup

The CBMS API uses OAuth 2.0 client credentials for authentication. Kiota's `BaseBearerTokenAuthenticationProvider` handles adding the `Authorization: Bearer {token}` header to each request — you provide the token acquisition logic by implementing `IAccessTokenProvider`.

### 1. Implement `IAccessTokenProvider`

`IAccessTokenProvider` is the Kiota interface for token acquisition:

```csharp
public interface IAccessTokenProvider
{
    Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = default,
        CancellationToken cancellationToken = default);

    AllowedHostsValidator AllowedHostsValidator { get; }
}
```

Create an implementation that obtains an OAuth client credentials token from the CBMS token endpoint:

```csharp
using Microsoft.Kiota.Abstractions.Authentication;

public class CbmsAccessTokenProvider : IAccessTokenProvider
{
    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = default,
        CancellationToken cancellationToken = default)
    {
        // TODO: Acquire an OAuth 2.0 client credentials token from the
        // CBMS token endpoint using the configured client ID and secret.
        // Cache/refresh the token as appropriate.
        throw new NotImplementedException();
    }
}
```

### 2. Wire up the client

Pass the token provider through `BaseBearerTokenAuthenticationProvider` into the request adapter:

```csharp
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

var tokenProvider = new CbmsAccessTokenProvider(/* client ID, secret, token endpoint */);
var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.example.com")
};
var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient);
var client = new CbmsSebtApiClient(adapter);
```

All types above (`IAccessTokenProvider`, `BaseBearerTokenAuthenticationProvider`, `HttpClientRequestAdapter`) are included in the `Microsoft.Kiota.Bundle` package — no extra dependencies needed.

## Usage

### Ping

```csharp
UntypedNode? result = await client.Ping.GetAsync();
```

Returns an `UntypedNode?` — the response shape is unspecified in the OpenAPI spec. This endpoint is useful for health checks to verify the CBMS API is reachable.

### Check Enrollment

```csharp
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

var response = await client.Sebt.CheckEnrollment.PostAsync(
[
    new CheckEnrollmentRequest
    {
        StdFirstName = "John",
        StdLastName = "Doe",
        StdDob = "2015-05-15",
        CbmsCsId = "C12345",
        StdSasId = "S98765",
        StdSchlCd = "SCH44",
        SebtYear = "2024",
        StdReqInd = "REQ-001"
    }
]);

foreach (var student in response.StdntDtls)
{
    Console.WriteLine($"{student.StdFstNm} {student.StdLstNm}: {student.StdntEligSts}");
    Console.WriteLine($"  Match confidence: {student.MtchCnfd}");
}
```

Note: the request body is a `List<CheckEnrollmentRequest>` — multiple students can be checked in a single call.

### Get Account Details

```csharp
var response = await client.Sebt.GetAccountDetails.PostAsync(
    new GetAccountDetailsRequest
    {
        PhnNm = "3035550199"
    });

foreach (var student in response.StdntEnrollDtls)
{
    Console.WriteLine($"{student.StdFstNm} {student.StdLstNm}");
    Console.WriteLine($"  Status: {student.StdntEligSts}, Card: {student.EbtCardSts}");
    Console.WriteLine($"  Balance: {student.CardBal}");
}
```

### Update Student Details

```csharp
var response = await client.Sebt.UpdateStdDtls.PatchAsync(
    new UpdateStudentDetailsRequest
    {
        SebtChldId = "CH-88291",
        SebtAppId = "APP-12345",
        Addr = new Address
        {
            AddrLn1 = "456 Oak Street",
            AddrLn2 = "Suite 200",
            Cty = "Denver",
            StaCd = "CO",
            Zip = "80202",
            Zip4 = "4421"
        },
        GurdFstNm = "Jane",
        GurdLstNm = "Smith",
        GurdEmailAddr = "jane.smith@example.com",
        ReqNewCard = "Y",
        NtfnOptInSw = "Y",
        NtfnSrc = "email",
        OptOut = "N"
    });

Console.WriteLine($"Response: {response.RespCd} — {response.RespMsg}");
```

### Error handling

All three endpoints throw `ErrorResponse` (which extends `ApiException`) on HTTP 400, 404, and 500 responses:

```csharp
try
{
    var response = await client.Sebt.GetAccountDetails.PostAsync(request);
}
catch (ErrorResponse ex)
{
    Console.WriteLine($"API: {ex.ApiName}, Correlation: {ex.CorrelationId}");
    foreach (var error in ex.ErrorDetails)
    {
        Console.WriteLine($"  {error.Code}: {error.Message}");
    }
}
```

## Code generation

The C# code in this project is auto-generated by [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) from a transformed OpenAPI spec. Do not edit the generated `.cs` files directly — they will be overwritten on regeneration.

The `openapi/` directory contains the spec, transformation scripts, and generation script. See [openapi/README.md](openapi/README.md) for the workflow when the spec changes.

## See also

- [ADR-0002: Kiota-generated API client for CBMS SEBT API](../../docs/adr/0002-kiota-generated-api-client-for-cbms-sebt-api.md)
