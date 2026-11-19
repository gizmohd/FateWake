using Fatewake.Infrastructure.Usage;
using Fatewake.Observability;
using System.Globalization;
namespace Fatewake.Api;
/// <summary>Admin-protected read-only operational projections.</summary>
public static class AdminDashboardEndpoints
{
    /// <summary>Maps safe monitoring routes with bounded and validated queries.</summary>
    public static void MapAdminDashboard(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("ArtAdministrator");
        group.MapGet("/summary", (HttpContext context, AdminDashboardService dashboard, CancellationToken ct) =>
            Query(context, f => dashboard.SummaryAsync(f, ct)));
        group.MapGet("/jobs", (HttpContext context, AdminDashboardService dashboard, CancellationToken ct) =>
            Query(context, f => dashboard.JobsAsync(f, ct)));
        group.MapGet("/jobs/{id:guid}", async (Guid id, AdminDashboardService dashboard, CancellationToken ct) =>
            await dashboard.JobAsync(id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        group.MapGet("/usage", (HttpContext context, AdminDashboardService dashboard, CancellationToken ct) =>
            Query(context, f => dashboard.UsageAsync(f, ct)));
        group.MapGet("/usage/groups", (HttpContext context, AdminDashboardService dashboard, CancellationToken ct) =>
            Query(context, f => dashboard.GroupsAsync(f, ct), true));
        group.MapGet("/users", async (string? search, AdminDashboardService dashboard, CancellationToken ct) =>
            search?.Length > 100 ? Results.BadRequest() : Results.Ok(await dashboard.UsersAsync(search, ct)));
    }
    private static async Task<IResult> Query<T>(HttpContext context, Func<AdminDashboardFilter, Task<T>> read, bool calendar = false)
    {
        var filter = new AdminDashboardFilter();
        try
        {
            foreach (var property in typeof(AdminDashboardFilter).GetProperties())
            {
                if (!context.Request.Query.TryGetValue(property.Name, out var values))
                    continue;
                if (values.Count != 1)
                    return Results.BadRequest();
                var value = values[0]?.Trim();
                if (string.IsNullOrEmpty(value))
                    continue;
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object parsed = type == typeof(string) ? value : type == typeof(Guid) ? Guid.Parse(value) :
                    type == typeof(DateTimeOffset) ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime() :
                    type == typeof(bool) ? bool.Parse(value) : int.Parse(value, CultureInfo.InvariantCulture);
                property.SetValue(filter, parsed);
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { return Results.BadRequest(new { error = "Invalid dashboard filters." }); }
        return AdminDashboardService.Valid(filter) && (!calendar || filter.Level != "request") ? Results.Ok(await read(filter)) : Results.BadRequest(new
        {
            error = "Invalid dashboard filters."
        });
    }
}
