using System.Text.Json;

namespace BootAudit;

public static class Reporting
{
    public static void PrintConsole(Report report)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"BootAudit  v{report.Version}   host: {report.Host}   {report.Timestamp}");
        Console.WriteLine();

        foreach (var c in report.Checks)
        {
            var color = c.Severity switch
            {
                Severity.Info => ConsoleColor.Green,
                Severity.Advisory => ConsoleColor.Cyan,
                Severity.Warning => ConsoleColor.Yellow,
                Severity.Critical => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };

            Console.Write("[ ");
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write($"{c.SeverityLabel,-4}");
            Console.ForegroundColor = prev;
            Console.Write(" ] ");

            Console.WriteLine($"{c.Name,-18} {c.Message}");
        }

        Console.WriteLine();
        Console.Write("Overall: ");
        var overallColor = report.OverallSeverity switch
        {
            Severity.Info => ConsoleColor.Green,
            Severity.Advisory => ConsoleColor.Cyan,
            Severity.Warning => ConsoleColor.Yellow,
            Severity.Critical => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
        var prevOverall = Console.ForegroundColor;
        Console.ForegroundColor = overallColor;
        Console.Write(report.OverallSeverity.ToString().ToUpperInvariant());
        Console.ForegroundColor = prevOverall;
        Console.WriteLine($"  (exit {report.ExitCode})");
        Console.WriteLine();
    }

    public static string ToJson(Report report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(report, options);
    }
}