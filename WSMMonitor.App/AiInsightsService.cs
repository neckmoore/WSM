using System.Net.Http.Json;
using System.Text;

namespace WSMMonitor;

public sealed class AiInsightsService : IDisposable
{
    private readonly HttpClient _http;

    public AiInsightsService(string baseUrl = "http://127.0.0.1:11434")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    public async Task<string> AnalyzeLogSummaryAsync(LogAnalysisResult r, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(r);
        var req = new
        {
            model = "llama3.1:8b",
            prompt,
            stream = false,
            options = new { temperature = 0.2 }
        };

        using var res = await _http.PostAsJsonAsync("api/generate", req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI service error: {(int)res.StatusCode} {res.ReasonPhrase}\n{body}");

        // Minimal resilient parse without strict DTO dependency.
        var marker = "\"response\":";
        var idx = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return body;
        var start = body.IndexOf('"', idx + marker.Length);
        if (start < 0) return body;
        var sb = new StringBuilder();
        bool esc = false;
        for (int i = start + 1; i < body.Length; i++)
        {
            var ch = body[i];
            if (esc)
            {
                sb.Append(ch switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => ch
                });
                esc = false;
                continue;
            }
            if (ch == '\\') { esc = true; continue; }
            if (ch == '"') break;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string BuildPrompt(LogAnalysisResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an SRE analyst. Analyze the Windows events below and give a short actionable summary in English.");
        sb.AppendLine("Response format:");
        sb.AppendLine("1) Key issues (up to 5 bullets)");
        sb.AppendLine("2) Likely causes");
        sb.AppendLine("3) What to check or do now (checklist)");
        sb.AppendLine("4) What to automate in monitoring");
        sb.AppendLine();
        sb.AppendLine($"Period: {r.From:yyyy-MM-dd HH:mm} - {r.To:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Totals: {r.Total}, Critical: {r.Critical}, Error: {r.Error}, Warning: {r.Warning}");
        sb.AppendLine("Top providers:");
        foreach (var p in r.TopProviders.Take(10))
            sb.AppendLine($"- {p.Provider}: {p.Count}");
        sb.AppendLine("Top Event IDs:");
        foreach (var i in r.TopEventIds.Take(10))
            sb.AppendLine($"- {i.EventId}: {i.Count}");
        sb.AppendLine("Sample events:");
        foreach (var e in r.Samples.Take(20))
            sb.AppendLine($"- [{e.Time:MM-dd HH:mm}] {e.Log}/{e.Level} {e.Provider} (ID {e.EventId}): {e.Message}");
        return sb.ToString();
    }

    public void Dispose() => _http.Dispose();
}
