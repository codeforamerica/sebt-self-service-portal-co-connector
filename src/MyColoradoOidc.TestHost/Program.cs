using SEBT.Portal.StatePlugins.CO.MyColorado;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "MyColoradoOidc.TestHost is for development only. Set ASPNETCORE_ENVIRONMENT=Development to run.");
}

var enableTestHost = builder.Configuration.GetValue<bool>("TestHost:Enabled");
if (!enableTestHost)
{
    throw new InvalidOperationException(
        "MyColoradoOidc.TestHost is disabled. Set TestHost:Enabled=true in configuration (e.g. appsettings.Development.json) to run.");
}

// Use same config shape as portal: Oidc:co (ClientId, DiscoveryEndpoint).
var oidcCoSection = builder.Configuration.GetSection("Oidc").GetSection("co");
var options = oidcCoSection.Get<MyColoradoOidcOptions>() ?? new MyColoradoOidcOptions();

builder.Services.AddSingleton(options);
builder.Services.AddHttpClient<MyColoradoOidcService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "MyColorado OIDC test host",
    endpoints = new
    {
        authValidate = "POST /auth/validate - validate id_token from frontend (frontend does OIDC + PKCE, sends token here)"
    },
    config = new
    {
        clientIdSet = !string.IsNullOrEmpty(options.ClientId)
    }
}));

app.MapPost("/auth/validate", async (ValidateTokenRequest? body, MyColoradoOidcService service, ILoggerFactory loggerFactory) =>
{
    if (body?.IdToken == null)
        return Results.BadRequest(new { error = "Missing id_token in request body. Send JSON: { \"id_token\": \"<jwt>\" }" });

    try
    {
        var result = await service.ValidateIdTokenAsync(body.IdToken);
        return Results.Ok(new
        {
            success = true,
            idTokenClaims = result.IdTokenClaims,
            idTokenPreview = result.IdToken.Length > 80 ? result.IdToken[..80] + "..." : result.IdToken
        });
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("MyColoradoOidc.Validate").LogError(ex, "ID token validation failed");
        return Results.BadRequest(new { success = false, error = "Invalid or expired id_token." });
    }
});

app.Run("http://localhost:8080");

record ValidateTokenRequest(string? IdToken);
