using System.Globalization;
using Fatewake.Observability;
namespace Fatewake.Web.Presentation;
/// <summary>Constructs safe dashboard query strings and formats nullable reported measurements.</summary>
public static class AdminDashboardQuery
{
    /// <summary>Builds common filters, leaving pagination and tree expansion to the requesting view.</summary>
    public static string Build(AdminDashboardFilter f)
    {
        var values = new Dictionary<string, string?>
        {
            ["range"] = f.Range,
            ["from"] = f.From?.ToString("O"),
            ["to"] = f.To?.ToString("O"),
            ["provider"] = f.Provider,
            ["model"] = f.Model,
            ["operation"] = f.Operation,
            ["jobStatus"] = f.JobStatus,
            ["outcome"] = f.Outcome,
            ["accountId"] = f.AccountId?.ToString(),
            ["unattributed"] = f.Unattributed.ToString(),
            ["timeZone"] = f.TimeZone
        };
        return string.Join("&", values.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value!)));
    }
    /// <summary>Displays unknown usage distinctly from reported zero.</summary>
    public static string Number(long? value) => value?.ToString("N0", CultureInfo.CurrentCulture) ?? "Unavailable";
    /// <summary>Displays an unknown provider measurement distinctly from zero.</summary>
    public static string Number(decimal? value) => value?.ToString("N2", CultureInfo.CurrentCulture) ?? "Unavailable";
    /// <summary>Formats actual costs without combining currencies.</summary>
    public static string Costs(AiCurrencyCost[] costs) => costs.Length == 0 ? "Unavailable" : string.Join(" · ", costs.Select(x => $"{x.Amount:0.####} {x.Currency}"));
    /// <summary>Converts an instant into the selected reporting timezone.</summary>
    public static string Date(DateTimeOffset? value, string zone) => value is { } instant ? TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("yyyy-MM-dd HH:mm:ss") : "Unavailable";
}
