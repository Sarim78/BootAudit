using System.Text.Json.Serialization;

namespace BootAudit;

public enum Severity
{
    Info = 0,
    Advisory = 1,
    Warning = 2,
    Critical = 3
}

public sealed class CheckResult
{
    public string Name { get; init; } = "";
    public Severity Severity { get; init; }
    public string Message { get; init; } = "";
    public Dictionary<string, string>? Details { get; init; }

    [JsonIgnore]
    public string SeverityLabel => Severity switch
    {
        Severity.Info => "OK",
        Severity.Advisory => "INFO",
        Severity.Warning => "WARN",
        Severity.Critical => "CRIT",
        _ => "??"
    };
}

public sealed class Report
{
    public string Tool { get; init; } = "BootAudit";
    public string Version { get; init; } = "1.0.0";
    public string Host { get; init; } = Environment.MachineName;
    public string Timestamp { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public Severity OverallSeverity { get; init; }
    public int ExitCode { get; init; }
    public List<CheckResult> Checks { get; init; } = new();
}