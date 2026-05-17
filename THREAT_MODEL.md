# Threat Model

This document explains the defensive-security rationale behind each BootAudit check. It is intended for reviewers, contributors, and learners who want to understand why each artifact matters, not just what the code does.

---

## Scope

BootAudit is a host-based, read-only audit tool. It does not detect intrusions in real time, block attacker actions, or modify system state. Its purpose is to surface boot-related signals that are commonly examined during:

- Incident response triage
- Patch and configuration compliance reviews
- Endpoint posture assessments
- Forensic timeline reconstruction

It is intentionally limited to artifacts that can be read without administrative privileges in most cases, making it safe to run as part of routine auditing.

---

## Threat Scenarios Addressed

### 1. Unpatched Kernel on Long-Running Hosts

**Scenario.** A workstation or server has been running for 200+ days. Windows Update has installed patches that require a reboot to take effect. The kernel and several system services are still running the vulnerable code. An attacker with local access (or a remote code execution foothold) can exploit a kernel-level CVE that was technically patched months ago.

**How BootAudit surfaces this.** The `UptimeRisk` check flags hosts above a configurable threshold (default 30 days). The `PendingReboot` check independently confirms whether updates are waiting to apply.

**Mitigations this signal supports.** Scheduled reboot policies, patch compliance enforcement via WSUS or Intune, alerting when uptime exceeds organizational SLAs.

---

### 2. Unexpected Shutdowns Indicating Instability or Tampering

**Scenario.** A host experiences repeated unexpected shutdowns (Event ID 6008) or kernel power failures (Event ID 41). Causes range from failing hardware to attacker-induced crashes used to disable monitoring agents or force a reboot that loads attacker persistence.

**How BootAudit surfaces this.** The `ShutdownReason` check parses recent power-related events (1074 for user-initiated, 6008 for unexpected, 41 for kernel power) and flags clusters of abnormal events.

**Mitigations this signal supports.** Correlate with EDR telemetry, escalate to incident response if unexpected shutdowns coincide with other anomalies (failed logons, new service installs, AV being disabled).

---

### 3. Boot-Time Security Controls Disabled

**Scenario.** Secure Boot is disabled, leaving the host vulnerable to bootkit-class malware that loads before the OS and below the level most EDR products can see. BitLocker is disabled, leaving disk contents recoverable if the device is stolen or seized.

**How BootAudit surfaces this.** The `BootIntegrity` check reports the status of both controls.

**Mitigations this signal supports.** Baseline enforcement via group policy, CIS-benchmark alignment, hardware refresh prioritization for non-compliant devices.

---

### 4. Pending Reboots Hiding Patch Gaps

**Scenario.** A vulnerability scanner reports a host as patched because the relevant KBs are installed. In reality, the patches have not taken effect because the reboot is pending. The host is exploitable despite appearing compliant.

**How BootAudit surfaces this.** The `PendingReboot` check reads the Component Based Servicing (`CBS`) and Windows Update keys that Windows itself uses to track pending reboots.

**Mitigations this signal supports.** Reconciling vulnerability scanner output with actual patch effective state, closing reporting gaps in compliance dashboards.

---

## What BootAudit Does Not Detect

To be honest about scope:

- **Live attacker activity.** BootAudit is not EDR. It cannot detect process injection, lateral movement, credential theft, or any behavior that does not leave a boot-time artifact.
- **Memory-resident malware.** Threats that never touch disk or registry boot keys are invisible to this tool.
- **Sophisticated event log tampering.** A determined attacker with SYSTEM privileges can clear or forge event logs. BootAudit reports what the logs say; it does not verify the logs have not been altered.
- **Network-level threats.** No network telemetry is examined.
- **Vulnerability identification at the CVE level.** BootAudit flags risk indicators (long uptime, pending reboots). It does not enumerate specific CVEs the host is exposed to.

---

## Data Handled

BootAudit reads, but never writes, the following:

| Source                                                              | Sensitivity |
|---------------------------------------------------------------------|-------------|
| Registry keys under `HKLM\System` and `HKLM\SOFTWARE\Microsoft`     | Low         |
| Windows System event log (last 100 power-related events by default) | Low to Medium |
| WMI namespace `root\cimv2` (OS metadata)                            | Low         |

Output may contain hostname, OS version, timestamps, and event IDs. Output should be treated as low-sensitivity operational data, but reports collected across an enterprise can become more sensitive in aggregate. Do not commit captured reports to public repositories.

---

## Assumptions

- The host clock is roughly accurate. Significant clock skew will produce misleading uptime values.
- The Windows event log has not been wiped or rotated past the lookback window.
- The user running BootAudit has read access to the registry keys and event logs queried. Without it, affected checks return an `Info` severity rather than failing the audit.

---

## Reporting Issues

If you discover a security-relevant bug (for example, a path that causes BootAudit to write to disk unexpectedly, or to require more permissions than documented), please open a private issue rather than a public one.