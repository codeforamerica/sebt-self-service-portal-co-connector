using SEBT.Portal.StatePlugins.CO.MyColorado;

var builder = WebApplication.CreateBuilder(args);

// Test Host is for local development only; refuse to run in Production.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "MyColoradoOidc.TestHost is for development only. Set ASPNETCORE_ENVIRONMENT=Development to run.");
}

// Require an explicit opt-in flag so the Test Host is not started by accident.
var enableTestHost = builder.Configuration.GetValue<bool>("TestHost:Enabled");
if (!enableTestHost)
{
    throw new InvalidOperationException(
        "MyColoradoOidc.TestHost is disabled. Set TestHost:Enabled=true in configuration (e.g. appsettings.Development.json) to run.");
}

// Bind MyColorado options (ClientId, ClientSecret from config or env)
var myColoradoSection = builder.Configuration.GetSection("MyColorado");
var options = new MyColoradoOidcOptions();
myColoradoSection.Bind(options);
// Override redirect so callback is this host on port 8080
options.RedirectUris = new List<string> { "http://localhost:8080/callback" };

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IMyColoradoPendingLoginStore, InMemoryPendingLoginStore>();
builder.Services.AddHttpClient<MyColoradoOidcService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "MyColorado OIDC test host",
    endpoints = new
    {
        login = "GET /login - redirects to MyColorado sign-in",
        callback = "GET /callback - receives code from MyColorado, exchanges for tokens"
    },
    config = new
    {
        clientIdSet = !string.IsNullOrEmpty(options.ClientId),
        clientSecretSet = !string.IsNullOrEmpty(options.ClientSecret),
        redirectUri = "http://localhost:8080/callback"
    }
}));

app.MapGet("/login", async (MyColoradoOidcService service) =>
{
    const string redirectUri = "http://localhost:8080/callback";
    var (authorizationUrl, state) = await service.PrepareAuthorizationAsync(redirectUri);
    return Results.Redirect(authorizationUrl);
});

app.MapGet("/callback", async (string? code, string? state, MyColoradoOidcService service, ILoggerFactory loggerFactory) =>
{
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        return Results.BadRequest("Missing code or state. Start from GET /login first.");

    try
    {
        var result = await service.ExchangeCodeForTokensAsync(code, state);
        return Results.Ok(new
        {
            success = true,
            accessTokenLength = result.AccessToken.Length,
            idTokenLength = result.IdToken.Length,
            idTokenClaims = result.IdTokenClaims,
            idTokenPreview = result.IdToken.Length > 80 ? result.IdToken[..80] + "..." : result.IdToken
        });
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("MyColoradoOidc.Callback").LogError(ex, "Token exchange or validation failed");
        return Results.BadRequest(new { success = false, error = "Authentication failed." });
    }
});

app.Run("http://localhost:8080");
