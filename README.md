# BootAudit

> **Educational and personal portfolio use only. Read-only by design.**
> A C# Windows CLI built to understand boot artifacts, patch state, and uptime risk, so you can defend the endpoint that produces them.

---

## Overview

This project audits a Windows host's boot and shutdown activity in a transparent, read-only way. The goal is not to build production security software, it is to understand the mechanics behind the artifacts that incident responders, SOC analysts, and patch-compliance teams rely on every day, so you can recognize them in the wild.

Understanding boot forensics is a fundamental skill for:

- SOC Analysts triaging endpoint alerts and reconstructing timelines
- Incident Responders investigating unexpected reboots or crashes
- Patch and Vulnerability teams enforcing reboot compliance after Windows Update
- Cybersecurity Students learning Windows internals to build defensive intuition

---

## Getting Started

### Prerequisites

- Windows 10, Windows 11, or Windows Server 2019+
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- PowerShell (preinstalled on Windows)

### Installation

```powershell
# 1. Clone the repository
git clone https://github.com/Sarim78/BootAudit.git
cd BootAudit

# 2. Restore dependencies
dotnet restore

# 3. Build the project
dotnet build -c Release
```

The compiled executable is produced at `src/bin/Release/net8.0/BootAudit.exe`.

### Running the Audit

```powershell
# Run all checks against the local machine
dotnet run --project src/BootAudit.csproj

# Or run the compiled binary directly
.\src\bin\Release\net8.0\BootAudit.exe
```

The tool will:

1. Run all six checks against the local machine
2. Print a severity-scored report to the console
3. Exit with a code matching the highest severity observed

### Producing Machine-Readable Output

```powershell
.\src\bin\Release\net8.0\BootAudit.exe --json > audit.json
```

### Running the Tests

```powershell
dotnet test
```

### Optional: Build a Portable Executable

```powershell
dotnet publish src/BootAudit.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

This produces a single `.exe` that runs on machines without the .NET runtime installed.

---

## The Six Checks

| # | Check               | Source                                                            | What it catches                                    |
|---|---------------------|-------------------------------------------------------------------|----------------------------------------------------|
| 1 | Last Shutdown       | `HKLM\System\CurrentControlSet\Control\Windows\ShutdownTime`      | Forensic baseline of last clean shutdown           |
| 2 | Last Boot           | WMI `Win32_OperatingSystem.LastBootUpTime`                        | Actual current uptime                              |
| 3 | Shutdown Reason     | Event log IDs `1074`, `6008`, `41`                                | Unexpected shutdowns and kernel power failures     |
| 4 | Uptime Risk         | Computed delta vs. `--threshold-days`                             | Unpatched-kernel risk on long-running hosts        |
| 5 | Pending Reboot      | CBS `RebootPending` + `WindowsUpdate\Auto Update\RebootRequired`  | Patches installed but not yet active               |
| 6 | Boot Integrity      | Secure Boot status + BitLocker status                             | Disabled boot-security features                    |

Each check returns one of four severities: **Info**, **Advisory**, **Warning**, or **Critical**. The overall exit code reflects the highest severity observed.

---

## CLI Options

| Flag                       | Description                                                       | Default |
|----------------------------|-------------------------------------------------------------------|---------|
| `--json`                   | Emit machine-readable JSON instead of console output              | off     |
| `--threshold-days <n>`     | Uptime threshold above which `UptimeRisk` flags Warning           | `30`    |
| `--check <name>`           | Run a single check by name (repeatable)                           | all     |
| `--verbose`                | Include raw artifact details (registry values, event records)     | off     |
| `--help`                   | Display usage information                                         |         |

### Examples

```powershell
# Run only the pending-reboot check
BootAudit.exe --check PendingReboot --json

# Enforce a stricter 14-day uptime policy
BootAudit.exe --threshold-days 14
```

---

## Exit Codes

BootAudit follows the Nagios plugin convention so it integrates cleanly with existing monitoring tooling.

| Code | Severity  | Meaning                                                       |
|------|-----------|---------------------------------------------------------------|
| `0`  | OK        | All checks passed                                             |
| `1`  | Advisory  | Informational findings, no action required                    |
| `2`  | Warning   | Action recommended (for example, uptime over threshold)       |
| `3`  | Critical  | Immediate attention required (for example, repeated `6008`)   |

---

## Sample Output

```
BootAudit  v1.0.0   host: WS-FINANCE-04   2026-05-17T14:22:08Z

[ OK   ] Last Shutdown      2026-05-15 22:14:03 UTC
[ OK   ] Last Boot          2026-05-15 22:14:51 UTC  (uptime: 1d 16h)
[ OK   ] Shutdown Reason    Last shutdown was user-initiated (Event 1074)
[ OK   ] Uptime Risk        1.7 days (threshold: 30)
[ WARN ] Pending Reboot     Windows Update reboot required
[ OK   ] Boot Integrity     Secure Boot: ON   BitLocker: ON

Overall: WARNING  (exit 2)
```

---

## Defensive Takeaways

After building this, you will understand why defenders recommend:

- **Patch compliance enforcement** so pending reboots do not linger and leave kernel CVEs exploitable
- **Centralized event log forwarding (WEF or SIEM)** so unexpected shutdowns are detected even after the host is wiped
- **Uptime monitoring thresholds** because hosts up for 200+ days are almost always missing critical updates
- **Secure Boot and BitLocker as baseline controls** to defend against bootkit-class threats
- **Endpoint Detection and Response (EDR)** to correlate boot anomalies with broader attacker behavior

---

## Limitations

- Windows only (`net8.0-windows` target)
- The Pending Reboot check may require Administrator rights on hardened systems
- This is a personal learning project and has not been audited for production use

---

## Disclaimer

BootAudit is provided as-is under the MIT license. It is a personal portfolio project for educational and demonstration purposes only. It is not affiliated with, endorsed by, or representative of any employer, academic institution, or commercial product. Always validate findings independently before acting on them.

---

## License

[MIT](LICENSE)
