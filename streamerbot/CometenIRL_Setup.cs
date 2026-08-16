using System;

// CometenIRLAlerts - Streamer.bot global setup v1.0
//
// Create an action named:
//   CometenIRL_Setup
//
// Add one "Execute C# Code" sub-action and paste this entire file.
// Run the action once after importing/installing Cometen IRL Alerts.
//
// Behavior:
// - Creates missing PERSISTED globals with safe defaults.
// - Existing globals are NEVER overwritten.
// - Secrets/install-specific values are created blank and must be filled manually.
// - Safe to run again after updates.

public class CPHInline
{
    private int created = 0;
    private int kept = 0;

    public bool Execute()
    {
        CPH.LogInfo("CometenIRL Setup: starting global setup.");

        // Relay / remote-control configuration
        CPH.LogInfo("CometenIRL Setup: configuring relay/remote globals.");
        SeedString("CometenIRL_RelayUrl", "https://la1ona.com/CometenIRLAlerts_Relay");
        SeedString("CometenIRL_SenderToken", "");
        SeedString("CometenIRL_BelaboxStreamId", "");

        // BELABOX Cloud watchdog configuration
        SeedString("CometenIRL_BelaboxStatsBaseUrl", "http://eu.srt.belabox.net:8080");
        SeedInt("CometenIRL_BelaboxFailChecks", 2);
        SeedInt("CometenIRL_BelaboxQueryFailChecks", 3);
        SeedInt("CometenIRL_BelaboxRecoverChecks", 5);
        SeedBool("CometenIRL_WatchdogLiveOnly", true);
        SeedBool("CometenIRL_WatchdogArmed", false);

        // OBS scene names
        SeedString("CometenIRL_StartingSoonScene", "IRL - STARTING SOON");
        SeedString("CometenIRL_DefaultReturnScene", "BELABOX SRT");
        SeedString("CometenIRL_FallbackScene", "IRL - SIGNAL MISTET");
        SeedString("CometenIRL_BrbScene", "IRL - BRB");
        SeedString("CometenIRL_EndingScene", "IRL - ENDING");

        // IRL lifecycle / admin control
        SeedBool("CometenIRL_IrlMode", false);
        SeedString("CometenIRL_Language", "no");
        SeedInt("CometenIRL_EndingSeconds", 25);
        SeedBool("CometenIRL_EndPending", false);
        SeedInt("CometenIRL_EndSequence", 0);

        // Twitch Channel Points
        SeedBool("CometenIRL_ManageRewards", false);
        SeedString("CometenIRL_NormalRewardGroup", "NORMAL");
        SeedString("CometenIRL_IrlRewardGroup", "IRL");

        // BELABOX watchdog telemetry / runtime state
        SeedBool("CometenIRL_BelaboxConnected", false);
        SeedInt("CometenIRL_BelaboxBitrate", 0);
        SeedDouble("CometenIRL_BelaboxRtt", 0.0);
        SeedInt("CometenIRL_BelaboxDroppedPackets", 0);
        SeedString("CometenIRL_BelaboxState", "standby");
        SeedInt("CometenIRL_BelaboxFailCount", 0);
        SeedInt("CometenIRL_BelaboxRecoverCount", 0);
        SeedInt("CometenIRL_BelaboxQueryFailCount", 0);
        SeedBool("CometenIRL_SrtFallbackActive", false);
        SeedString("CometenIRL_SrtReturnScene", "BELABOX SRT");

        CPH.SetGlobalVar("CometenIRL_SetupVersion", "1.0", true);

        CPH.LogInfo(
            "CometenIRL Setup: complete. Created=" + created
            + ", kept existing=" + kept
            + ", setupVersion=1.0."
        );

        WarnIfBlank("CometenIRL_SenderToken",
            "CometenIRL Setup: CometenIRL_SenderToken is blank. Add the sender token before alerts/remote control are used.");

        WarnIfBlank("CometenIRL_BelaboxStreamId",
            "CometenIRL Setup: CometenIRL_BelaboxStreamId is blank. Add the BELABOX stream ID before enabling the watchdog.");

        CPH.LogInfo(
            "CometenIRL Setup: Channel Point automation defaults to OFF. "
            + "Create/configure NORMAL and IRL reward groups, then set CometenIRL_ManageRewards=true."
        );

        return true;
    }

    private void SeedString(string name, string defaultValue)
    {
        string current = null;

        try
        {
            current = CPH.GetGlobalVar<string>(name, true);
        }
        catch
        {
            current = null;
        }

        if (current == null)
        {
            CPH.SetGlobalVar(name, defaultValue, true);
            created++;
            CPH.LogInfo("CometenIRL Setup: created " + name + " = " + DisplayString(name, defaultValue));
        }
        else
        {
            kept++;
            CPH.LogInfo("CometenIRL Setup: kept existing " + name + " = " + DisplayString(name, current));
        }
    }

    private void SeedBool(string name, bool defaultValue)
    {
        bool? current = null;

        try
        {
            current = CPH.GetGlobalVar<bool?>(name, true);
        }
        catch
        {
            current = null;
        }

        if (!current.HasValue)
        {
            CPH.SetGlobalVar(name, defaultValue, true);
            created++;
            CPH.LogInfo("CometenIRL Setup: created " + name + " = " + defaultValue);
        }
        else
        {
            kept++;
            CPH.LogInfo("CometenIRL Setup: kept existing " + name + " = " + current.Value);
        }
    }

    private void SeedInt(string name, int defaultValue)
    {
        int? current = null;

        try
        {
            current = CPH.GetGlobalVar<int?>(name, true);
        }
        catch
        {
            current = null;
        }

        if (!current.HasValue)
        {
            CPH.SetGlobalVar(name, defaultValue, true);
            created++;
            CPH.LogInfo("CometenIRL Setup: created " + name + " = " + defaultValue);
        }
        else
        {
            kept++;
            CPH.LogInfo("CometenIRL Setup: kept existing " + name + " = " + current.Value);
        }
    }

    private void SeedDouble(string name, double defaultValue)
    {
        double? current = null;

        try
        {
            current = CPH.GetGlobalVar<double?>(name, true);
        }
        catch
        {
            current = null;
        }

        if (!current.HasValue)
        {
            CPH.SetGlobalVar(name, defaultValue, true);
            created++;
            CPH.LogInfo("CometenIRL Setup: created " + name + " = " + defaultValue);
        }
        else
        {
            kept++;
            CPH.LogInfo("CometenIRL Setup: kept existing " + name + " = " + current.Value);
        }
    }

    private void WarnIfBlank(string name, string warning)
    {
        string value = string.Empty;

        try
        {
            value = CPH.GetGlobalVar<string>(name, true) ?? string.Empty;
        }
        catch
        {
            value = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            CPH.LogWarn(warning);
        }
    }

    private string DisplayString(string name, string value)
    {
        if (name == "CometenIRL_SenderToken")
        {
            return string.IsNullOrWhiteSpace(value) ? "<EMPTY>" : "<SET>";
        }

        if (name == "CometenIRL_BelaboxStreamId")
        {
            return string.IsNullOrWhiteSpace(value) ? "<EMPTY>" : "<SET>";
        }

        return "\"" + (value ?? string.Empty) + "\"";
    }
}
