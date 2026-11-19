using System.Security.Claims;
using System.Security.Cryptography;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fatewake.Api.Authentication;

/// <summary>Manages registration and revocation of OpenIddict applications without exposing stored secrets.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/AdminApplicationEndpoints.md">Documentation</see>
public sealed class AdminApplicationEndpoints(FatewakeDbContext db, IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens, IConfiguration configuration,
    IHostEnvironment environment, ILogger<AdminApplicationEndpoints> log)
{
    /// <summary>Maps the identity administrator API; every operation uses canonical API authorization.</summary>
    public static void Map(WebApplication app)
    {
        var routes = app.MapGroup("/api/admin/applications").RequireAuthorization("IdentityAdministrator");
        routes.MapGet("/", (AdminApplicationEndpoints service, string? search, int? page, CancellationToken ct) =>
            service.ListAsync(search, page ?? 1, ct));
        routes.MapGet("/{id}", (AdminApplicationEndpoints service, string id, CancellationToken ct) =>
            service.ReadAsync(id, ct));
        routes.MapPost("/", (AdminApplicationEndpoints service, HttpContext context, AdminApplicationRequest request, CancellationToken ct) =>
            service.SaveAsync(context, null, request, false, ct));
        routes.MapPut("/{id}", (AdminApplicationEndpoints service, HttpContext context, string id, AdminApplicationRequest request, CancellationToken ct) =>
            service.SaveAsync(context, id, request, false, ct));
        routes.MapPost("/{id}/rotate-secret", (AdminApplicationEndpoints service, HttpContext context, string id, string version, CancellationToken ct) =>
            service.SaveAsync(context, id, new(null, null, null, null, null, false, version), true, ct));
        routes.MapDelete("/{id}", (AdminApplicationEndpoints service, HttpContext context, string id, string version, string confirm, CancellationToken ct) =>
            service.DeleteAsync(context, id, version, confirm, ct));
    }

    private IQueryable<OpenIddictEntityFrameworkCoreApplication> RegistrationRows =>
        db.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking();

    private async Task<IResult> ListAsync(string? search, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || search?.Length > 100) return Results.BadRequest();
        using var operation = OperationTelemetry.Start("admin.oidc_list", log);
        var query = RegistrationRows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(row => row.ClientId!.ToLower().Contains(term) || row.DisplayName!.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(row => row.ClientId).ThenBy(row => row.Id).Skip((page - 1) * 25).Take(25)
            .Select(row => new { row.Id, row.ClientId, row.DisplayName, row.ClientType }).ToListAsync(ct);
        return Results.Ok(new { total, rows });
    }

    private async Task<IResult> ReadAsync(string id, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("admin.oidc_read", log);
        if (await applications.FindByIdAsync(id, ct) is not OpenIddictEntityFrameworkCoreApplication app)
            return Results.NotFound();
        var isProtected = IsSeeded(app);
        return Results.Ok(new
        {
            app.Id,
            app.ClientId,
            app.DisplayName,
            app.ClientType,
            version = app.ConcurrencyToken,
            isProtected,
            requiredRedirectUri = isProtected ? OpenIddictEndpoints.ConfiguredRedirectUri(configuration).AbsoluteUri : null,
            requiredPostLogoutUri = isProtected ? OpenIddictEndpoints.ConfiguredPostLogoutUri(configuration).AbsoluteUri : null,
            redirectUris = await applications.GetRedirectUrisAsync(app, ct),
            postLogoutRedirectUris = await applications.GetPostLogoutRedirectUrisAsync(app, ct),
            allowRefreshTokens = await applications.HasPermissionAsync(app, Permissions.GrantTypes.RefreshToken, ct)
        });
    }

    private async Task<IResult> SaveAsync(HttpContext context, string? id, AdminApplicationRequest request, bool rotate, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("admin.oidc_write", log);
        context.Response.Headers.CacheControl = "no-store";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (id is null)
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(861017235)", ct);
        var app = id is null ? null : await applications.FindByIdAsync(id, ct) as OpenIddictEntityFrameworkCoreApplication;
        if (id is not null && app is null) return Results.NotFound();
        if (app is not null && app.ConcurrencyToken != request.Version)
            return Results.Conflict(new { error = "The application was changed. Reload the details." });
        var descriptor = new OpenIddictApplicationDescriptor();
        if (app is not null) await applications.PopulateAsync(descriptor, app, ct);
        var isSeeded = app is not null && IsSeeded(app);
        string? secret = null;
        if (isSeeded)
        {
            if (rotate || request.ClientId != descriptor.ClientId || request.ClientType != descriptor.ClientType ||
                request.DisplayName != descriptor.DisplayName ||
                request.AllowRefreshTokens != descriptor.Permissions.Contains(Permissions.GrantTypes.RefreshToken))
                return Results.Conflict(new { error = "Only callback URLs can be edited on the seeded Fatewake Web client." });
            if (request.RedirectUris is not { Length: > 0 and <= 12 } redirects || request.PostLogoutRedirectUris is not { Length: <= 12 } logout ||
                !TryUris(redirects, out var callbacks) || !TryUris(logout, out var logoutCallbacks))
                return Results.BadRequest(new { error = "Provide valid, exact callback URLs." });
            var requiredRedirect = OpenIddictEndpoints.ConfiguredRedirectUri(configuration);
            var requiredLogout = OpenIddictEndpoints.ConfiguredPostLogoutUri(configuration);
            if (!callbacks.Contains(requiredRedirect) || !logoutCallbacks.Contains(requiredLogout))
                return Results.Conflict(new { error = "Keep the configured Fatewake Web redirect and post-logout URLs. Change the service configuration before removing them." });
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.UnionWith(callbacks);
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.UnionWith(logoutCallbacks);
        }
        else if (rotate)
        {
            if (descriptor.ClientType != ClientTypes.Confidential) return Results.BadRequest(new { error = "Only confidential clients have secrets." });
            secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            descriptor.ClientSecret = secret;
        }
        else
        {
            var clientId = request.ClientId?.Trim();
            var name = request.DisplayName?.Trim();
            if (clientId is not { Length: > 0 and <= 100 } || clientId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')) ||
                name is not { Length: > 0 and <= 100 } ||
                request.ClientType is not (ClientTypes.Public or ClientTypes.Confidential) ||
                request.RedirectUris is not { Length: > 0 and <= 12 } redirects || request.PostLogoutRedirectUris is not { Length: <= 12 } logout ||
                !TryUris(redirects, out var callbacks) || !TryUris(logout, out var logoutCallbacks))
                return Results.BadRequest(new { error = "Provide a valid client ID, display name, type, and exact callback URLs." });
            if (app is not null && app.ClientId != clientId)
                return Results.BadRequest(new { error = "Client ID cannot change after creation." });
            if (app is null && await applications.FindByClientIdAsync(clientId, ct) is not null)
                return Results.Conflict(new { error = "This client ID is already registered." });
            descriptor.ClientId = clientId;
            descriptor.ClientType = request.ClientType;
            descriptor.DisplayName = name;
            descriptor.ConsentType = ConsentTypes.Explicit;
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.UnionWith(callbacks);
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.UnionWith(logoutCallbacks);
            descriptor.Permissions.Clear();
            descriptor.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "fatewake-api"]);
            if (logoutCallbacks.Count > 0) descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
            if (request.AllowRefreshTokens) descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
            if (descriptor.ClientType == ClientTypes.Confidential && app?.ClientType != ClientTypes.Confidential)
            {
                secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                descriptor.ClientSecret = secret;
            }
            if (descriptor.ClientType == ClientTypes.Public) descriptor.ClientSecret = null;
        }

        try
        {
            if (app is null)
                id = await applications.GetIdAsync(await applications.CreateAsync(descriptor, ct), ct);
            else
            {
                if (rotate || app.ClientType != descriptor.ClientType)
                {
                    await tokens.RevokeByApplicationIdAsync(id!, ct);
                    await authorizations.RevokeByApplicationIdAsync(id!, ct);
                }
                await applications.UpdateAsync(app, descriptor, ct);
            }
            await AuditAsync(context, id!, rotate ? "application.rotate" : app is null ? "application.create" : "application.update", ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { id, secret });
        }
        catch (OpenIddictExceptions.ConcurrencyException ex)
        {
            operation.Fail(ex);
            return Results.Conflict(new { error = "The application was changed. Reload the details." });
        }
        catch (OpenIddictExceptions.ValidationException ex)
        {
            operation.Fail(ex);
            return Results.BadRequest(new { error = "OpenIddict rejected the application settings." });
        }
    }

    private async Task<IResult> DeleteAsync(HttpContext context, string id, string version, string confirm, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("admin.oidc_delete", log);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await applications.FindByIdAsync(id, ct) is not OpenIddictEntityFrameworkCoreApplication app)
            return Results.NotFound();
        if (IsSeeded(app) || app.ConcurrencyToken != version)
            return Results.Conflict(new { error = "The application is protected or was changed. Reload the details." });
        if (!string.Equals(confirm, app.ClientId, StringComparison.Ordinal))
            return Results.BadRequest(new { error = "Enter the exact client ID to confirm removal." });
        try
        {
            await tokens.RevokeByApplicationIdAsync(id, ct);
            await authorizations.RevokeByApplicationIdAsync(id, ct);
            await applications.DeleteAsync(app, ct);
            await AuditAsync(context, id, "application.delete", ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        }
        catch (OpenIddictExceptions.ConcurrencyException ex)
        {
            operation.Fail(ex);
            return Results.Conflict(new { error = "The application was changed. Reload the details." });
        }
    }

    private bool IsSeeded(OpenIddictEntityFrameworkCoreApplication app) =>
        app.ClientId == (configuration["OpenIddictWeb:ClientId"] ?? "fatewake-web");

    private bool TryUris(string[] values, out HashSet<Uri> uris)
    {
        uris = [];
        foreach (var value in values)
        {
            if (value is not { Length: > 0 and <= 2048 } || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Fragment.Length != 0 || uri.Query.Length != 0 || uri.UserInfo.Length != 0 || value.Contains('*') ||
                (uri.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)) ||
                !uris.Add(uri)) return false;
        }
        return true;
    }

    private async Task AuditAsync(HttpContext context, string target, string action, CancellationToken ct)
    {
        var actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity_admin_audit (actor, target, action, result, occurred_at, correlation)
            VALUES ({actor}, {target}, {action}, 'succeeded', {DateTimeOffset.UtcNow}, {context.TraceIdentifier})
            """, ct);
        log.LogInformation("Admin mutation {Action} targeted application {ApplicationId} by {AdminAccountId}", action, target, actor);
    }
}
