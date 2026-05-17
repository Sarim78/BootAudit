namespace BootAudit;

public static class Program
{
    public static int Main(string[] args)
    {
        var opts = ParseArgs(args);

        if (opts.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var checksToRun = opts.SelectedChecks.Count > 0
            ? opts.SelectedChecks
            : Checks.AllCheckNames.ToList();

        var results = checksToRun
            .Select(name => Checks.Run(name, opts.ThresholdDays))
            .ToList();

        var overall = results.Count == 0
            ? Severity.Info
            : results.Max(r => r.Severity);

        int exitCode = (int)overall;

        var report = new Report
        {
            OverallSeverity = overall,
            ExitCode = exitCode,
            Checks = results
        };

        if (opts.Json)
        {
            Console.WriteLine(Reporting.ToJson(report));
        }
        else
        {
            Reporting.PrintConsole(report);
        }

        return exitCode;
    }

    private sealed class Options
    {
        public bool Json { get; set; }
        public bool Verbose { get; set; }
        public bool ShowHelp { get; set; }
        public int ThresholdDays { get; set; } = 30;
        public List<string> SelectedChecks { get; } = new();
    }

    private static Options ParseArgs(string[] args)
    {
        var opts = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--json":
                    opts.Json = true;
                    break;
                case "--verbose":
                    opts.Verbose = true;
                    break;
                case "--help":
                case "-h":
                    opts.ShowHelp = true;
                    break;
                case "--threshold-days":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var n) && n > 0)
                        opts.ThresholdDays = n;
                    break;
                case "--check":
                    if (i + 1 < args.Length)
                        opts.SelectedChecks.Add(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {a}");
                    opts.ShowHelp = true;
                    break;
            }
        }

        return opts;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("BootAudit - Windows boot forensics and patch-compliance CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  BootAudit.exe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --json                  Emit machine-readable JSON output");
        Console.WriteLine("  --threshold-days <n>    Uptime threshold for UptimeRisk (default 30)");
        Console.WriteLine("  --check <name>          Run a single check (repeatable)");
        Console.WriteLine("  --verbose               Reserved for future detailed output");
        Console.WriteLine("  --help, -h              Show this help text");
        Console.WriteLine();
        Console.WriteLine("Available checks:");
        foreach (var name in Checks.AllCheckNames)
            Console.WriteLine($"  {name}");
        Console.WriteLine();
        Console.WriteLine("Exit codes: 0 OK, 1 Advisory, 2 Warning, 3 Critical");
    }
}