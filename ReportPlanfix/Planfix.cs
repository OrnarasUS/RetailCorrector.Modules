// Подробная документация API:
// https://clck.ru/3Fsyen

using RetailCorrector.API.Static;
using RetailCorrector.API;
using System.ComponentModel;
using System.Text;

namespace ReportPlanfix;

[DisplayName("PlanFix (JSON)")]
public class Planfix : Report
{
    public override string Id => "PlanFix";

    public override Dictionary<string, string> Properties => _properties;

    private readonly Dictionary<string, string> _properties = new()
    {
        {"taskid", "0"},
        {"fiscal", "Касса"},
        {"account", "ats"},
        {"method", "correction-receipt"},
        {"template", "{\"task\": %task%, \"success\": %success%, " +
            "\"total\": %total%, \"fiscal\": \"%name%\"}"}
    };

    private string taskId => Properties["taskid"];
    private string fiscalName => Properties["fiscal"];
    private string account => Properties["account"];
    private string method => Properties["method"];
    private string templateBody => Properties["template"];

    public override async Task Send()
    {
        var body = new StringBuilder(templateBody);
        body.Replace("%task%", taskId);
        body.Replace("%success%", $"{Counters.SuccessCount}");
        body.Replace("%total%", $"{Counters.TotalCount}");
        body.Replace("%name%", fiscalName);

        var uri = $"https://{account}.planfix.ru/webhook/json/{method}";
        var req = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body.ToString())
        };
        using var client = new HttpClient();
        var resp = await client.SendAsync(req);
        var result = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode || result != $"{{\"task\":{taskId}}}") throw new HttpRequestException();
    }
}

