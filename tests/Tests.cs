using System.Text.Json;
using Xunit;

namespace BootAudit.Tests;

public class UptimeRiskTests
{
    [Theory]
    [InlineData(0.5, 30, Severity.Info)]
    [InlineData(29.9, 30, Severity.Info)]
    [InlineData(30.0, 30, Severity.Warning)]
    [InlineData(45.0, 30, Severity.Warning)]
    [InlineData(59.9, 30, Severity.Warning)]
    [InlineData(60.0, 30, Severity.Critical)]
    [InlineData(200.0, 30, Severity.Critical)]
    public void ScoreUptime_ReturnsExpectedSeverity(double days, int threshold, Severity expected)
    {
        var actual = Checks.ScoreUptime(days, threshold);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScoreUptime_ZeroThreshold_AlwaysInfo()
    {
        Assert.Equal(Severity.Info, Checks.ScoreUptime(500, 0));
    }

    [Fact]
    public void ScoreUptime_NegativeThreshold_AlwaysInfo()
    {
        Assert.Equal(Severity.Info, Checks.ScoreUptime(500, -1));
    }
}

public class FormatUptimeTests
{
    [Fact]
    public void FormatUptime_LessThanHour_ShowsMinutes()
    {
        var result = Checks.FormatUptime(TimeSpan.FromMinutes(42));
        Assert.Equal("42m", result);
    }

    [Fact]
    public void FormatUptime_HoursOnly_ShowsHoursAndMinutes()
    {
        var result = Checks.FormatUptime(TimeSpan.FromMinutes(125)); // 2h 5m
        Assert.Equal("2h 5m", result);
    }

    [Fact]
    public void FormatUptime_OverOneDay_ShowsDaysAndHours()
    {
        var result = Checks.FormatUptime(TimeSpan.FromHours(50)); // 2d 2h
        Assert.Equal("2d 2h", result);
    }
}

public class ReportingTests
{
    [Fact]
    public void ToJson_ProducesValidParseableJson()
    {
        var report = BuildSampleReport(Severity.Warning, 2);

        var json = Reporting.ToJson(report);

        // Round-trip parse confirms the JSON is well-formed.
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("tool", out _));
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
    }

    [Fact]
    public void ToJson_UsesSnakeCaseFieldNames()
    {
        var report = BuildSampleReport(Severity.Info, 0);

        var json = Reporting.ToJson(report);

        Assert.Contains("\"overall_severity\"", json);
        Assert.Contains("\"exit_code\"", json);
        Assert.DoesNotContain("\"OverallSeverity\"", json);
    }

    [Fact]
    public void ToJson_SerializesSeverityAsString()
    {
        var report = BuildSampleReport(Severity.Critical, 3);

        var json = Reporting.ToJson(report);

        Assert.Contains("\"Critical\"", json);
    }

    [Fact]
    public void ToJson_IncludesAllChecks()
    {
        var report = BuildSampleReport(Severity.Warning, 2);

        var json = Reporting.ToJson(report);
        using var doc = JsonDocument.Parse(json);
        var checks = doc.RootElement.GetProperty("checks");

        Assert.Equal(2, checks.GetArrayLength());
    }

    private static Report BuildSampleReport(Severity overall, int exitCode) => new()
    {
        OverallSeverity = overall,
        ExitCode = exitCode,
        Checks = new List<CheckResult>
        {
            new()
            {
                Name = "LastBoot",
                Severity = Severity.Info,
                Message = "Last boot: 2026-05-15 22:14:51 UTC (uptime: 1d 16h)"
            },
            new()
            {
                Name = "PendingReboot",
                Severity = overall,
                Message = "Reboot pending: Windows Update."
            }
        }
    };
}

public class CheckResultTests
{
    [Theory]
    [InlineData(Severity.Info, "OK")]
    [InlineData(Severity.Advisory, "INFO")]
    [InlineData(Severity.Warning, "WARN")]
    [InlineData(Severity.Critical, "CRIT")]
    public void SeverityLabel_MapsCorrectly(Severity sev, string expectedLabel)
    {
        var result = new CheckResult { Name = "Test", Severity = sev, Message = "" };
        Assert.Equal(expectedLabel, result.SeverityLabel);
    }
}