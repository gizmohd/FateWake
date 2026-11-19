using Fatewake.Web.Features;
using Fatewake.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "fatewake.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
}).AddExternalSignIn(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.Configure<CharacterCustomizationOptions>(builder.Configuration.GetSection(CharacterCustomizationOptions.SectionName));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticatedApiClient>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("local-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHttpClient("fatewake-api", client => client.BaseAddress = new("https+http://api"));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/verify-email"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next(context);
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapLocalAuthentication();
// Keep artwork same-origin while the API owns approved assets and binary storage.
app.MapGet("/art/{key}/{version:int}/{format}", async (string key, int version, string format,
    IHttpClientFactory clients, HttpContext context, CancellationToken ct) =>
{
    if (version < 1 || format is not ("png" or "webp")) return Results.NotFound();
    using var request = new HttpRequestMessage(HttpMethod.Get,
        $"/api/art/{Uri.EscapeDataString(key)}/{version}/{format}");
    if (context.User.Identity?.IsAuthenticated == true && context.User.FindFirst("fatewake.api_token") is { } token)
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
    foreach (var header in new[] { "If-None-Match", "If-Modified-Since" })
        if (context.Request.Headers.TryGetValue(header, out var values))
            request.Headers.TryAddWithoutValidation(header, values.ToArray());

    using var response = await clients.CreateClient("fatewake-api").SendAsync(
        request, HttpCompletionOption.ResponseHeadersRead, ct);
    context.Response.StatusCode = (int)response.StatusCode;
    if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotModified)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Empty;
    }
    foreach (var header in new[] { "Cache-Control", "ETag", "Last-Modified", "Expires", "Content-Length" })
    {
        if (response.Headers.TryGetValues(header, out var values) ||
            response.Content.Headers.TryGetValues(header, out values))
            context.Response.Headers[header] = values.ToArray();
    }
    if (!response.IsSuccessStatusCode) return Results.Empty;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "image/" + format;
    if (context.Response.Headers.CacheControl.ToString().Contains("private", StringComparison.OrdinalIgnoreCase))
        context.Response.Headers.Vary = "Cookie";
    await response.Content.CopyToAsync(context.Response.Body, ct);
    return Results.Empty;
});
app.MapRazorComponents<Fatewake.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
