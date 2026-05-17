using System.Diagnostics;
using System.Management;
using Microsoft.Win32;

namespace BootAudit;

// All six audit checks. Each method returns a CheckResult.
// Checks are intentionally read-only and tolerant of missing permissions.
public static class Checks
{
    private const string CheckLastShutdown = "LastShutdown";
    private const string CheckLastBoot = "LastBoot";
    private const string CheckShutdownReason = "ShutdownReason";
    private const string CheckUptimeRisk = "UptimeRisk";
    private const string CheckPendingReboot = "PendingReboot";
    private const string CheckBootIntegrity = "BootIntegrity";

    public static readonly string[] AllCheckNames =
    {
        CheckLastShutdown,
        CheckLastBoot,
        CheckShutdownReason,
        CheckUptimeRisk,
        CheckPendingReboot,
        CheckBootIntegrity
    };

    public static CheckResult Run(string name, int thresholdDays) => name switch
    {
        CheckLastShutdown => LastShutdown(),
        CheckLastBoot => LastBoot(),
        CheckShutdownReason => ShutdownReason(),
        CheckUptimeRisk => UptimeRisk(thresholdDays),
        CheckPendingReboot => PendingReboot(),
        CheckBootIntegrity => BootIntegrity(),
        _ => new CheckResult
        {
            Name = name,
            Severity = Severity.Advisory,
            Message = $"Unknown check: {name}"
        }
    };

    // 1. Last Shutdown
    public static CheckResult LastShutdown()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"System\CurrentControlSet\Control\Windows");

            var value = key?.GetValue("ShutdownTime");
            if (value is not byte[] bytes || bytes.Length != 8)
            {
                return new CheckResult
                {
                    Name = CheckLastShutdown,
                    Severity = Severity.Advisory,
                    Message = "ShutdownTime registry value not found or unreadable."
                };
            }

            long ft = BitConverter.ToInt64(bytes, 0);
            var lastShutdown = DateTime.FromFileTimeUtc(ft);

            return new CheckResult
            {
                Name = CheckLastShutdown,
                Severity = Severity.Info,
                Message = $"Last recorded shutdown: {lastShutdown:yyyy-MM-dd HH:mm:ss} UTC",
                Details = new() { ["last_shutdown_utc"] = lastShutdown.ToString("o") }
            };
        }
        catch (Exception ex)
        {
            return new CheckResult
            {
                Name = CheckLastShutdown,
                Severity = Severity.Advisory,
                Message = $"Failed to read shutdown time: {ex.Message}"
            };
        }
    }

    // 2. Last Boot (and current uptime)
    public static CheckResult LastBoot()
    {
        try
        {
            DateTime bootTime = GetLastBootUtc();
            var uptime = DateTime.UtcNow - bootTime;

            return new CheckResult
            {
                Name = CheckLastBoot,
                Severity = Severity.Info,
                Message = $"Last boot: {bootTime:yyyy-MM-dd HH:mm:ss} UTC " +
                          $"(uptime: {FormatUptime(uptime)})",
                Details = new()
                {
                    ["last_boot_utc"] = bootTime.ToString("o"),
                    ["uptime_days"] = uptime.TotalDays.ToString("F2")
                }
            };
        }
        catch (Exception ex)
        {
            return new CheckResult
            {
                Name = CheckLastBoot,
                Severity = Severity.Advisory,
                Message = $"Failed to read boot time: {ex.Message}"
            };
        }
    }

    // 3. Shutdown Reason (parses System event log)
    public static CheckResult ShutdownReason()
    {
        try
        {
            var unexpected = 0;
            var kernelPower = 0;
            string lastReason = "unknown";
            EventLogEntry? lastEntry = null;

            using var log = new EventLog("System");
            int count = log.Entries.Count;
            int scanned = 0;

            // Walk newest -> oldest, examine up to 500 entries.
            for (int i = count - 1; i >= 0 && scanned < 500; i--, scanned++)
            {
                EventLogEntry entry;
                try { entry = log.Entries[i]; }
                catch { continue; }

                if (entry.InstanceId is 1074 or 6008 or 41)
                {
                    lastEntry ??= entry;

                    if (entry.InstanceId == 6008) unexpected++;
                    if (entry.InstanceId == 41) kernelPower++;
                }
            }

            if (lastEntry is not null)
            {
                lastReason = lastEntry.InstanceId switch
                {
                    1074 => $"user-initiated (Event 1074, {lastEntry.TimeGenerated:yyyy-MM-dd HH:mm})",
                    6008 => $"unexpected shutdown (Event 6008, {lastEntry.TimeGenerated:yyyy-MM-dd HH:mm})",
                    41 => $"kernel power failure (Event 41, {lastEntry.TimeGenerated:yyyy-MM-dd HH:mm})",
                    _ => "unknown"
                };
            }

            var severity = (unexpected + kernelPower) switch
            {
                0 => Severity.Info,
                <= 2 => Severity.Warning,
                _ => Severity.Critical
            };

            string msg = severity == Severity.Info
                ? $"Last shutdown was {lastReason}."
                : $"Last shutdown was {lastReason}. " +
                  $"Found {unexpected} unexpected and {kernelPower} kernel-power events in last 500 entries.";

            return new CheckResult
            {
                Name = CheckShutdownReason,
                Severity = severity,
                Message = msg,
                Details = new()
                {
                    ["unexpected_count"] = unexpected.ToString(),
                    ["kernel_power_count"] = kernelPower.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new CheckResult
            {
                Name = CheckShutdownReason,
                Severity = Severity.Advisory,
                Message = $"Failed to read System event log: {ex.Message}"
            };
        }
    }

    // 4. Uptime Risk
    public static CheckResult UptimeRisk(int thresholdDays)
    {
        try
        {
            DateTime bootTime = GetLastBootUtc();
            double days = (DateTime.UtcNow - bootTime).TotalDays;

            var severity = ScoreUptime(days, thresholdDays);
            string msg = severity == Severity.Info
                ? $"Uptime {days:F1} days (threshold: {thresholdDays})."
                : $"Uptime {days:F1} days exceeds threshold of {thresholdDays} days. " +
                  "Long-running hosts often miss kernel patches.";

            return new CheckResult
            {
                Name = CheckUptimeRisk,
                Severity = severity,
                Message = msg,
                Details = new()
                {
                    ["uptime_days"] = days.ToString("F2"),
                    ["threshold_days"] = thresholdDays.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new CheckResult
            {
                Name = CheckUptimeRisk,
                Severity = Severity.Advisory,
                Message = $"Failed to evaluate uptime: {ex.Message}"
            };
        }
    }

    // Pure scoring function. Public so tests can hit it without WMI.
    public static Severity ScoreUptime(double days, int thresholdDays)
    {
        if (thresholdDays <= 0) return Severity.Info;
        if (days < thresholdDays) return Severity.Info;
        if (days < thresholdDays * 2) return Severity.Warning;
        return Severity.Critical;
    }

    // 5. Pending Reboot
    public static CheckResult PendingReboot()
    {
        try
        {
            bool cbsPending = KeyExists(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

            bool wuPending = KeyExists(
                Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");

            if (!cbsPending && !wuPending)
            {
                return new CheckResult
                {
                    Name = CheckPendingReboot,
                    Severity = Severity.Info,
                    Message = "No pending reboot detected."
                };
            }

            var reasons = new List<string>();
            if (cbsPending) reasons.Add("Component Based Servicing");
            if (wuPending) reasons.Add("Windows Update");

            return new CheckResult
            {
                Name = CheckPendingReboot,
                Severity = Severity.Warning,
                Message = $"Reboot pending: {string.Join(", ", reasons)}.",
                Details = new()
                {
                    ["cbs_pending"] = cbsPending.ToString(),
                    ["wu_pending"] = wuPending.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new CheckResult
            {
                Name = CheckPendingReboot,
                Severity = Severity.Advisory,
                Message = $"Failed to check pending reboot keys: {ex.Message}"
            };
        }
    }

    // 6. Boot Integrity (Secure Boot + BitLocker)
    public static CheckResult BootIntegrity()
    {
        string secureBoot = "Unknown";
        string bitlocker = "Unknown";
        var problems = new List<string>();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");

            var v = key?.GetValue("UEFISecureBootEnabled");
            if (v is int i)
            {
                secureBoot = i == 1 ? "ON" : "OFF";
                if (i != 1) problems.Add("Secure Boot disabled");
            }
            else
            {
                secureBoot = "Unknown (legacy BIOS or insufficient permissions)";
            }
        }
        catch
        {
            secureBoot = "Unknown";
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume");

            bool anyProtected = false;
            bool anyVolume = false;

            foreach (ManagementObject mo in searcher.Get())
            {
                anyVolume = true;
                var status = Convert.ToInt32(mo["ProtectionStatus"]);
                if (status == 1) anyProtected = true;
            }

            if (!anyVolume)
            {
                bitlocker = "Unknown";
            }
            else
            {
                bitlocker = anyProtected ? "ON" : "OFF";
                if (!anyProtected) problems.Add("BitLocker not protecting any volume");
            }
        }
        catch
        {
            bitlocker = "Unknown (requires elevation)";
        }

        var severity = problems.Count == 0 ? Severity.Info : Severity.Warning;

        return new CheckResult
        {
            Name = CheckBootIntegrity,
            Severity = severity,
            Message = $"Secure Boot: {secureBoot}   BitLocker: {bitlocker}" +
                      (problems.Count > 0 ? $"   Issues: {string.Join(", ", problems)}" : ""),
            Details = new()
            {
                ["secure_boot"] = secureBoot,
                ["bitlocker"] = bitlocker
            }
        };
    }

    // Helpers
    private static DateTime GetLastBootUtc()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT LastBootUpTime FROM Win32_OperatingSystem");

        foreach (ManagementObject mo in searcher.Get())
        {
            var raw = mo["LastBootUpTime"]?.ToString();
            if (!string.IsNullOrEmpty(raw))
            {
                return ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime();
            }
        }
        throw new InvalidOperationException("LastBootUpTime not available via WMI.");
    }

    private static bool KeyExists(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path);
        return key is not null;
    }

    public static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}