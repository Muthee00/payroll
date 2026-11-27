using System.Text.Json;

namespace PayMaster.Web.Helpers;

/// <summary>
/// Chart configuration for wwwroot/js/charts.js. Categorical colours come from a palette validated
/// for colour-vision deficiency (adjacent ΔE ≥ 9); they are assigned in fixed order, never cycled.
/// </summary>
public static class Charts
{
    public static readonly string[] Series = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Doughnut(IEnumerable<string> labels, IEnumerable<decimal> values) =>
        JsonSerializer.Serialize(new
        {
            type = "doughnut", format = "money", labels,
            datasets = new[] { new { data = values, backgroundColor = Series } }
        }, Json);

    public static string Lines(IEnumerable<string> labels, params (string Label, IEnumerable<decimal> Values)[] series) =>
        JsonSerializer.Serialize(new
        {
            type = "line", format = "money", legend = series.Length > 1, labels,
            datasets = series.Select((s, i) => new { label = s.Label, data = s.Values, borderColor = Series[i], fill = i == 0 })
        }, Json);

    public static string Bars(IEnumerable<string> labels, string label, IEnumerable<decimal> values, bool money = false, bool horizontal = false, decimal? max = null) =>
        JsonSerializer.Serialize(new
        {
            type = "bar", format = money ? "money" : "number", labels, indexAxis = horizontal ? "y" : null, max,
            datasets = new[] { new { label, data = values, backgroundColor = Series[0] } }
        }, Json);
}
